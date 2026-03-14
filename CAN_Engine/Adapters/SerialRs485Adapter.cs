using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using ATS.CAN.Engine.Models;
using ATS.CAN.Engine.Services;
using ATS.CAN.Engine.Services.Interfaces;

namespace ATS.CAN.Engine.Adapters
{
    /// <summary>
    /// RS485 Serial adapter implementation using Virtual CAN framing.
    /// Protocols: [0xAA] [ID_H] [ID_L] [DLC] [DATA0...7] [CRC] [0x55]
    /// </summary>
    public class SerialRs485Adapter : ICanAdapter
    {
        public string AdapterType => "RS485 Unified";

        private readonly ICanLogger _logger;
        private string _portName = string.Empty;
        private SerialPort? _serialPort;
        private readonly ConcurrentQueue<byte> _rxBuffer = new();
        private volatile bool _connected;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly object _sendLock = new object();
        private DateTime _lastMessageTime = DateTime.MinValue;
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);
        private bool _timeoutNotified = false;

        // Protocol constants (Must match firmware rs485_service.h)
        private const byte FRAME_HEADER = 0xAA;
        private const byte FRAME_FOOTER = 0x55;
        private const int FRAME_SIZE = 14;

        public bool IsConnected => _connected;
        public event Action<CANMessage>? MessageReceived;
        public event EventHandler<string>? DataTimeout;
        public event EventHandler<bool>? ConnectionStatusChanged;

        public SerialRs485Adapter(ICanLogger? logger = null)
        {
            _logger = logger ?? DefaultCanLogger.Instance;
        }

        public bool Connect(CanAdapterConfig config, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (config is not SerialRs485AdapterConfig rsConfig)
            {
                errorMessage = "Invalid configuration type for RS485 adapter";
                return false;
            }

            try
            {
                _serialPort = new SerialPort(rsConfig.PortName, rsConfig.BaudRate, Parity.None, 8, StopBits.One);
                
                // High performance configuration
                _serialPort.ReadBufferSize = 65536;
                _serialPort.WriteBufferSize = 65536;
                _serialPort.WriteTimeout = 100;
                _serialPort.ReadTimeout = 100;
                
                _serialPort.Open();
                _portName = rsConfig.PortName;
                _connected = true;

                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => ReadSerialLoopAsync(_cancellationTokenSource.Token));

                ConnectionStatusChanged?.Invoke(this, true);
                _logger.LogInfo($"RS485 Adapter Connected on {rsConfig.PortName} at {rsConfig.BaudRate} bps", "SerialRs485Adapter");
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                _logger.LogError($"RS485 connection error: {ex.Message}", "SerialRs485Adapter");
                _connected = false;
                return false;
            }
        }

        public void Disconnect()
        {
            if (!_connected) return;

            _connected = false;
            _cancellationTokenSource?.Cancel();
            
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.Close();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error closing serial port: {ex.Message}", "SerialRs485Adapter");
            }

            ConnectionStatusChanged?.Invoke(this, false);
            _logger.LogInfo("RS485 Disconnected", "SerialRs485Adapter");
        }

        public bool SendMessage(uint id, byte[] data)
        {
            if (!_connected || _serialPort == null) return false;

            try
            {
                var frame = CreateFrame(id, data ?? new byte[0]);
                lock (_sendLock)
                {
                    _serialPort.Write(frame, 0, frame.Length);
                }

                // Fire event for TX visibility in monitoring tools
                MessageReceived?.Invoke(new CANMessage(id, data ?? new byte[0], DateTime.Now, "TX"));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"RS485 Send error: {ex.Message}", "SerialRs485Adapter");
                return false;
            }
        }

        public string[] GetAvailableOptions()
        {
            return SerialPort.GetPortNames();
        }

        private async Task ReadSerialLoopAsync(CancellationToken token)
        {
            var tempBuffer = new byte[1024];
            _lastMessageTime = DateTime.UtcNow;

            while (_connected && !token.IsCancellationRequested)
            {
                try
                {
                    if (_serialPort is { IsOpen: true } && _serialPort.BytesToRead > 0)
                    {
                        int read = _serialPort.Read(tempBuffer, 0, tempBuffer.Length);
                        for (int i = 0; i < read; i++)
                        {
                            _rxBuffer.Enqueue(tempBuffer[i]);
                        }

                        ProcessBuffer();
                        _lastMessageTime = DateTime.UtcNow;
                        _timeoutNotified = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Serial Read Loop Error: {ex.Message}", "SerialRs485Adapter");
                }

                // Check for timeout (1kHz data should be constant)
                if (!_timeoutNotified && DateTime.UtcNow - _lastMessageTime > _timeout)
                {
                    _timeoutNotified = true;
                    DataTimeout?.Invoke(this, "RS485 Data Timeout");
                }

                await Task.Delay(1, token); // Tight loop for low latency
            }
        }

        private void ProcessBuffer()
        {
            // Protocol: [AA] [ID_H] [ID_L] [DLC] [D0-D7] [CRC] [55] = 14 Bytes
            while (_rxBuffer.Count >= FRAME_SIZE)
            {
                if (_rxBuffer.TryPeek(out byte header) && header == FRAME_HEADER)
                {
                    var bytes = _rxBuffer.ToArray();
                    if (bytes.Length < FRAME_SIZE) return;

                    // Check footer
                    if (bytes[FRAME_SIZE - 1] == FRAME_FOOTER)
                    {
                        // Verify CRC
                        byte crc = 0;
                        for (int i = 0; i < 12; i++) crc ^= bytes[i];

                        if (crc == bytes[12])
                        {
                            // VALID PACKET
                            uint id = (uint)((bytes[1] << 8) | bytes[2]);
                            byte dlc = bytes[3];
                            byte[] data = new byte[dlc];
                            Array.Copy(bytes, 4, data, 0, dlc);

                            // Dequeue processed bytes
                            for (int i = 0; i < FRAME_SIZE; i++) _rxBuffer.TryDequeue(out _);

                            MessageReceived?.Invoke(new CANMessage(id, data));
                            continue; 
                        }
                    }
                    
                    // Invalid packet or misalignment: drop header and search again
                    _rxBuffer.TryDequeue(out _);
                }
                else
                {
                    // Not a header: drop byte
                    _rxBuffer.TryDequeue(out _);
                }
            }
        }

        private byte[] CreateFrame(uint id, byte[] data)
        {
            byte[] frame = new byte[FRAME_SIZE];
            frame[0] = FRAME_HEADER;
            frame[1] = (byte)((id >> 8) & 0xFF);
            frame[2] = (byte)(id & 0xFF);
            frame[3] = (byte)Math.Min(data.Length, 8);
            
            for (int i = 0; i < frame[3]; i++) 
            {
                frame[4 + i] = data[i];
            }

            byte crc = 0;
            for (int i = 0; i < 12; i++) crc ^= frame[i];
            frame[12] = crc;
            frame[13] = FRAME_FOOTER;

            return frame;
        }

        public void Dispose()
        {
            Disconnect();
            _serialPort?.Dispose();
        }
    }
}

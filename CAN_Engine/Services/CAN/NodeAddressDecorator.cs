using System;
using ATS.CAN.Engine.Models;
using ATS.CAN.Engine.Adapters;
using ATS.CAN.Engine.Services.Interfaces;
using ATS.CAN.Engine.Services.CAN;
using System.Linq;

namespace ATS.CAN.Engine.Services.CAN
{
    /// <summary>
    /// Decorator for ICANService that handles node-specific addressing and filtering
    /// for a shared CAN bus (multiple boards on a single physical port).
    /// </summary>
    public class NodeAddressDecorator : ICANService
    {
        private readonly ICANService _inner;
        private readonly byte _nodeId;

        public NodeAddressDecorator(ICANService inner, byte nodeId)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _nodeId = nodeId;

            // Subscribe to inner service events and filter them
            _inner.MessageReceived += OnInnerMessageReceived;
            _inner.RawDataReceived += OnInnerRawDataReceived;
            _inner.SystemStatusReceived += OnInnerSystemStatusReceived;
            _inner.FirmwareVersionReceived += OnInnerFirmwareVersionReceived;
            _inner.PerformanceMetricsReceived += OnInnerPerformanceMetricsReceived;
            _inner.DataTimeout += OnInnerDataTimeout;
            _inner.LmvStreamChanged += OnInnerLmvStreamChanged;
        }

        // --- ICANService Properties ---
        public bool IsConnected => _inner.IsConnected;
        public bool IsStreaming => _inner.IsStreaming;
        public AdcMode CurrentADCMode => _inner.CurrentADCMode;
        public long TxMessageCount => _inner.TxMessageCount;
        public long RxMessageCount => _inner.RxMessageCount;
        public DateTime LastRxTime => _inner.LastRxTime;
        public DateTime LastSystemStatusTime => _inner.LastSystemStatusTime;

        // --- ICANService Events ---
        public event Action<CANMessage>? MessageReceived;
        public event EventHandler<RawDataEventArgs>? RawDataReceived;
        public event EventHandler<SystemStatusEventArgs>? SystemStatusReceived;
        public event EventHandler<FirmwareVersionEventArgs>? FirmwareVersionReceived;
        public event EventHandler<PerformanceMetricsEventArgs>? PerformanceMetricsReceived;
        public event EventHandler<string>? DataTimeout;
        public event EventHandler<AxleType>? LmvStreamChanged;

        // --- Event Filtering Logic ---

        private void OnInnerMessageReceived(CANMessage m) => MessageReceived?.Invoke(m);

        private void OnInnerRawDataReceived(object? s, RawDataEventArgs e)
        {
            // Only fire if the BoardId in the data matches our NodeId
            if (e.BoardId == _nodeId || _nodeId == 0)
            {
                RawDataReceived?.Invoke(this, e);
            }
        }

        private void OnInnerSystemStatusReceived(object? s, SystemStatusEventArgs e)
        {
            // For system status, we might need a BoardId in the args eventually.
            // For now, if the protocol includes it, we filter here.
            SystemStatusReceived?.Invoke(this, e);
        }

        private void OnInnerFirmwareVersionReceived(object? s, FirmwareVersionEventArgs e)
        {
            if (e.BoardId == _nodeId || _nodeId == 0)
            {
                FirmwareVersionReceived?.Invoke(this, e);
            }
        }

        private void OnInnerPerformanceMetricsReceived(object? s, PerformanceMetricsEventArgs e) => PerformanceMetricsReceived?.Invoke(this, e);
        private void OnInnerDataTimeout(object? s, string m) => DataTimeout?.Invoke(this, m);
        private void OnInnerLmvStreamChanged(object? s, AxleType side) => LmvStreamChanged?.Invoke(this, side);

        // --- ICANService Methods with Addressing ---

        public bool Connect(CanAdapterConfig config, out string errorMessage) => _inner.Connect(config, out errorMessage);
        public void Disconnect() => _inner.Disconnect();

        public bool SendMessage(uint id, byte[] data, bool log = true)
        {
            // Prepend our NodeId to the payload
            byte[] addressedData = new byte[Math.Min(data.Length + 1, 8)];
            addressedData[0] = _nodeId;
            Array.Copy(data, 0, addressedData, 1, Math.Min(data.Length, 7));

            return _inner.SendMessage(id, addressedData, log);
        }

        public bool StartStream(TransmissionRate rate, uint startMsgId = CANMessageProcessor.CAN_MSG_ID_START_STREAM)
        {
            byte[] data = new byte[1] { (byte)rate };
            return SendMessage(startMsgId, data);
        }

        public bool StopAllStreams() => SendMessage(CANMessageProcessor.CAN_MSG_ID_STOP_ALL_STREAMS, new byte[0]);
        public bool SwitchToInternalADC() => SendMessage(CANMessageProcessor.CAN_MSG_ID_MODE_INTERNAL, new byte[0]);
        public bool SwitchToADS1115() => SendMessage(CANMessageProcessor.CAN_MSG_ID_MODE_ADS1115, new byte[0]);
        public bool SwitchSystemMode(SystemMode mode) => SendMessage(CANMessageProcessor.CAN_MSG_ID_SET_SYSTEM_MODE, new byte[] { (byte)mode });

        public bool SelectLmvStream(AxleType side) => _inner.SelectLmvStream(side);
        public void SetActiveAxle(AxleType side) => _inner.SetActiveAxle(side);
        public bool RequestSystemStatus(bool log = true) => SendMessage(CANMessageProcessor.CAN_MSG_ID_STATUS_REQUEST, new byte[0], log);
        public bool RequestFirmwareVersion() => SendMessage(CANMessageProcessor.CAN_MSG_ID_VERSION_REQUEST, new byte[0]);
        public void SetTimeout(TimeSpan timeout) => _inner.SetTimeout(timeout);

        public void Dispose()
        {
            _inner.MessageReceived -= OnInnerMessageReceived;
            _inner.RawDataReceived -= OnInnerRawDataReceived;
            _inner.SystemStatusReceived -= OnInnerSystemStatusReceived;
            _inner.FirmwareVersionReceived -= OnInnerFirmwareVersionReceived;
            _inner.PerformanceMetricsReceived -= OnInnerPerformanceMetricsReceived;
            _inner.DataTimeout -= OnInnerDataTimeout;
            _inner.LmvStreamChanged -= OnInnerLmvStreamChanged;
            // We don't dispose the inner service because it's shared!
        }
    }
}

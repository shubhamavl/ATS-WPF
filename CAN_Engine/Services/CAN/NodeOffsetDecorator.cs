using System;
using ATS.CAN.Engine.Models;
using ATS.CAN.Engine.Adapters;
using ATS.CAN.Engine.Services.Interfaces;
using ATS.CAN.Engine.Services.CAN;
using System.Linq;

namespace ATS.CAN.Engine.Services.CAN
{
    /// <summary>
    /// Decorator for ICANService that handles CAN ID shifting for shared bus operation.
    /// Node 1: Offset = 0
    /// Node 2: RX Offset (telemetry) = +0x10, TX Offset (commands) = +0x02
    /// </summary>
    public class NodeOffsetDecorator : ICANService
    {
        private readonly ICANService _inner;
        private readonly int _nodeOffset;
        private readonly CANEventDispatcher _eventDispatcher;

        public NodeOffsetDecorator(ICANService inner, int nodeId)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _eventDispatcher = new CANEventDispatcher();
            
            if (nodeId == 2)
            {
                _nodeOffset = 0x80;
            }
            else
            {
                _nodeOffset = 0;
            }

            // Wire up our dispatcher to our public events
            _eventDispatcher.RawDataReceived += (s, e) => RawDataReceived?.Invoke(this, e);
            _eventDispatcher.SystemStatusReceived += (s, e) => {
                LastSystemStatusTime = DateTime.Now;
                SystemStatusReceived?.Invoke(this, e);
            };
            _eventDispatcher.FirmwareVersionReceived += (s, e) => FirmwareVersionReceived?.Invoke(this, e);
            _eventDispatcher.PerformanceMetricsReceived += (s, e) => PerformanceMetricsReceived?.Invoke(this, e);
            _eventDispatcher.LmvStreamChanged += (s, e) => LmvStreamChanged?.Invoke(this, e);
            _eventDispatcher.DataTimeout += (s, e) => DataTimeout?.Invoke(this, e);

            // Subscribe to inner service's raw message stream
            _inner.MessageReceived += OnInnerMessageReceived;
            _inner.DataTimeout += (s, m) => DataTimeout?.Invoke(this, m);
        }

        public bool IsConnected => _inner.IsConnected;
        public bool IsStreaming => _inner.IsStreaming;
        public AdcMode CurrentADCMode => _eventDispatcher.CurrentADCMode;
        public long TxMessageCount => _inner.TxMessageCount;
        public long RxMessageCount => _inner.RxMessageCount;
        public DateTime LastRxTime => _inner.LastRxTime;
        public DateTime LastSystemStatusTime { get; private set; } = DateTime.MinValue;

        public event Action<CANMessage>? MessageReceived;
        public event EventHandler<RawDataEventArgs>? RawDataReceived;
        public event EventHandler<SystemStatusEventArgs>? SystemStatusReceived;
        public event EventHandler<FirmwareVersionEventArgs>? FirmwareVersionReceived;
        public event EventHandler<PerformanceMetricsEventArgs>? PerformanceMetricsReceived;
        public event EventHandler<string>? DataTimeout;
        public event EventHandler<AxleType>? LmvStreamChanged;

        // --- Incoming Message Decoration (Interception & Dispatching) ---

        private void OnInnerMessageReceived(CANMessage m)
        {
            // Only process messages that match our offset configuration
            // Note: If nodeId is 1, offset is 0, so it matches base IDs.
            // If nodeId is 2, offset is 0x10, so it matches shifted IDs.
            
            uint baseId = (m.ID >= (uint)_nodeOffset) ? (m.ID - (uint)_nodeOffset) : m.ID;
            
            // Re-check if this IS a message destined for this virtual node
            // (e.g. if we are Node 2, we ONLY care about 0x210, 0x310, etc.)
            bool match = false;
            if (_nodeOffset == 0)
            {
                // Node 1: Matches base IDs exactly
                match = m.ID == CANMessageProcessor.CAN_MSG_ID_TOTAL_RAW_DATA ||
                        m.ID == CANMessageProcessor.CAN_MSG_ID_SYSTEM_STATUS ||
                        m.ID == CANMessageProcessor.CAN_MSG_ID_SYS_PERF ||
                        m.ID == CANMessageProcessor.CAN_MSG_ID_VERSION_RESPONSE ||
                        m.ID == CANMessageProcessor.CAN_MSG_ID_LMV_STREAM_CONFIRM;
            }
            else
            {
                // Node 2: Matches shifted IDs
                match = m.ID == (CANMessageProcessor.CAN_MSG_ID_TOTAL_RAW_DATA + _nodeOffset) ||
                        m.ID == (CANMessageProcessor.CAN_MSG_ID_SYSTEM_STATUS + _nodeOffset) ||
                        m.ID == (CANMessageProcessor.CAN_MSG_ID_SYS_PERF + _nodeOffset) ||
                        m.ID == (CANMessageProcessor.CAN_MSG_ID_VERSION_RESPONSE + _nodeOffset) ||
                        m.ID == (CANMessageProcessor.CAN_MSG_ID_LMV_STREAM_CONFIRM + _nodeOffset);
            }

            if (match)
            {
                // Normalize for internal application use
                var normalizedMessage = new CANMessage(baseId, m.Data);
                MessageReceived?.Invoke(normalizedMessage);
                
                // Dispatch specific events (Weight, Status, etc) using the normalized ID
                if (m.Data != null)
                {
                    _eventDispatcher.FireSpecificEvents(baseId, m.Data);
                }
            }
        }

        // --- Outgoing Message Decoration (Addition) ---

        public bool SendMessage(uint id, byte[] data, bool log = true)
        {
            // Add offset to command IDs for this specific board
            return _inner.SendMessage(id + (uint)_nodeOffset, data, log);
        }

        public bool StartStream(TransmissionRate rate, uint startMsgId = CANMessageProcessor.CAN_MSG_ID_START_STREAM)
        {
            return SendMessage(startMsgId, new byte[] { (byte)rate });
        }

        public bool StopAllStreams() => SendMessage(CANMessageProcessor.CAN_MSG_ID_STOP_ALL_STREAMS, new byte[0]);
        public bool SwitchToInternalADC() => SendMessage(CANMessageProcessor.CAN_MSG_ID_MODE_INTERNAL, new byte[0]);
        public bool SwitchToADS1115() => SendMessage(CANMessageProcessor.CAN_MSG_ID_MODE_ADS1115, new byte[0]);
        public bool SwitchSystemMode(SystemMode mode) => SendMessage(CANMessageProcessor.CAN_MSG_ID_SET_SYSTEM_MODE, new byte[] { (byte)mode });

        public bool Connect(CanAdapterConfig config, out string errorMessage) => _inner.Connect(config, out errorMessage);
        public void Disconnect() => _inner.Disconnect();
        public bool SelectLmvStream(AxleType side) => _inner.SelectLmvStream(side);
        public void SetActiveAxle(AxleType side) => _inner.SetActiveAxle(side);
        public bool RequestSystemStatus(bool log = true) => SendMessage(CANMessageProcessor.CAN_MSG_ID_STATUS_REQUEST, new byte[0], log);
        public bool RequestFirmwareVersion() => SendMessage(CANMessageProcessor.CAN_MSG_ID_VERSION_REQUEST, new byte[0]);
        public void SetTimeout(TimeSpan timeout) => _inner.SetTimeout(timeout);

        public void Dispose()
        {
            _inner.MessageReceived -= OnInnerMessageReceived;
            // Note: Event dispatcher handles its own subscriptions (local)
        }
    }
}

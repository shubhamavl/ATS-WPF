using System;
using ATS.CAN.Engine.Models;
using ATS.CAN.Engine.Adapters;
using ATS.CAN.Engine.Core;
using ATS.CAN.Engine.Services.Interfaces;

namespace ATS.CAN.Engine.Services.CAN
{
    /// <summary>
    /// A dynamic proxy for ICANService that automatically delegates all calls
    /// and events to the "Active" node in the SystemManager.
    /// This allows background services to remain agnostic of which physical port is being used.
    /// </summary>
    public class ManagedCanProxy : ICANService
    {
        private readonly ISystemManager _systemManager;
        private ICANService? _currentService;

        public ManagedCanProxy(ISystemManager systemManager)
        {
            _systemManager = systemManager;
            _systemManager.ActiveNodeChanged += OnActiveNodeChanged;
            _systemManager.NodesInitialized += OnNodesInitialized;
            RefreshSubscriptions();
        }

        private void OnActiveNodeChanged(object? sender, EventArgs e) => RefreshSubscriptions();
        private void OnNodesInitialized(object? sender, EventArgs e) => RefreshSubscriptions();

        private void RefreshSubscriptions()
        {
            var newService = _systemManager.ActiveNodeService;
            if (ReferenceEquals(_currentService, newService)) return;

            // Unsubscribe from old
            if (_currentService != null)
            {
                _currentService.MessageReceived -= ForwardMessageReceived;
                _currentService.RawDataReceived -= ForwardRawDataReceived;
                _currentService.SystemStatusReceived -= ForwardSystemStatusReceived;
                _currentService.FirmwareVersionReceived -= ForwardFirmwareVersionReceived;
                _currentService.PerformanceMetricsReceived -= ForwardPerformanceMetricsReceived;
                _currentService.DataTimeout -= ForwardDataTimeout;
                _currentService.LmvStreamChanged -= ForwardLmvStreamChanged;
            }

            _currentService = newService;

            // Subscribe to new
            if (_currentService != null)
            {
                _currentService.MessageReceived += ForwardMessageReceived;
                _currentService.RawDataReceived += ForwardRawDataReceived;
                _currentService.SystemStatusReceived += ForwardSystemStatusReceived;
                _currentService.FirmwareVersionReceived += ForwardFirmwareVersionReceived;
                _currentService.PerformanceMetricsReceived += ForwardPerformanceMetricsReceived;
                _currentService.DataTimeout += ForwardDataTimeout;
                _currentService.LmvStreamChanged += ForwardLmvStreamChanged;
                
                // Note: ManagedCanProxy is a bridge, logging logic removed or moved to DI logger if needed
            }
        }

        private void ForwardMessageReceived(CANMessage m) => MessageReceived?.Invoke(m);
        private void ForwardRawDataReceived(object? s, RawDataEventArgs e) => RawDataReceived?.Invoke(this, e);
        private void ForwardSystemStatusReceived(object? s, SystemStatusEventArgs e) => SystemStatusReceived?.Invoke(this, e);
        private void ForwardFirmwareVersionReceived(object? s, FirmwareVersionEventArgs e) => FirmwareVersionReceived?.Invoke(this, e);
        private void ForwardPerformanceMetricsReceived(object? s, PerformanceMetricsEventArgs e) => PerformanceMetricsReceived?.Invoke(this, e);
        private void ForwardDataTimeout(object? s, string m) => DataTimeout?.Invoke(this, m);
        private void ForwardLmvStreamChanged(object? s, AxleType side) => LmvStreamChanged?.Invoke(this, side);

        // Properties
        public bool IsConnected => _currentService?.IsConnected ?? false;
        public bool IsStreaming => _currentService?.IsStreaming ?? false;
        public AdcMode CurrentADCMode => _currentService?.CurrentADCMode ?? AdcMode.InternalWeight;
        public long TxMessageCount => _currentService?.TxMessageCount ?? 0;
        public long RxMessageCount => _currentService?.RxMessageCount ?? 0;
        public DateTime LastRxTime => _currentService?.LastRxTime ?? DateTime.MinValue;
        public DateTime LastSystemStatusTime => _currentService?.LastSystemStatusTime ?? DateTime.MinValue;

        // Events
        public event Action<CANMessage>? MessageReceived;
        public event EventHandler<RawDataEventArgs>? RawDataReceived;
        public event EventHandler<SystemStatusEventArgs>? SystemStatusReceived;
        public event EventHandler<FirmwareVersionEventArgs>? FirmwareVersionReceived;
        public event EventHandler<PerformanceMetricsEventArgs>? PerformanceMetricsReceived;
        public event EventHandler<string>? DataTimeout;
        public event EventHandler<AxleType>? LmvStreamChanged;

        // Methods
        public bool Connect(CanAdapterConfig config, out string errorMessage)
        {
            if (_currentService == null)
            {
                errorMessage = "No active node available";
                return false;
            }
            return _currentService.Connect(config, out errorMessage);
        }

        public void Disconnect() => _currentService?.Disconnect();
        
        public bool SendMessage(uint id, byte[] data, bool log = true) 
        {
            if (_currentService == null) return false;
            return _currentService.SendMessage(id, data, log);
        }

        public bool StartStream(TransmissionRate rate, uint startMsgId = 0x040) => _currentService?.StartStream(rate, startMsgId) ?? false;
        public bool StopAllStreams() => _currentService?.StopAllStreams() ?? false;
        public bool SwitchToInternalADC() => _currentService?.SwitchToInternalADC() ?? false;
        public bool SwitchToADS1115() => _currentService?.SwitchToADS1115() ?? false;
        public bool SwitchSystemMode(SystemMode mode) => _currentService?.SwitchSystemMode(mode) ?? false;
        public bool SelectLmvStream(AxleType side) => _currentService?.SelectLmvStream(side) ?? false;
        
        public bool RequestSystemStatus(bool log = true) 
        {
            if (_currentService == null) return false;
            return _currentService.RequestSystemStatus(log);
        }

        public bool RequestFirmwareVersion() => _currentService?.RequestFirmwareVersion() ?? false;
        public void SetTimeout(TimeSpan timeout) => _currentService?.SetTimeout(timeout);

        public void Dispose()
        {
            _systemManager.ActiveNodeChanged -= OnActiveNodeChanged;
            _systemManager.NodesInitialized -= OnNodesInitialized;
            
            if (_currentService != null)
            {
                _currentService.MessageReceived -= ForwardMessageReceived;
                _currentService.RawDataReceived -= ForwardRawDataReceived;
                _currentService.SystemStatusReceived -= ForwardSystemStatusReceived;
                _currentService.FirmwareVersionReceived -= ForwardFirmwareVersionReceived;
                _currentService.PerformanceMetricsReceived -= ForwardPerformanceMetricsReceived;
                _currentService.DataTimeout -= ForwardDataTimeout;
                _currentService.LmvStreamChanged -= ForwardLmvStreamChanged;
            }
        }
    }
}

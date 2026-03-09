using System;
using ATS_WPF.Models;
using ATS_WPF.Adapters;

namespace ATS_WPF.Services.Interfaces
{
    public interface ICANService : IDisposable
    {
        bool IsConnected { get; }
        bool IsStreaming { get; }
        AdcMode CurrentADCMode { get; }
        long TxMessageCount { get; }
        long RxMessageCount { get; }
        DateTime LastRxTime { get; }
        DateTime LastSystemStatusTime { get; }
        event Action<CANMessage>? MessageReceived;
        event EventHandler<RawDataEventArgs>? RawDataReceived;
        event EventHandler<SystemStatusEventArgs>? SystemStatusReceived;
        event EventHandler<FirmwareVersionEventArgs>? FirmwareVersionReceived;
        event EventHandler<PerformanceMetricsEventArgs>? PerformanceMetricsReceived;
        event EventHandler<string>? DataTimeout;

        bool Connect(CanAdapterConfig config, out string errorMessage);
        void Disconnect();
        bool SendMessage(uint id, byte[] data, bool log = true);
        bool StartStream(TransmissionRate rate, uint startMsgId = CANMessageProcessor.CAN_MSG_ID_START_STREAM);
        bool StopAllStreams();
        bool SwitchToInternalADC();
        bool SwitchToADS1115();
        bool SwitchSystemMode(SystemMode mode);
        bool RequestSystemStatus(bool log = true);
        bool RequestFirmwareVersion();
        void SetTimeout(TimeSpan timeout);
    }
}


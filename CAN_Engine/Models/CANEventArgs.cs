using System;
using ATS.CAN.Engine.Core;

namespace ATS.CAN.Engine.Models
{
    public class RawDataEventArgs : EventArgs
    {
        public string SideTag { get; set; } = string.Empty; 
        public uint CanId { get; set; }
        public int RawADCSum { get; set; }
        public DateTime TimestampFull { get; set; } // PC reception timestamp
    }

    public class SystemStatusEventArgs : EventArgs
    {
        public SystemStatus SystemStatus { get; set; }      // 0=OK, 1=Warning, 2=Error
        public byte ErrorFlags { get; set; }        // Error flags
        public AdcMode ADCMode { get; set; }        // Current ADC mode
        public SystemMode RelayState { get; set; }        // Current relay state (0=Weight, 1=Brake)
        public uint UptimeSeconds { get; set; }     // System uptime in seconds
        public DateTime Timestamp { get; set; }     // PC3 reception timestamp
    }

    public class PerformanceMetricsEventArgs : EventArgs
    {
        public ushort CanTxHz { get; set; }        // CAN transmission frequency
        public ushort AdcSampleHz { get; set; }    // ADC sampling frequency
        public DateTime Timestamp { get; set; }     // PC3 reception timestamp
    }

    public class FirmwareVersionEventArgs : EventArgs
    {
        public byte Major { get; set; }              // Major version number
        public byte Minor { get; set; }              // Minor version number
        public byte Patch { get; set; }              // Patch version number
        public byte Build { get; set; }              // Build number
        public byte BoardId { get; set; }            // Board ID
        public DateTime Timestamp { get; set; }      // PC3 reception timestamp

        public string VersionString => $"{Major}.{Minor}.{Patch}";
        public string VersionStringFull => $"{Major}.{Minor}.{Patch}.{Build}";
    }

#if CAN_ENGINE_BOOTLOADER
    public class BootPingResponseEventArgs : EventArgs
    {
        public DateTime Timestamp { get; set; }
    }
#endif

#if CAN_ENGINE_BOOTLOADER
    public class BootBeginResponseEventArgs : EventArgs
    {
        public BootloaderStatus Status { get; set; }
        public DateTime Timestamp { get; set; }
    }
#endif

#if CAN_ENGINE_BOOTLOADER
    public class BootProgressEventArgs : EventArgs
    {
        public byte Percent { get; set; }
        public uint BytesReceived { get; set; }
        public DateTime Timestamp { get; set; }
    }
#endif

#if CAN_ENGINE_BOOTLOADER
    public class BootEndResponseEventArgs : EventArgs
    {
        public BootloaderStatus Status { get; set; }
        public DateTime Timestamp { get; set; }
    }
#endif

#if CAN_ENGINE_BOOTLOADER
    public class BootErrorEventArgs : EventArgs
    {
        public uint CanId { get; set; }
        public byte[]? RawData { get; set; }
        public DateTime Timestamp { get; set; }
        public string Message => RawData != null ? ATS.CAN.Engine.Core.BootloaderProtocol.ParseErrorMessage(CanId, RawData) : "Unknown Error";
    }
#endif

#if CAN_ENGINE_BOOTLOADER
    public class BootQueryResponseEventArgs : EventArgs
    {
        public bool Present { get; set; }
        public byte Major { get; set; }
        public byte Minor { get; set; }
        public byte Patch { get; set; }
        public byte ActiveBank { get; set; }
        public byte BankAValid { get; set; }
        public byte BankBValid { get; set; }
        public DateTime Timestamp { get; set; }
    }
#endif

    public class CANErrorEventArgs : EventArgs
    {
        public string? ErrorMessage { get; set; }
        public int ErrorCode { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

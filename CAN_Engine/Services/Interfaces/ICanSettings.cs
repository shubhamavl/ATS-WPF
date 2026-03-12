using ATS_WPF.Services;
using ATS_WPF.Models;
using ATS_WPF.Services.Interfaces;
using System;

namespace ATS.CAN.Engine.Services.Interfaces
{
    /// <summary>
    /// Minimal settings interface for the CAN Engine.
    /// Contains ONLY the settings the engine needs to operate.
    /// ATS-WPF implements this from appsettings.json.
    /// LMS implements this by providing values from its Database.
    /// </summary>
    public interface ICanSettings
    {
        /// <summary>COM port for single-node modes (TwoWheeler, LMV, 3W)</summary>
        string ComPort { get; }

        /// <summary>COM port for HMV Left node</summary>
        string LeftComPort { get; }

        /// <summary>COM port for HMV Right node</summary>
        string RightComPort { get; }

        /// <summary>CAN bus baud rate setting</summary>
        ushort CanBitrateKbps { get; }

        /// <summary>Data transmission rate (streaming frequency)</summary>
        byte TransmissionRateCode { get; }

        /// <summary>Timeout in seconds before firing DataTimeout event</summary>
        int DataTimeoutSeconds { get; }

        /// <summary>Fired when any setting changes at runtime</summary>
        event EventHandler? SettingsChanged;
    }
}

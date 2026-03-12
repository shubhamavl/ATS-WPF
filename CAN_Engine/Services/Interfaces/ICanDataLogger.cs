using System;
using ATS.CAN.Engine.Models;

namespace ATS.CAN.Engine.Services.Interfaces
{
    /// <summary>
    /// Abstraction for data logging (CSV, Database, etc.)
    /// </summary>
    public interface ICanDataLogger
    {
        bool IsLogging { get; }

        void LogDataPoint(
            string source,
            int rawValue,
            double calibratedValue,
            double taredValue,
            double tareOffset,
            double slope,
            double intercept,
            AdcMode adcMode);

        void UpdateSystemStatus(
            SystemStatus status,
            byte errorFlags,
            SystemMode relayState,
            double canHz,
            double adcHz,
            uint uptime,
            string firmware);
    }
}

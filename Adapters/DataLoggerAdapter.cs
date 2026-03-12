using ATS.CAN.Engine.Models;
using ATS.CAN.Engine.Services.Interfaces;
using ATS_WPF.Services.Interfaces;

namespace ATS_WPF.Adapters
{
    /// <summary>
    /// Adapts the ATS-WPF DataLogger to the shared ICanDataLogger interface.
    /// </summary>
    public class DataLoggerAdapter : ICanDataLogger
    {
        private readonly IDataLoggerService _logger;

        public DataLoggerAdapter(IDataLoggerService logger)
        {
            _logger = logger;
        }

        public bool IsLogging => _logger.IsLogging;

        public void LogDataPoint(string source, int rawValue, double calibratedValue, double taredValue, double tareOffset, double slope, double intercept, AdcMode adcMode)
        {
            _logger.LogDataPoint(source, rawValue, calibratedValue, taredValue, tareOffset, slope, intercept, adcMode);
        }

        public void UpdateSystemStatus(SystemStatus status, byte errorFlags, SystemMode relayState, double canHz, double adcHz, uint uptime, string firmware)
        {
            _logger.UpdateSystemStatus(status, errorFlags, relayState, (ushort)canHz, (ushort)adcHz, uptime, firmware);
        }
    }
}

using ATS.CAN.Engine.Services.Interfaces;
using ATS_WPF.Services;
using ATS_WPF.Services.Interfaces;

namespace ATS_WPF.Adapters
{
    /// <summary>
    /// Adapts the ATS-WPF ProductionLogger to the shared ICanLogger interface.
    /// This file stays in the ATS-WPF project, NOT the shared CAN_Engine.
    /// </summary>
    public class ProductionCanLogger : ICanLogger
    {
        private readonly IProductionLoggerService _logger;

        public ProductionCanLogger(IProductionLoggerService logger)
        {
            _logger = logger;
        }

        public void LogInfo(string message, string source = "")
        {
            _logger.LogInfo(message, source);
        }

        public void LogWarning(string message, string source = "")
        {
            _logger.LogWarning(message, source);
        }

        public void LogError(string message, string source = "")
        {
            _logger.LogError(message, source);
        }

        public void LogDebug(string message, string source = "")
        {
            // ProductionLogger doesn't have LogDebug, so we just use LogInfo or skip
            System.Diagnostics.Debug.WriteLine($"[DEBUG][{source}] {message}");
        }
    }
}

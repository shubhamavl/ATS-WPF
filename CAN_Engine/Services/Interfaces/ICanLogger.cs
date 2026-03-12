namespace ATS.CAN.Engine.Services.Interfaces
{
    /// <summary>
    /// Universal logging interface for the CAN Engine.
    /// ATS-WPF implements this via ProductionLogger.
    /// LMS implements this via Serilog.
    /// </summary>
    public interface ICanLogger
    {
        void LogInfo(string message, string source = "");
        void LogWarning(string message, string source = "");
        void LogError(string message, string source = "");
        void LogDebug(string message, string source = "");
    }
}

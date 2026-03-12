using System;

namespace ATS.CAN.Engine.Services.Interfaces
{
    /// <summary>
    /// Default no-op logger. Used when no logger is injected.
    /// Writes to System.Diagnostics.Debug so it's visible in VS Output window
    /// but does nothing in production.
    /// </summary>
    public class DefaultCanLogger : ICanLogger
    {
        public static readonly DefaultCanLogger Instance = new();

        public void LogInfo(string message, string source = "")
        {
            System.Diagnostics.Debug.WriteLine($"[CAN-INFO] [{source}] {message}");
        }

        public void LogWarning(string message, string source = "")
        {
            System.Diagnostics.Debug.WriteLine($"[CAN-WARN] [{source}] {message}");
        }

        public void LogError(string message, string source = "")
        {
            System.Diagnostics.Debug.WriteLine($"[CAN-ERROR] [{source}] {message}");
        }

        public void LogDebug(string message, string source = "")
        {
            System.Diagnostics.Debug.WriteLine($"[CAN-DEBUG] [{source}] {message}");
        }
    }
}

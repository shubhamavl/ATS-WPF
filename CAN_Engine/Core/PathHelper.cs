using ATS.CAN.Engine.Models;
using System;
using System.IO;

namespace ATS.CAN.Engine.Core
{
    /// <summary>
    /// Helper class for portable file paths - all paths relative to executable directory
    /// </summary>
    public static class PathHelper
    {

        /// <summary>
        /// Gets the directory where the executable is located
        /// </summary>
        public static string ApplicationDirectory
        {
            get
            {
                return @"C:\ProgramData\ATS.Shared.CAN";
            }
        }

        /// <summary>
        /// Gets the path to the application data directory (portable, next to executable)
        /// </summary>
        public static string GetDataDirectory()
        {
            string dataDir = Path.Combine(ApplicationDirectory, "Data");
            if (!Directory.Exists(dataDir))
            {
                try
                {
                    Directory.CreateDirectory(dataDir);
                }
                catch { }
            }
            return dataDir;
        }

        /// <summary>
        /// Gets the path to the logs directory (portable, next to executable)
        /// </summary>
        public static string GetLogsDirectory()
        {
            string logsDir = Path.Combine(ApplicationDirectory, "Logs");
            if (!Directory.Exists(logsDir))
            {
                try
                {
                    Directory.CreateDirectory(logsDir);
                }
                catch { }
            }
            return logsDir;
        }

        /// <summary>
        /// Gets the path to the settings file (portable, next to executable)
        /// </summary>
        public static string GetSettingsPath()
        {
            return Path.Combine(ApplicationDirectory, "settings.json");
        }

        /// <summary>
        /// Gets the path to the update working directory (portable, next to executable)
        /// </summary>
        public static string GetUpdateDirectory()
        {
            string updateDir = Path.Combine(ApplicationDirectory, "Update");
            if (!Directory.Exists(updateDir))
            {
                try
                {
                    Directory.CreateDirectory(updateDir);
                }
                catch
                {
                    // Ignore directory creation failures here; caller will handle IO errors.
                }
            }
            return updateDir;
        }

        /// <summary>
        /// Gets the expected path to the external updater executable that performs file replacement.
        /// </summary>
        public static string GetUpdaterExecutablePath()
        {
            return Path.Combine(ApplicationDirectory, "ATS_Updater.exe");
        }

        /// <summary>
        /// Gets the path to a calibration file (portable, in Data directory)
        /// </summary>
        /// <param name="adcMode">ADC mode enum</param>
        /// <param name="systemMode">System mode enum</param>
        /// <param name="axleType">The type of the axle (e.g. Total, Left, Right)</param>
        public static string GetCalibrationPath(VehicleMode vehicleMode, Models.AxleType axleType, Models.AdcMode adcMode, Models.SystemMode systemMode = Models.SystemMode.Weight)
        {
            string modeStr = vehicleMode.ToString().ToLower();
            string modeSuffix = adcMode == Models.AdcMode.InternalWeight ? "internal" : "ads1115";
            string typeSuffix = systemMode == Models.SystemMode.Brake ? "_brake" : "";
            return Path.Combine(GetDataDirectory(), $"calibration_{modeStr}_{axleType.ToString().ToLower()}_{modeSuffix}{typeSuffix}.json");
        }

        /// <summary>
        /// Gets the path to the tare configuration file (portable, in Data directory)
        /// </summary>
        public static string GetTareConfigPath(VehicleMode vehicleMode, Models.AxleType axleType)
        {
            string modeStr = vehicleMode.ToString().ToLower();
            return Path.Combine(GetDataDirectory(), $"tare_{modeStr}_{axleType.ToString().ToLower()}_config.json");
        }

        /// <summary>
        /// Gets a path relative to the application directory
        /// </summary>
        public static string GetApplicationPath(string relativePath)
        {
            return Path.Combine(ApplicationDirectory, relativePath);
        }
    }
}



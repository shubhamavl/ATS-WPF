using System;
using System.IO;
using System.Text.Json;
using ATS.CAN.Engine.Core;
using ATS.CAN.Engine.Models;

namespace ATS.CAN.Engine.Services
{
    /// <summary>
    /// Tare manager for calibrator weight offset compensation (ATS Two-Wheeler).
    /// When calibrator weight is removed after calibration, the scale shows negative weight.
    /// Tare stores this negative offset and adds it to all future readings to compensate.
    /// Supports mode-specific offsets: Internal/ADS1115 (2 independent offsets)
    /// </summary>
    public class TareManager
    {
        private readonly Dictionary<AdcMode, TareEntry> _tareEntries = new();
        
        private readonly AxleType _type;

        public record TareEntry(double OffsetKg, bool IsTared, DateTime TareTime);

        public TareManager(AxleType type)
        {
            _type = type;
            // Initialize with empty entries
            _tareEntries[AdcMode.InternalWeight] = new TareEntry(0, false, DateTime.MinValue);
            _tareEntries[AdcMode.Ads1115] = new TareEntry(0, false, DateTime.MinValue);
        }

        /// <summary>
        /// Tare total weight: store positive offset from calibrator removal for compensation
        /// When calibrator is removed, weight goes negative (e.g., -23 kg).
        /// Tare stores the absolute value (e.g., +23 kg) to compensate all future readings.
        /// </summary>
        /// <param name="currentCalibratedKg">Current calibrated weight in kg (must be negative)</param>
        /// <param name="adcMode">ADC mode enum</param>
        public void TareTotal(double currentCalibratedKg, AdcMode adcMode)
        {
            // Validate offset value
            if (double.IsNaN(currentCalibratedKg) || double.IsInfinity(currentCalibratedKg))
            {
                throw new ArgumentException($"Invalid tare offset: {currentCalibratedKg} (NaN or Infinity)", nameof(currentCalibratedKg));
            }

            _tareEntries[adcMode] = new TareEntry(currentCalibratedKg, true, DateTime.Now);
            System.Diagnostics.Debug.WriteLine($"[TareManager] Total {adcMode} tare set: offset={currentCalibratedKg:F3} kg");
        }

        /// <summary>
        /// Reset total tare for specific ADC mode
        /// </summary>
        /// <param name="adcMode">ADC mode (0=Internal, 1=ADS1115)</param>
        public void ResetTotal(AdcMode adcMode)
        {
            _tareEntries[adcMode] = new TareEntry(0, false, DateTime.MinValue);
        }

        /// <summary>
        /// Reset all tares (all modes)
        /// </summary>
        public void ResetAll()
        {
            ResetTotal(AdcMode.InternalWeight);
            ResetTotal(AdcMode.Ads1115);
        }

        /// <summary>
        /// Apply tare: add positive offset to current calibrated weight to compensate for calibrator removal
        /// Example: If offset is +23 kg and calibrated weight is -23 kg, result = 0 kg
        /// If calibrated weight is -13 kg (added 10 kg), result = -13 + 23 = 10 kg
        /// </summary>
        /// <param name="calibratedKg">Current calibrated weight in kg</param>
        /// <param name="adcMode">ADC mode (0=Internal, 1=ADS1115)</param>
        /// <returns>Compensated weight (calibrated + offset, where offset is positive)</returns>
        public double ApplyTare(double calibratedKg, AdcMode adcMode)
        {
            if (_tareEntries.TryGetValue(adcMode, out var entry) && entry.IsTared)
            {
                double compensatedWeight = calibratedKg + entry.OffsetKg;
                System.Diagnostics.Debug.WriteLine($"[TareManager] ApplyTare: mode={adcMode}, calibrated={calibratedKg:F3} kg, offset={entry.OffsetKg:F3} kg, result={compensatedWeight:F3} kg");
                return compensatedWeight;
            }

            return calibratedKg;
        }

        /// <summary>
        /// Check if total weight and ADC mode is tared
        /// </summary>
        /// <param name="adcMode">ADC mode (0=Internal, 1=ADS1115)</param>
        /// <returns>True if tared</returns>
        public bool IsTared(AdcMode adcMode)
        {
            return _tareEntries.TryGetValue(adcMode, out var entry) && entry.IsTared;
        }

        /// <summary>
        /// Get offset weight for ADC mode
        /// </summary>
        /// <param name="adcMode">ADC mode (0=Internal, 1=ADS1115)</param>
        /// <returns>Offset weight in kg (positive value, stored as absolute of negative weight)</returns>
        public double GetOffsetKg(AdcMode adcMode)
        {
            return _tareEntries.TryGetValue(adcMode, out var entry) ? entry.OffsetKg : 0;
        }

        /// <summary>
        /// Get tare time for ADC mode
        /// </summary>
        /// <param name="adcMode">ADC mode (0=Internal, 1=ADS1115)</param>
        /// <returns>Tare time or DateTime.MinValue if not tared</returns>
        public DateTime GetTareTime(AdcMode adcMode)
        {
            return _tareEntries.TryGetValue(adcMode, out var entry) ? entry.TareTime : DateTime.MinValue;
        }

        /// <summary>
        /// Get tare status text for display
        /// </summary>
        /// <param name="adcMode">ADC mode (0=Internal, 1=ADS1115)</param>
        /// <returns>Status text</returns>
        public string GetTareStatusText(AdcMode adcMode)
        {
            bool isTared = IsTared(adcMode);
            if (isTared)
            {
                double offset = GetOffsetKg(adcMode);
                return $"✓ Tared (offset: {offset:F0}kg)";
            }
            else
            {
                return "- Not Tared";
            }
        }

        /// <summary>
        /// Get tare time for display
        /// </summary>
        /// <param name="adcMode">ADC mode (0=Internal, 1=ADS1115)</param>
        /// <returns>Tare time or empty string if not tared</returns>
        public string GetTareTimeText(AdcMode adcMode)
        {
            if (IsTared(adcMode))
            {
                DateTime tareTime = GetTareTime(adcMode);
                return tareTime.ToString("HH:mm:ss");
            }
            else
            {
                return "";
            }
        }

        /// <summary>
        /// Save tare state to JSON file
        /// </summary>
        public void SaveToFile(VehicleMode vehicleMode)
        {
            var tareData = new TareData
            {
                Entries = new Dictionary<AdcMode, TareEntry>(_tareEntries),
                SaveTime = DateTime.Now
            };

            string path = PathHelper.GetTareConfigPath(vehicleMode, _type); // Portable: in Data directory
            string jsonString = JsonSerializer.Serialize(tareData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, jsonString);
        }

        /// <summary>
        /// Load tare state from JSON file
        /// </summary>
        /// <returns>True if loaded successfully</returns>
        public bool LoadFromFile(VehicleMode vehicleMode)
        {
            string path = PathHelper.GetTareConfigPath(vehicleMode, _type); // Portable: in Data directory
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                string jsonString = File.ReadAllText(path);
                var tareData = JsonSerializer.Deserialize<TareData>(jsonString);

                if (tareData?.Entries != null)
                {
                    foreach (var entry in tareData.Entries)
                    {
                        _tareEntries[entry.Key] = entry.Value;
                    }

                    return true;
                }
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing tare config: {ex.Message}");
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading tare config: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Delete tare configuration file
        /// </summary>
        public void DeleteConfig(VehicleMode vehicleMode)
        {
            string path = PathHelper.GetTareConfigPath(vehicleMode, _type); // Portable: in Data directory
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deleting tare config (IO): {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Access denied deleting tare config: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Data structure for tare configuration persistence (offset-based)
    /// </summary>
    public class TareData
    {
        public Dictionary<AdcMode, TareManager.TareEntry>? Entries { get; set; }
        public DateTime SaveTime { get; set; }
    }
}


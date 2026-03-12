using System;
using ATS_WPF.Models;
using ATS.CAN.Engine.Models;
using ATS_WPF.Core;
using ATS.CAN.Engine.Core;

namespace ATS_WPF.Services.Interfaces
{
    public interface ISettingsService
    {
        AppSettings Settings { get; }
        event EventHandler? SettingsChanged;

        void LoadSettings();
        void SaveSettings();

        void SetComPort(string portName);
        void SetCanBaudRate(string baudRate);
        void SetTransmissionRate(string samplingRate);
        void SetSaveDirectory(string path);

        void UpdateSystemStatus(AdcMode adcMode, SystemStatus systemStatus, byte errorFlags);
        AdcMode GetLastKnownADCMode();

        void SetFilterSettings(string type, double alpha, int windowSize, bool enabled);
        void SetDisplaySettings(int weightDecimals, int uiUpdateRate, int dataTimeoutSeconds);
        void SetUIVisibilitySettings(int bannerDuration, int messageLimit, bool showRawADC, bool showCalibrated, bool showStreaming, bool showCalibrationIcons);
        void SetAdvancedSettings(int txFlashMs, string logFormat, int batchSize, int clockInterval, int calibrationDelay, bool showQualityMetrics);
        void SetBootloaderFeaturesEnabled(bool enabled);
        void SetCalibrationMode(string mode);
        void SetCalibrationAveragingSettings(bool enabled, int sampleCount, int durationMs, bool useMedian, bool removeOutliers, double outlierThreshold, double maxStdDev);
        void SetBrakeSettings(string unit, double multiplier);

        // Calibration and Tare accessors
        // Calibration and Tare accessors
        LinearCalibration GetCalibrationDataInternal(AxleType axleType);
        LinearCalibration GetCalibrationDataADS1115(AxleType axleType);
        LinearCalibration GetCalibrationDataInternalBrake(AxleType axleType);
        LinearCalibration GetCalibrationDataADS1115Brake(AxleType axleType);
        string GetCalibrationFilePath(AxleType axleType, bool adcMode);
        string GetCalibrationBrakeFilePath(AxleType axleType, bool adcMode);
        string GetTareFilePath(AxleType axleType);
        void ResetCalibration(AxleType axleType, bool adsMode);
        void ResetBrakeCalibration(AxleType axleType, bool adsMode);
        double TareValue { get; }
    }
}


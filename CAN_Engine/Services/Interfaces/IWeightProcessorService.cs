using System;
using ATS.CAN.Engine.Models;
using ATS.CAN.Engine.Core;
using ATS.CAN.Engine.Services;

namespace ATS.CAN.Engine.Services.Interfaces
{
    /// <summary>
    /// Interface for the weight processing service.
    /// This is now part of the shared engine.
    /// </summary>
    public interface IWeightProcessorService : IDisposable
    {
        ProcessedWeightData LatestTotal { get; }
        LinearCalibration? InternalCalibration { get; }
        LinearCalibration? Ads1115Calibration { get; }

        void Start();
        void Stop();
        void SetCalibration(LinearCalibration? calibration, AdcMode mode = AdcMode.InternalWeight);
        void SetADCMode(AdcMode mode);
        void SetBrakeMode(bool isBrakeMode);
        void LoadCalibration();
        void SetTareManager(TareManager tareManager);
        void ConfigureFilter(FilterType type, double alpha, int windowSize, bool enabled);
        void EnqueueRawData(int rawValue);
        void ResetFilters();
        void Tare();
        void ResetTare();

        bool IsActiveCalibrated { get; }
    }
}

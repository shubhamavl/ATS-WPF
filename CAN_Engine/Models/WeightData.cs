using System;

namespace ATS_WPF.Models
{
    /// <summary>
    /// Raw weight data from CAN (ATS Two-Wheeler)
    /// Supports both Internal ADC (unsigned 0-16380 for 4 channels) and ADS1115 (signed -131072 to +131068)
    /// </summary>
    /// <summary>
    /// Raw weight data from CAN (ATS Two-Wheeler)
    /// </summary>
    public record RawWeightData(int RawADC, DateTime Timestamp);

    /// <summary>
    /// Processed weight data with calibration and tare applied
    /// </summary>
    public record ProcessedWeightData(
        int RawADC, 
        double CalibratedWeight, 
        double TaredWeight, 
        double TareValue, 
        DateTime Timestamp);
}



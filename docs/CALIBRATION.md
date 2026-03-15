# Calibration and Measurement

## Overview
The ATS system employs high-precision calibration logic to convert raw ADC counts from load cells into physical weight units (kilograms or Newtons). The system follows the industry-standard **"Calibrate First, Then Tare"** workflow.

## Calibration Methods

### 1. Linear Regression (Global)
This is the default and recommended method for most sensors. It uses the **Least Squares** method to fit a single straight line through all calibration points.
- **Equation**: `y = mx + b`
- **m (Slope)**: The sensitivity of the sensor (kg/ADC).
- **b (Intercept)**: The electrical offset of the sensor at zero load.
- **Quality**: The system calculates the **R² (Coefficient of Determination)** to evaluate how well the line fits the data points.

### 2. Piecewise Linear Interpolation
Used for sensors that exhibit non-linear behavior over their full range.
- The system connects adjacent calibration points with individual line segments.
- It provides high accuracy for non-linear load cells but requires more calibration points across the expected weight range.

## The Measurement Pipeline
1.  **Raw Capture**: Get ADC value (e.g., 14,250).
2.  **Calibration**: Apply the linear model: `CalibratedWeight = (Slope * RawADC) + Intercept`.
3.  **Filtering**: Apply EMA or SMA filters to the calibrated weight to remove mechanical noise.
4.  **Tare**: Subtract the stored zero-offset: `FinalWeight = CalibratedWeight - StoredTare`.

## Tare Logic
Tare is used to "zero" the scale when an empty platform or a calibrator fixture is present.
- **Persistence**: Unlike many systems where tare is lost on restart, the ATS system saves the tare value to disk.
- **Calculation**: Clicking "Tare" stores the current *CalibratedWeight* as the new offset.

## Calibration File Format
Calibration data is stored in JSON files within the `Data/` folder. The filename structure is:
`calibration_{VehicleMode}_{AxleType}_{AdcMode}_{SystemMode}.json`

### Example File:
```json
{
  "Slope": 0.0245,
  "Intercept": -12.4,
  "CalibrationDate": "2026-03-09T14:30:00",
  "IsValid": true,
  "Mode": 0,
  "Points": [
    { "RawADC": 500, "KnownWeight": 0 },
    { "RawADC": 4580, "KnownWeight": 100 }
  ],
  "R2": 0.9999
}
```

## Multi-Sample Averaging
To ensure calibration accuracy, the system doesn't just take a single "snapshot" of the ADC. Instead:
1.  It collects a batch of samples (default: 50) over 2 seconds.
2.  It removes outliers (values outside 2 standard deviations).
3.  It calculates the **Median** or **Mean** of the remaining samples to use as the calibration point.

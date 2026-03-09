using System;
using System.IO;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ATS_WPF.Models;
using ATS_WPF.Core;
using ATS_WPF.Services.Interfaces;

namespace ATS_WPF.Services
{
    /// <summary>
    /// Filter type enumeration
    /// </summary>


    /// <summary>
    /// High-performance weight data processor for ATS Two-Wheeler
    /// Runs on dedicated thread to handle 1kHz data rate
    /// Processes single total weight (all 4 channels summed)
    /// </summary>
    public class WeightProcessor : IWeightProcessorService
    {
        private readonly AxleType _axleType;
        private readonly VehicleMode _vehicleMode;
        // Input queue: Raw ADC data from CAN thread
        private readonly ConcurrentQueue<RawWeightData> _rawDataQueue = new();

        // Output: Latest processed data (lock-free)
        private volatile ProcessedWeightData _latestTotal = new();

        // Calibration references (immutable after set)
        // Calibration references (immutable after set)
        private LinearCalibration? _internalCalibration;
        private LinearCalibration? _ads1115Calibration;
        private SettingsManager _settingsManager = SettingsManager.Instance;
        private bool _isBrakeMode = false;
        private TareManager? _tareManager;
        private readonly IDataLoggerService _dataLogger;

        // ADC mode tracking
        private AdcMode _totalADCMode = AdcMode.InternalWeight;

        // Thread control
        private Task? _processingTask;
        private CancellationTokenSource? _cancellationSource;
        private volatile bool _isRunning = false;

        // Performance tracking
        private long _processedCount = 0;
        private long _droppedCount = 0;

        // ===== WEIGHT FILTERING (Configurable) =====
        // Filter configuration
        private FilterType _filterType = FilterType.EMA;
        private double _filterAlpha = 0.15;  // EMA alpha (0.0-1.0)
        private int _filterWindowSize = 10;  // SMA window size
        private bool _filterEnabled = true;  // Enable/disable filtering

        // EMA filtered weight values
        private double _totalFilteredCalibrated = 0;
        private double _totalFilteredTared = 0;

        // Track if EMA filters are initialized (first sample)
        private bool _calibratedFilterInitialized = false;
        private bool _taredFilterInitialized = false;

        // SMA buffers
        private readonly Queue<double> _totalSmaCalibrated = new Queue<double>();
        private readonly Queue<double> _totalSmaTared = new Queue<double>();

        public ProcessedWeightData LatestTotal => _latestTotal;
        public LinearCalibration? InternalCalibration => _internalCalibration;
        public LinearCalibration? Ads1115Calibration => _ads1115Calibration;
        public long ProcessedCount => _processedCount;
        public long DroppedCount => _droppedCount;
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Single property to check if the currently active mode is calibrated
        /// </summary>
        public bool IsActiveCalibrated
        {
            get
            {
                 var calibration = _totalADCMode == AdcMode.InternalWeight ? _internalCalibration : _ads1115Calibration;
                 return calibration?.IsValid == true;
            }
        }

        public WeightProcessor(AxleType axleType, VehicleMode vehicleMode, ICANService canService, TareManager tareManager, IDataLoggerService dataLogger)
        {
            _axleType = axleType;
            _vehicleMode = vehicleMode;
            _tareManager = tareManager;
            _dataLogger = dataLogger;

            // Subscribe to raw data from CAN service
            canService.RawDataReceived += (s, e) => {
                if (IsStreamForThisAxle(e))
                {
                    EnqueueRawData(e.RawADCSum);
                }
            };

            // Subscribe to system status
            canService.SystemStatusReceived += (s, e) => {
                SetADCMode(e.ADCMode);
                SetBrakeMode(e.RelayState != 0);
                ResetFilters();
                
                // Update logger with system status (from whichever node we are on)
                _dataLogger.UpdateSystemStatus(e.SystemStatus, e.ErrorFlags, e.RelayState, 0, 0, e.UptimeSeconds, ""); // Hz updated separately
            };

            canService.PerformanceMetricsReceived += (s, e) => {
                _dataLogger.UpdateSystemStatus(SystemStatus.Ok, 0, SystemMode.Weight, e.CanTxHz, e.AdcSampleHz, 0, "");
            };

            canService.FirmwareVersionReceived += (s, e) => {
                _dataLogger.UpdateSystemStatus(SystemStatus.Ok, 0, SystemMode.Weight, 0, 0, 0, e.VersionString);
            };

            LoadCalibration();
            LoadFilterSettings();
            
            // Subscribe to settings changes
            _settingsManager.SettingsChanged += (s, e) => LoadFilterSettings();
        }

        private bool IsStreamForThisAxle(RawDataEventArgs e)
        {
            // If we are in TwoWheeler or HMV, all raw data coming over our dedicated CANNode belongs to us (because nodes map 1:1)
            if (_vehicleMode != VehicleMode.LMV) return true;

            // LMV shares one CANNode and switches between Left/Right streams.
            // But we actually need to look at if `e` has an stream identifier like CAN ID.
            // Currently RawDataEventArgs doesn't expose the original CAN ID. 
            // In the short-term, assume `IsStreamForThisAxle` works with the ID from CANService.
            // NOTE: We need to augment CANService to expose the origin CAN ID or side in RawDataEventArgs
            
            // To be implemented: Stream ID check based on e.CanId 
            // 0x200 -> Left, 0x201 -> Right
            return e.SideTag == _axleType.ToString() || string.IsNullOrEmpty(e.SideTag);
        }

        private void LoadFilterSettings()
        {
            var settings = _settingsManager.Settings;
            ConfigureFilter(settings.FilterType, settings.FilterAlpha, settings.FilterWindowSize, settings.FilterEnabled);
        }

        /// <summary>
        /// Start the processing thread
        /// </summary>
        public void Start()
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
            _cancellationSource = new CancellationTokenSource();
            _processingTask = Task.Run(async () => await ProcessingLoop(_cancellationSource.Token));

            ProductionLogger.Instance.LogInfo("WeightProcessor started", "WeightProcessor");
        }

        /// <summary>
        /// Stop the processing thread
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            _cancellationSource?.Cancel();
            _processingTask?.Wait(1000);

            ProductionLogger.Instance.LogInfo($"WeightProcessor stopped. Processed: {_processedCount}, Dropped: {_droppedCount}", "WeightProcessor");
        }

        /// <summary>
        /// Set calibration reference
        /// </summary>
        public void SetCalibration(LinearCalibration? calibration, AdcMode mode = AdcMode.InternalWeight)
        {
            if (mode == AdcMode.InternalWeight)
            {
                _internalCalibration = calibration;
            }
            else
            {
                _ads1115Calibration = calibration;
            }

            ProductionLogger.Instance.LogInfo($"Calibration set manually (Mode {mode}) - Valid: {calibration?.IsValid}", "WeightProcessor");
        }

        /// <summary>
        /// Set ADC mode (0=Internal, 1=ADS1115)
        /// </summary>
        public void SetADCMode(AdcMode adcMode)
        {
            _totalADCMode = adcMode;
        }

        public void SetBrakeMode(bool isBrakeMode)
        {
            if (_isBrakeMode != isBrakeMode)
            {
                _isBrakeMode = isBrakeMode;
                LoadCalibration();
            }
        }

        public void LoadCalibration()
        {
            try
            {
                // Load calibrations for both nodes using the axle name
                _internalCalibration = LinearCalibration.LoadFromFile(_axleType, AdcMode.InternalWeight, _isBrakeMode ? SystemMode.Brake : SystemMode.Weight);
                _ads1115Calibration = LinearCalibration.LoadFromFile(_axleType, AdcMode.Ads1115, _isBrakeMode ? SystemMode.Brake : SystemMode.Weight);

                string modeStr = _isBrakeMode ? "Brake" : "Weight";

                // Log calibration status
                if (_internalCalibration != null)
                {
                    ProductionLogger.Instance.LogInfo($"Internal {_axleType} calibration loaded ({modeStr}): {_internalCalibration.GetEquationString()}", "WeightProcessor");
                }

                if (_ads1115Calibration != null)
                {
                    ProductionLogger.Instance.LogInfo($"ADS1115 {_axleType} calibration loaded ({modeStr}): {_ads1115Calibration.GetEquationString()}", "WeightProcessor");
                }
            }
            catch (FileNotFoundException ex)
            {
                ProductionLogger.Instance.LogError($"Calibration file not found: {ex.Message}", "WeightProcessor");
            }
            catch (JsonException ex)
            {
                ProductionLogger.Instance.LogError($"Invalid calibration data: {ex.Message}", "WeightProcessor");
            }
            catch (IOException ex)
            {
                ProductionLogger.Instance.LogError($"Error reading calibration file: {ex.Message}", "WeightProcessor");
            }
        }

        /// <summary>
        /// Set tare manager reference
        /// </summary>
        public void SetTareManager(TareManager tareManager)
        {
            _tareManager = tareManager;
            ProductionLogger.Instance.LogInfo("TareManager set", "WeightProcessor");
        }

        /// <summary>
        /// Configure filter settings
        /// </summary>
        public void ConfigureFilter(FilterType type, double alpha, int windowSize, bool enabled)
        {
            _filterType = type;
            _filterAlpha = alpha;
            _filterWindowSize = windowSize;
            _filterEnabled = enabled;

            // Clear SMA buffers when settings change
            _totalSmaCalibrated.Clear();
            _totalSmaTared.Clear();

            // Reset EMA filters
            _calibratedFilterInitialized = false;
            _taredFilterInitialized = false;

            ProductionLogger.Instance.LogInfo($"Filter configured: Type={type}, Alpha={alpha}, Window={windowSize}, Enabled={enabled}", "WeightProcessor");
        }

        /// <summary>
        /// Enqueue raw ADC data for processing
        /// Supports both Internal ADC (unsigned 0-16380 for 4 channels) and ADS1115 (signed -131072 to +131068)
        /// </summary>
        public void EnqueueRawData(int rawADC)
        {
            const int MAX_QUEUE_SIZE = 100; // Prevent memory leak

            if (_rawDataQueue.Count > MAX_QUEUE_SIZE)
            {
                Interlocked.Increment(ref _droppedCount);
                return; // Drop oldest data
            }

            _rawDataQueue.Enqueue(new RawWeightData
            {
                // Side removed from model
                RawADC = rawADC,  // Can be signed (ADS1115) or unsigned (Internal)
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// Processing thread - runs continuously
        /// </summary>
        /// <summary>
        /// Processing thread - runs continuously
        /// </summary>
        private async Task ProcessingLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_rawDataQueue.TryDequeue(out var rawData))
                {
                    ProcessRawData(rawData);
                    Interlocked.Increment(ref _processedCount);
                }
                else
                {
                    try
                    {
                        // No data available - yield briefly
                        await Task.Delay(1, token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Core processing pipeline for raw ADC data.
        /// Steps:
        /// 1. Converts raw ADC to physical unit (kg) using active calibration (Linear Regression or Piecewise).
        /// 2. Applies configured filter (EMA/SMA) to the calibrated value.
        /// 3. Applies tare offset (if set) to calculate net weight.
        /// 4. Re-filters the tared weight for smooth display.
        /// 5. Updates the thread-safe LatestTotal property.
        /// </summary>
        /// <param name="raw">Raw weight data packet containing ADC value and timestamp</param>
        private void ProcessRawData(RawWeightData raw)
        {
            var processed = new ProcessedWeightData
            {
                RawADC = raw.RawADC,
                Timestamp = raw.Timestamp
            };

            // Apply calibration (fast floating-point math)
            // Select calibration based on current ADC mode
            var calibration = _totalADCMode == AdcMode.InternalWeight ? _internalCalibration : _ads1115Calibration;

            if (calibration?.IsValid == true)
            {
                double calibratedWeight = calibration.RawToKg(raw.RawADC);

                // Apply filtering if enabled
                if (_filterEnabled)
                {
                    processed.CalibratedWeight = ApplyFilter(calibratedWeight, true);
                }
                else
                {
                    processed.CalibratedWeight = calibratedWeight;
                }

                // Apply tare (mode-specific) - uses _totalADCMode which should match the calibration mode
                double tareValue = _tareManager?.GetOffsetKg(_totalADCMode) ?? 0;
                processed.TareValue = tareValue;
                double taredWeight = _tareManager?.ApplyTare(processed.CalibratedWeight, _totalADCMode) ?? processed.CalibratedWeight;

                // Apply filtering to tared weight if enabled
                if (_filterEnabled)
                {
                    processed.TaredWeight = ApplyFilter(taredWeight, false);
                }
                else
                {
                    processed.TaredWeight = taredWeight;
                }
            }

            _latestTotal = processed; // Atomic write

            // Log data point if logging is active
            if (_dataLogger.IsLogging)
            {
                _dataLogger.LogDataPoint(
                    _axleType.ToString(),
                    processed.RawADC,
                    processed.CalibratedWeight,
                    processed.TaredWeight,
                    processed.TareValue,
                    calibration?.Slope ?? 0,
                    calibration?.Intercept ?? 0,
                    _totalADCMode
                );
            }
        }

        /// <summary>
        /// Apply filter based on configured filter type
        /// </summary>
        internal double ApplyFilter(double value, bool isCalibrated)
        {
            switch (_filterType)
            {
                case FilterType.EMA:
                    return ApplyEMA(value, isCalibrated);
                case FilterType.SMA:
                    return ApplySMA(value, isCalibrated);
                case FilterType.None:
                default:
                    return value;
            }
        }

        /// <summary>
        /// Apply Exponential Moving Average filter
        /// </summary>
        private double ApplyEMA(double value, bool isCalibrated)
        {
            if (isCalibrated)
            {
                if (!_calibratedFilterInitialized)
                {
                    _totalFilteredCalibrated = value;
                    _calibratedFilterInitialized = true;
                    return value;
                }
                _totalFilteredCalibrated = _filterAlpha * value + (1 - _filterAlpha) * _totalFilteredCalibrated;
                return _totalFilteredCalibrated;
            }
            else
            {
                if (!_taredFilterInitialized)
                {
                    _totalFilteredTared = value;
                    _taredFilterInitialized = true;
                    return value;
                }
                _totalFilteredTared = _filterAlpha * value + (1 - _filterAlpha) * _totalFilteredTared;
                return _totalFilteredTared;
            }
        }

        /// <summary>
        /// Apply Simple Moving Average filter
        /// </summary>
        private double ApplySMA(double value, bool isCalibrated)
        {
            Queue<double> buffer = isCalibrated ? _totalSmaCalibrated : _totalSmaTared;

            buffer.Enqueue(value);
            if (buffer.Count > _filterWindowSize)
            {
                buffer.Dequeue();
            }

            // Return average of buffer
            return buffer.Count > 0 ? buffer.Average() : value;
        }

        /// <summary>
        /// Reset filters (call when tare changes or calibration changes)
        /// </summary>
        public void ResetFilters()
        {
            _calibratedFilterInitialized = false;
            _taredFilterInitialized = false;
            _totalFilteredCalibrated = 0;
            _totalFilteredTared = 0;

            // Clear SMA buffers
            _totalSmaCalibrated.Clear();
            _totalSmaTared.Clear();

            ProductionLogger.Instance.LogInfo("Weight filters reset", "WeightProcessor");
        }

        public void Tare()
        {
            if (_tareManager == null)
            {
                return;
            }

            var latest = _latestTotal;
            if (latest != null && latest.CalibratedWeight < 0)
            {
                _tareManager.TareTotal(latest.CalibratedWeight, _totalADCMode);
                ResetFilters();
            }
        }

        public void ResetTare()
        {
            if (_tareManager == null)
            {
                return;
            }

            _tareManager.ResetTotal(_totalADCMode);
            ResetFilters();
        }

        public void Dispose()
        {
            Stop();
            _cancellationSource?.Dispose();
        }
    }
}



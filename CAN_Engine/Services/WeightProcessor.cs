using System;
using System.IO;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ATS.CAN.Engine.Models;
using ATS.CAN.Engine.Services.Interfaces;
using ATS.CAN.Engine.Core;

namespace ATS.CAN.Engine.Services
{
    /// <summary>
    /// High-performance weight data processor for the shared CAN engine.
    /// Runs on dedicated thread to handle high data rates.
    /// Processes weight data using injected settings, logging, and data logging abstractions.
    /// </summary>
    public class WeightProcessor : IWeightProcessorService, IDisposable
    {
        private readonly AxleType _axleType;
        private readonly VehicleMode _vehicleMode;
        private AxleType _confirmedLmvSide = AxleType.Left; 
        
        private readonly ConcurrentQueue<RawWeightData> _rawDataQueue = new();
        private volatile ProcessedWeightData _latestTotal = new(0, 0, 0, 0, DateTime.MinValue);

        private LinearCalibration? _internalCalibration;
        private LinearCalibration? _ads1115Calibration;
        private readonly ICanSettings _settings;
        private readonly ICanLogger _logger;
        private readonly ICanDataLogger _dataLogger;
        
        private bool _isBrakeMode = false;
        private SystemMode _systemMode = SystemMode.Weight;
        private TareManager? _tareManager;

        private AdcMode _totalADCMode = AdcMode.InternalWeight;

        private Task? _processingTask;
        private CancellationTokenSource? _cancellationSource;
        private volatile bool _isRunning = false;

        private long _processedCount = 0;
        private long _droppedCount = 0;

        private FilterType _filterType = FilterType.EMA;
        private double _filterAlpha = 0.15;  
        private int _filterWindowSize = 10;  
        private bool _filterEnabled = true;  

        private double _totalFilteredCalibrated = 0;
        private double _totalFilteredTared = 0;
        private bool _calibratedFilterInitialized = false;
        private bool _taredFilterInitialized = false;

        private readonly Queue<double> _totalSmaCalibrated = new Queue<double>();
        private readonly Queue<double> _totalSmaTared = new Queue<double>();


        public ProcessedWeightData LatestTotal => _latestTotal;
        public LinearCalibration? InternalCalibration => _internalCalibration;
        public LinearCalibration? Ads1115Calibration => _ads1115Calibration;
        public long ProcessedCount => _processedCount;
        public long DroppedCount => _droppedCount;
        public bool IsRunning => _isRunning;

        public bool IsActiveCalibrated
        {
            get
            {
                // The provided code edit is syntactically incorrect and refers to undeclared variables 'cal' and 'adcMode'.
                // To maintain syntactic correctness and make a faithful edit, I will interpret the intent as
                // ensuring the active calibration is valid and its ADCMode matches the current _totalADCMode.
                // If the intent was to literally insert the broken snippet, it would result in a non-compiling file.
                // Given the instruction "Fix property casing in WeightProcessor and pass VehicleMode in CalibrationSettingsViewModel",
                // and the provided snippet, the most reasonable interpretation for this specific property is to refine its logic.
                var calibration = _totalADCMode == AdcMode.InternalWeight ? _internalCalibration : _ads1115Calibration;
                return calibration?.IsValid == true && calibration?.ADCMode == _totalADCMode;
            }
        }

        public WeightProcessor(
            AxleType axleType, 
            VehicleMode vehicleMode, 
            ICANService canService, 
            TareManager tareManager, 
            ICanSettings settings, 
            ICanDataLogger dataLogger,
            ICanLogger? logger = null)
        {
            _axleType = axleType;
            _vehicleMode = vehicleMode;
            _tareManager = tareManager;
            _settings = settings;
            _dataLogger = dataLogger;
            _logger = logger ?? DefaultCanLogger.Instance;
            
            canService.RawDataReceived += (s, e) => {
                if (IsStreamForThisAxle(e))
                {
                    EnqueueRawData(e.RawADCSum);
                }
            };

            canService.LmvStreamChanged += (s, side) => {
                _confirmedLmvSide = side;
            };

            canService.SystemStatusReceived += (s, e) => {
                SetADCMode(e.ADCMode);
                SetBrakeMode(e.RelayState != 0);
                ResetFilters();
                
                _dataLogger.UpdateSystemStatus(e.SystemStatus, e.ErrorFlags, (SystemMode)e.RelayState, 0, 0, e.UptimeSeconds, ""); 
            };

            canService.PerformanceMetricsReceived += (s, e) => {
                _dataLogger.UpdateSystemStatus(SystemStatus.Ok, 0, SystemMode.Weight, e.CanTxHz, e.AdcSampleHz, 0, "");
            };

            canService.FirmwareVersionReceived += (s, e) => {
                _dataLogger.UpdateSystemStatus(SystemStatus.Ok, 0, SystemMode.Weight, 0, 0, 0, e.VersionString);
            };

            LoadCalibration();
            
            // Manual loading of filter settings instead of direct host model access
            // In a real scenario, these could be added to ICanSettings if they are common.
            // For now, we'll use defaults or add them to ICanSettings later.
            
            _settings.SettingsChanged += (s, e) => LoadCalibration();
        }

        private bool IsStreamForThisAxle(RawDataEventArgs e)
        {
            if (_vehicleMode != VehicleMode.LMV) return true;
            if (e.CanId != CANMessageProcessor.CAN_MSG_ID_TOTAL_RAW_DATA) return false;
            return _axleType == _confirmedLmvSide;
        }

        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _cancellationSource = new CancellationTokenSource();
            _processingTask = Task.Run(async () => await ProcessingLoop(_cancellationSource.Token));

            _logger.LogInfo($"{_axleType} WeightProcessor started", "WeightProcessor");
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _cancellationSource?.Cancel();
            _processingTask?.Wait(1000);

            _logger.LogInfo($"{_axleType} WeightProcessor stopped. Processed: {_processedCount}, Dropped: {_droppedCount}", "WeightProcessor");
        }

        public void SetCalibration(LinearCalibration? calibration, AdcMode mode = AdcMode.InternalWeight)
        {
            if (mode == AdcMode.InternalWeight) _internalCalibration = calibration;
            else _ads1115Calibration = calibration;

            _logger.LogInfo($"Calibration set manually for {_axleType} (Mode {mode}) - Valid: {calibration?.IsValid}", "WeightProcessor");
        }

        public void SetTareManager(TareManager tareManager)
        {
            _tareManager = tareManager;
            _logger.LogInfo($"{_axleType} TareManager set", "WeightProcessor");
        }

        public void SetADCMode(AdcMode adcMode) => _totalADCMode = adcMode;

        public void SetBrakeMode(bool isBrakeMode)
        {
            if (_isBrakeMode != isBrakeMode)
            {
                _isBrakeMode = isBrakeMode;
                _systemMode = _isBrakeMode ? SystemMode.Brake : SystemMode.Weight;
                _logger.LogInfo($"Loading calibrations for {_axleType} (Mode: {_vehicleMode})", "WeightProcessor");
                LoadCalibrationForMode(AdcMode.InternalWeight);
                LoadCalibrationForMode(AdcMode.Ads1115);
            }
        }

        public void LoadCalibration()
        {
            _systemMode = _isBrakeMode ? SystemMode.Brake : SystemMode.Weight;
            _logger.LogInfo($"Loading calibrations for {_axleType} (Mode: {_vehicleMode})", "WeightProcessor");
            LoadCalibrationForMode(AdcMode.InternalWeight);
            LoadCalibrationForMode(AdcMode.Ads1115);
        }

        private void LoadCalibrationForMode(AdcMode mode)
        {
            try
            {
                var cal = LinearCalibration.LoadFromFile(_vehicleMode, _axleType, mode, _systemMode);

                if (mode == AdcMode.InternalWeight)
                {
                    _internalCalibration = cal;
                    if (_internalCalibration != null)
                        _logger.LogInfo($"Internal {_axleType} calibration loaded ({_systemMode}): {_internalCalibration.GetEquationString()}", "WeightProcessor");
                }
                else
                {
                    _ads1115Calibration = cal;
                    if (_ads1115Calibration != null)
                        _logger.LogInfo($"ADS1115 {_axleType} calibration loaded ({_systemMode}): {_ads1115Calibration.GetEquationString()}", "WeightProcessor");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading calibration for {_axleType} (Mode {mode}): {ex.Message}", "WeightProcessor");
            }
        }

        public void SaveCalibration(LinearCalibration calibration)
        {
            try
            {
                calibration.SaveToFile(_vehicleMode, _axleType);
                _logger.LogInfo($"Calibration saved for {_axleType} (Mode: {_vehicleMode}, ADC: {calibration.ADCMode})", "WeightProcessor");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving calibration for {_axleType}: {ex.Message}", "WeightProcessor");
            }
        }

        public void ConfigureFilter(FilterType type, double alpha, int windowSize, bool enabled)
        {
            _filterType = type;
            _filterAlpha = alpha;
            _filterWindowSize = windowSize;
            _filterEnabled = enabled;

            _totalSmaCalibrated.Clear();
            _totalSmaTared.Clear();
            _calibratedFilterInitialized = false;
            _taredFilterInitialized = false;

            _logger.LogInfo($"{_axleType} Filter configured: Type={type}, Alpha={alpha}, Window={windowSize}, Enabled={enabled}", "WeightProcessor");
        }

        public void EnqueueRawData(int rawADC)
        {
            const int MAX_QUEUE_SIZE = 100;
            if (_rawDataQueue.Count > MAX_QUEUE_SIZE)
            {
                Interlocked.Increment(ref _droppedCount);
                return; 
            }
            _rawDataQueue.Enqueue(new RawWeightData(rawADC, DateTime.Now));
        }

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
                    try { await Task.Delay(1, token); }
                    catch (TaskCanceledException) { break; }
                }
            }
        }

        private void ProcessRawData(RawWeightData raw)
        {
            var calibration = _totalADCMode == AdcMode.InternalWeight ? _internalCalibration : _ads1115Calibration;

            double calibratedWeight = 0;
            double taredWeight = 0;
            double tareValue = 0;

            if (calibration?.IsValid == true)
            {
                calibratedWeight = calibration.RawToKg(raw.RawADC);

                if (_filterEnabled) calibratedWeight = ApplyFilter(calibratedWeight, true);

                tareValue = _tareManager?.GetOffsetKg(_totalADCMode) ?? 0;
                taredWeight = _tareManager?.ApplyTare(calibratedWeight, _totalADCMode) ?? calibratedWeight;

                if (_filterEnabled) taredWeight = ApplyFilter(taredWeight, false);
            }

            var processed = new ProcessedWeightData(raw.RawADC, calibratedWeight, taredWeight, tareValue, raw.Timestamp);
            Interlocked.Exchange(ref _latestTotal, processed);

            if (_dataLogger.IsLogging)
            {
                _dataLogger.LogDataPoint(_axleType.ToString(), processed.RawADC, processed.CalibratedWeight, processed.TaredWeight, processed.TareValue, calibration?.Slope ?? 0, calibration?.Intercept ?? 0, _totalADCMode);
            }
        }

        internal double ApplyFilter(double value, bool isCalibrated)
        {
            switch (_filterType)
            {
                case FilterType.EMA: return ApplyEMA(value, isCalibrated);
                case FilterType.SMA: return ApplySMA(value, isCalibrated);
                default: return value;
            }
        }

        private double ApplyEMA(double value, bool isCalibrated)
        {
            if (isCalibrated)
            {
                if (!_calibratedFilterInitialized) { _totalFilteredCalibrated = value; _calibratedFilterInitialized = true; return value; }
                _totalFilteredCalibrated = _filterAlpha * value + (1 - _filterAlpha) * _totalFilteredCalibrated;
                return _totalFilteredCalibrated;
            }
            else
            {
                if (!_taredFilterInitialized) { _totalFilteredTared = value; _taredFilterInitialized = true; return value; }
                _totalFilteredTared = _filterAlpha * value + (1 - _filterAlpha) * _totalFilteredTared;
                return _totalFilteredTared;
            }
        }

        private double ApplySMA(double value, bool isCalibrated)
        {
            Queue<double> buffer = isCalibrated ? _totalSmaCalibrated : _totalSmaTared;
            buffer.Enqueue(value);
            if (buffer.Count > _filterWindowSize) buffer.Dequeue();
            return buffer.Count > 0 ? buffer.Average() : value;
        }

        public void ResetFilters()
        {
            _calibratedFilterInitialized = false;
            _taredFilterInitialized = false;
            _totalFilteredCalibrated = 0;
            _totalFilteredTared = 0;
            _totalSmaCalibrated.Clear();
            _totalSmaTared.Clear();
            _logger.LogInfo($"{_axleType} Weight filters reset", "WeightProcessor");
        }

        public void Tare()
        {
            if (_tareManager == null) return;
            var latest = _latestTotal;
            if (latest != null)
            {
                _tareManager.TareTotal(latest.CalibratedWeight, _totalADCMode);
                _tareManager?.SaveToFile(_vehicleMode);
                ResetFilters();
            }
        }

        public void ResetTare()
        {
            if (_tareManager == null) return;
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

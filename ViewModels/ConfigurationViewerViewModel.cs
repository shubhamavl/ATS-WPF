using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ATS_WPF.Services.Interfaces;
using ATS.CAN.Engine.Services.Interfaces;
using ATS_WPF.ViewModels.Base;
using ATS_WPF.Core;
using ATS.CAN.Engine.Core;
using ATS_WPF.Models;
using ATS.CAN.Engine.Models;

namespace ATS_WPF.ViewModels
{
    public class ConfigurationViewerViewModel : BaseViewModel
    {
        private readonly ISettingsService _settings;
        private readonly IDialogService _dialog;

        private AxleType _selectedAxle = AxleType.Total;
        public AxleType SelectedAxle
        {
            get => _selectedAxle;
            set
            {
                if (SetProperty(ref _selectedAxle, value))
                {
                    Refresh();
                }
            }
        }

        public ObservableCollection<AxleType> AvailableAxles { get; } = new ObservableCollection<AxleType>();

        public bool IsAxleSelectorVisible => AvailableAxles.Count > 1;

        // Weight Mode Properties
        public string InternalCalFileLocation => _settings.GetCalibrationFilePath(SelectedAxle, false);
        public string InternalCalStatus => _settings.GetCalibrationDataInternal(SelectedAxle).IsValid ? "Calibrated" : "Not Calibrated";
        public double InternalCalSlope => _settings.GetCalibrationDataInternal(SelectedAxle).Slope;
        public double InternalCalIntercept => _settings.GetCalibrationDataInternal(SelectedAxle).Intercept;
        public string InternalCalDate => _settings.GetCalibrationDataInternal(SelectedAxle).CalibrationDate.ToString("yyyy-MM-dd HH:mm");

        public int InternalCalZeroPoint => _settings.GetCalibrationDataInternal(SelectedAxle).Points?.FirstOrDefault(p => p.KnownWeight == 0)?.RawADC ?? 0;
        public int InternalCalKnownWeightAdc => _settings.GetCalibrationDataInternal(SelectedAxle).Points?.OrderByDescending(p => p.KnownWeight).FirstOrDefault()?.RawADC ?? 0;

        public string AdsCalFileLocation => _settings.GetCalibrationFilePath(SelectedAxle, true);
        public string AdsCalStatus => _settings.GetCalibrationDataADS1115(SelectedAxle).IsValid ? "Calibrated" : "Not Calibrated";
        public double AdsCalSlope => _settings.GetCalibrationDataADS1115(SelectedAxle).Slope;
        public double AdsCalIntercept => _settings.GetCalibrationDataADS1115(SelectedAxle).Intercept;
        public string AdsCalDate => _settings.GetCalibrationDataADS1115(SelectedAxle).CalibrationDate.ToString("yyyy-MM-dd HH:mm");

        public int AdsCalZeroPoint => _settings.GetCalibrationDataADS1115(SelectedAxle).Points?.FirstOrDefault(p => p.KnownWeight == 0)?.RawADC ?? 0;
        public int AdsCalKnownWeightAdc => _settings.GetCalibrationDataADS1115(SelectedAxle).Points?.OrderByDescending(p => p.KnownWeight).FirstOrDefault()?.RawADC ?? 0;

        // Brake Mode Properties
        public string InternalCalBrakeFileLocation => _settings.GetCalibrationBrakeFilePath(SelectedAxle, false);
        public string InternalCalBrakeStatus => _settings.GetCalibrationDataInternalBrake(SelectedAxle).IsValid ? "Calibrated" : "Not Calibrated";
        public double InternalCalBrakeSlope => _settings.GetCalibrationDataInternalBrake(SelectedAxle).Slope;
        public double InternalCalBrakeIntercept => _settings.GetCalibrationDataInternalBrake(SelectedAxle).Intercept;
        public string InternalCalBrakeDate => _settings.GetCalibrationDataInternalBrake(SelectedAxle).CalibrationDate.ToString("yyyy-MM-dd HH:mm");
        public int InternalCalBrakeZeroPoint => _settings.GetCalibrationDataInternalBrake(SelectedAxle).Points?.FirstOrDefault(p => p.KnownWeight == 0)?.RawADC ?? 0;
        public int InternalCalBrakeKnownWeightAdc => _settings.GetCalibrationDataInternalBrake(SelectedAxle).Points?.OrderByDescending(p => p.KnownWeight).FirstOrDefault()?.RawADC ?? 0;

        public string AdsCalBrakeFileLocation => _settings.GetCalibrationBrakeFilePath(SelectedAxle, true);
        public string AdsCalBrakeStatus => _settings.GetCalibrationDataADS1115Brake(SelectedAxle).IsValid ? "Calibrated" : "Not Calibrated";
        public double AdsCalBrakeSlope => _settings.GetCalibrationDataADS1115Brake(SelectedAxle).Slope;
        public double AdsCalBrakeIntercept => _settings.GetCalibrationDataADS1115Brake(SelectedAxle).Intercept;
        public string AdsCalBrakeDate => _settings.GetCalibrationDataADS1115Brake(SelectedAxle).CalibrationDate.ToString("yyyy-MM-dd HH:mm");
        public int AdsCalBrakeZeroPoint => _settings.GetCalibrationDataADS1115Brake(SelectedAxle).Points?.FirstOrDefault(p => p.KnownWeight == 0)?.RawADC ?? 0;
        public int AdsCalBrakeKnownWeightAdc => _settings.GetCalibrationDataADS1115Brake(SelectedAxle).Points?.OrderByDescending(p => p.KnownWeight).FirstOrDefault()?.RawADC ?? 0;

        public string TareFileLocation => _settings.GetTareFilePath(SelectedAxle);
        public string TareStatus => _settings.TareValue != 0 ? "Active" : "Stable/Zero";
        public double TareBaseline => _settings.TareValue;

        public string DataDirectoryPath => PathHelper.GetDataDirectory();

        public ICommand ResetInternalCommand { get; }
        public ICommand ResetAdsCommand { get; }
        public ICommand ResetInternalBrakeCommand { get; }
        public ICommand ResetAdsBrakeCommand { get; }
        public ICommand OpenDataDirectoryCommand { get; }
        public ICommand RefreshCommand { get; }

        public ConfigurationViewerViewModel(ISettingsService settings, IDialogService dialog)
        {
            _settings = settings;
            _dialog = dialog;

            ResetInternalCommand = new RelayCommand(_ => ResetInternal());
            ResetAdsCommand = new RelayCommand(_ => ResetAds());
            ResetInternalBrakeCommand = new RelayCommand(_ => ResetInternalBrake());
            ResetAdsBrakeCommand = new RelayCommand(_ => ResetAdsBrake());
            OpenDataDirectoryCommand = new RelayCommand(_ => OpenDataDirectory());
            RefreshCommand = new RelayCommand(_ => Refresh());

            InitializeAxles();
        }

        private void InitializeAxles()
        {
            AvailableAxles.Clear();
            var mode = _settings.Settings.VehicleMode;

            if (mode == VehicleMode.TwoWheeler)
            {
                AvailableAxles.Add(AxleType.Total);
                SelectedAxle = AxleType.Total;
            }
            else
            {
                AvailableAxles.Add(AxleType.Left);
                AvailableAxles.Add(AxleType.Right);
                SelectedAxle = AxleType.Left; // Default for HMV/LMV
            }

            OnPropertyChanged(nameof(IsAxleSelectorVisible));
        }

        private void ResetInternal()
        {
            if (_dialog.ShowConfirmation($"Are you sure you want to reset Internal Calibration for {SelectedAxle} Axle?", "Reset Configuration"))
            {
                _settings.ResetCalibration(SelectedAxle, false);
                Refresh();
            }
        }

        private void ResetAds()
        {
            if (_dialog.ShowConfirmation($"Are you sure you want to reset ADS1115 Calibration for {SelectedAxle} Axle?", "Reset Configuration"))
            {
                _settings.ResetCalibration(SelectedAxle, true);
                Refresh();
            }
        }

        private void ResetInternalBrake()
        {
            if (_dialog.ShowConfirmation($"Are you sure you want to reset Internal Brake Calibration for {SelectedAxle} Axle?", "Reset Configuration"))
            {
                _settings.ResetBrakeCalibration(SelectedAxle, false);
                Refresh();
            }
        }

        private void ResetAdsBrake()
        {
            if (_dialog.ShowConfirmation($"Are you sure you want to reset ADS1115 Brake Calibration for {SelectedAxle} Axle?", "Reset Configuration"))
            {
                _settings.ResetBrakeCalibration(SelectedAxle, true);
                Refresh();
            }
        }

        private void OpenDataDirectory()
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", DataDirectoryPath);
            }
            catch (Exception ex)
            {
                _dialog.ShowError($"Could not open directory: {ex.Message}", "Error");
            }
        }

        public void Refresh()
        {
            OnPropertyChanged(""); // Refresh all properties
        }
    }
}


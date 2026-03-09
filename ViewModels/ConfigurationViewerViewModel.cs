using System;
using System.Linq;
using System.Windows.Input;
using ATS_WPF.Services.Interfaces;
using ATS_WPF.ViewModels.Base;
using ATS_WPF.Core;
using ATS_WPF.Models;

namespace ATS_WPF.ViewModels
{
    public class ConfigurationViewerViewModel : BaseViewModel
    {
        private readonly ISettingsService _settings;
        private readonly IDialogService _dialog;

        public string InternalCalFileLocation => _settings.GetCalibrationFilePath(AxleType.Total, false);
        public string InternalCalStatus => _settings.GetCalibrationDataInternal(AxleType.Total).IsValid ? "Calibrated" : "Not Calibrated";
        public double InternalCalSlope => _settings.GetCalibrationDataInternal(AxleType.Total).Slope;
        public double InternalCalIntercept => _settings.GetCalibrationDataInternal(AxleType.Total).Intercept;
        public string InternalCalDate => _settings.GetCalibrationDataInternal(AxleType.Total).CalibrationDate.ToString("yyyy-MM-dd HH:mm");

        public int InternalCalZeroPoint => _settings.GetCalibrationDataInternal(AxleType.Total).Points?.FirstOrDefault(p => p.KnownWeight == 0)?.RawADC ?? 0;
        public int InternalCalKnownWeightAdc => _settings.GetCalibrationDataInternal(AxleType.Total).Points?.OrderByDescending(p => p.KnownWeight).FirstOrDefault()?.RawADC ?? 0;

        public string AdsCalFileLocation => _settings.GetCalibrationFilePath(AxleType.Total, true);
        public string AdsCalStatus => _settings.GetCalibrationDataADS1115(AxleType.Total).IsValid ? "Calibrated" : "Not Calibrated";
        public double AdsCalSlope => _settings.GetCalibrationDataADS1115(AxleType.Total).Slope;
        public double AdsCalIntercept => _settings.GetCalibrationDataADS1115(AxleType.Total).Intercept;
        public string AdsCalDate => _settings.GetCalibrationDataADS1115(AxleType.Total).CalibrationDate.ToString("yyyy-MM-dd HH:mm");

        public int AdsCalZeroPoint => _settings.GetCalibrationDataADS1115(AxleType.Total).Points?.FirstOrDefault(p => p.KnownWeight == 0)?.RawADC ?? 0;
        public int AdsCalKnownWeightAdc => _settings.GetCalibrationDataADS1115(AxleType.Total).Points?.OrderByDescending(p => p.KnownWeight).FirstOrDefault()?.RawADC ?? 0;

        // Brake Mode Properties
        public string InternalCalBrakeFileLocation => _settings.GetCalibrationBrakeFilePath(AxleType.Total, false);
        public string InternalCalBrakeStatus => _settings.GetCalibrationDataInternalBrake(AxleType.Total).IsValid ? "Calibrated" : "Not Calibrated";
        public double InternalCalBrakeSlope => _settings.GetCalibrationDataInternalBrake(AxleType.Total).Slope;
        public double InternalCalBrakeIntercept => _settings.GetCalibrationDataInternalBrake(AxleType.Total).Intercept;
        public string InternalCalBrakeDate => _settings.GetCalibrationDataInternalBrake(AxleType.Total).CalibrationDate.ToString("yyyy-MM-dd HH:mm");
        public int InternalCalBrakeZeroPoint => _settings.GetCalibrationDataInternalBrake(AxleType.Total).Points?.FirstOrDefault(p => p.KnownWeight == 0)?.RawADC ?? 0;
        public int InternalCalBrakeKnownWeightAdc => _settings.GetCalibrationDataInternalBrake(AxleType.Total).Points?.OrderByDescending(p => p.KnownWeight).FirstOrDefault()?.RawADC ?? 0;

        public string AdsCalBrakeFileLocation => _settings.GetCalibrationBrakeFilePath(AxleType.Total, true);
        public string AdsCalBrakeStatus => _settings.GetCalibrationDataADS1115Brake(AxleType.Total).IsValid ? "Calibrated" : "Not Calibrated";
        public double AdsCalBrakeSlope => _settings.GetCalibrationDataADS1115Brake(AxleType.Total).Slope;
        public double AdsCalBrakeIntercept => _settings.GetCalibrationDataADS1115Brake(AxleType.Total).Intercept;
        public string AdsCalBrakeDate => _settings.GetCalibrationDataADS1115Brake(AxleType.Total).CalibrationDate.ToString("yyyy-MM-dd HH:mm");
        public int AdsCalBrakeZeroPoint => _settings.GetCalibrationDataADS1115Brake(AxleType.Total).Points?.FirstOrDefault(p => p.KnownWeight == 0)?.RawADC ?? 0;
        public int AdsCalBrakeKnownWeightAdc => _settings.GetCalibrationDataADS1115Brake(AxleType.Total).Points?.OrderByDescending(p => p.KnownWeight).FirstOrDefault()?.RawADC ?? 0;

        public string TareFileLocation => _settings.GetTareFilePath(AxleType.Total);
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
        }

        private void ResetInternal()
        {
            if (_dialog.ShowConfirmation("Are you sure you want to reset Internal Calibration?", "Reset Configuration"))
            {
                _settings.ResetCalibration(AxleType.Total, false);
                Refresh();
            }
        }

        private void ResetAds()
        {
            if (_dialog.ShowConfirmation("Are you sure you want to reset ADS1115 Calibration?", "Reset Configuration"))
            {
                _settings.ResetCalibration(AxleType.Total, true);
                Refresh();
            }
        }

        private void ResetInternalBrake()
        {
            if (_dialog.ShowConfirmation("Are you sure you want to reset Internal Brake Calibration?", "Reset Configuration"))
            {
                _settings.ResetBrakeCalibration(AxleType.Total, false);
                Refresh();
            }
        }

        private void ResetAdsBrake()
        {
            if (_dialog.ShowConfirmation("Are you sure you want to reset ADS1115 Brake Calibration?", "Reset Configuration"))
            {
                _settings.ResetBrakeCalibration(AxleType.Total, true);
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


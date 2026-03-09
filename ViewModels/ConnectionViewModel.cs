using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ATS_WPF.Adapters;
using ATS_WPF.Services.Interfaces;
using ATS_WPF.ViewModels.Base;
using ATS_WPF.Models;
using ATS_WPF.Core;

namespace ATS_WPF.ViewModels
{
    public class ConnectionViewModel : BaseViewModel
    {
        private readonly SystemManager _systemManager;
        private readonly ISettingsService _settings;

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    OnPropertyChanged(nameof(ConnectionButtonText));
                    OnPropertyChanged(nameof(ConnectionButtonColor));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(ConnectionStatusText));
                    ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)StartStreamCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string ConnectionButtonText => IsConnected ? "❌ Disconnect" : "🔌 Connect";
        public Brush ConnectionButtonColor => IsConnected
            ? new SolidColorBrush(Color.FromRgb(220, 53, 69)) // Red
            : new SolidColorBrush(Color.FromRgb(40, 167, 69)); // Green

        public Brush StatusColor => IsConnected
            ? new SolidColorBrush(Color.FromRgb(40, 167, 69)) // Green
            : new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red

        public string ConnectionStatusText => IsConnected ? "Connected" : "Disconnected";

        private bool _isStreaming;
        public bool IsStreaming
        {
            get => _isStreaming;
            set
            {
                if (SetProperty(ref _isStreaming, value))
                {
                    ((RelayCommand)StartStreamCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)StopStreamCommand).RaiseCanExecuteChanged();
                }
            }
        }

        // Adapter Configuration
        public ObservableCollection<string> AdapterTypes { get; } = new ObservableCollection<string> { "USB-CAN-A Serial", "PCAN" };

        private string _selectedAdapterType = "USB-CAN-A Serial";
        public string SelectedAdapterType
        {
            get => _selectedAdapterType;
            set
            {
                if (SetProperty(ref _selectedAdapterType, value))
                {
                    IsUsbAdapter = value == "USB-CAN-A Serial";
                    IsPcanAdapter = value == "PCAN";
                }
            }
        }

        private bool _isUsbAdapter;
        public bool IsUsbAdapter
        {
            get => _isUsbAdapter;
            set => SetProperty(ref _isUsbAdapter, value);
        }

        private bool _isPcanAdapter;
        public bool IsPcanAdapter
        {
            get => _isPcanAdapter;
            set => SetProperty(ref _isPcanAdapter, value);
        }

        public ObservableCollection<string> AvailablePorts { get; } = new ObservableCollection<string>();

        private string _selectedPort = "";
        public string SelectedPort
        {
            get => _selectedPort;
            set => SetProperty(ref _selectedPort, value);
        }

        private string _selectedLeftPort = "";
        public string SelectedLeftPort
        {
            get => _selectedLeftPort;
            set => SetProperty(ref _selectedLeftPort, value);
        }

        private string _selectedRightPort = "";
        public string SelectedRightPort
        {
            get => _selectedRightPort;
            set => SetProperty(ref _selectedRightPort, value);
        }

        public bool IsHmvMode => _settings.Settings.VehicleMode == VehicleMode.HMV;
        public bool IsSinglePortMode => !IsHmvMode;

        public ObservableCollection<string> BaudRates { get; } = new ObservableCollection<string>
        {
            "125 kbps", "250 kbps", "500 kbps", "1 Mbps"
        };

        private string _selectedBaudRate = "250 kbps";
        public string SelectedBaudRate
        {
            get => _selectedBaudRate;
            set => SetProperty(ref _selectedBaudRate, value);
        }

        public ObservableCollection<string> StreamingRates { get; } = new ObservableCollection<string>
        {
            "1 Hz", "100 Hz", "500 Hz", "1 kHz"
        };

        private string _selectedStreamingRate = "1 kHz";
        public string SelectedStreamingRate
        {
            get => _selectedStreamingRate;
            set => SetProperty(ref _selectedStreamingRate, value);
        }

        public ICommand ConnectCommand { get; }
        public ICommand StartStreamCommand { get; }
        public ICommand StopStreamCommand { get; }
        public ICommand RefreshPortsCommand { get; }

        public ConnectionViewModel(SystemManager systemManager, ISettingsService settings)
        {
            _systemManager = systemManager;
            _settings = settings;

            ConnectCommand = new RelayCommand(OnConnect);
            StartStreamCommand = new RelayCommand(OnStartStream, _ => IsConnected && !IsStreaming);
            StopStreamCommand = new RelayCommand(OnStopStream, _ => IsConnected); // Stop all always active if connected
            RefreshPortsCommand = new RelayCommand(OnRefreshPorts);

            // Initialize from Settings
            LoadSettings();
            RefreshPorts();
        }

        private void LoadSettings()
        {
            SelectedAdapterType = "USB-CAN-A Serial";
            IsUsbAdapter = SelectedAdapterType == "USB-CAN-A Serial";
            IsPcanAdapter = SelectedAdapterType == "PCAN";

            SelectedPort = _settings.Settings.ComPort;
            SelectedLeftPort = _settings.Settings.LeftComPort;
            SelectedRightPort = _settings.Settings.RightComPort;
            
            SelectedBaudRate = GetBaudRateString(_settings.Settings.CanBaudRate);
            SelectedStreamingRate = GetStreamingRateString(_settings.Settings.TransmissionRate);
        }

        public void RefreshMode()
        {
            OnPropertyChanged(nameof(IsHmvMode));
            OnPropertyChanged(nameof(IsSinglePortMode));
            LoadSettings();
            RefreshPorts();
        }

        private void OnRefreshPorts(object? obj)
        {
            RefreshPorts();
        }

        private void RefreshPorts()
        {
            AvailablePorts.Clear();
            var ports = SerialPort.GetPortNames();
            foreach (var port in ports)
            {
                AvailablePorts.Add(port);
            }

            // Port selection logic: ensure selected ports are valid
            if (!string.IsNullOrEmpty(SelectedPort) && !AvailablePorts.Contains(SelectedPort)) SelectedPort = AvailablePorts.FirstOrDefault() ?? "";
            else if (string.IsNullOrEmpty(SelectedPort)) SelectedPort = AvailablePorts.FirstOrDefault() ?? "";

            if (!string.IsNullOrEmpty(SelectedLeftPort) && !AvailablePorts.Contains(SelectedLeftPort)) SelectedLeftPort = AvailablePorts.FirstOrDefault() ?? "";
            else if (string.IsNullOrEmpty(SelectedLeftPort)) SelectedLeftPort = AvailablePorts.FirstOrDefault() ?? "";

            if (!string.IsNullOrEmpty(SelectedRightPort) && !AvailablePorts.Contains(SelectedRightPort)) SelectedRightPort = AvailablePorts.FirstOrDefault() ?? "";
            else if (string.IsNullOrEmpty(SelectedRightPort)) SelectedRightPort = AvailablePorts.FirstOrDefault() ?? "";
        }

        private void OnConnect(object? parameter)
        {
            if (IsConnected)
            {
                _systemManager.DisconnectAll();
                IsConnected = false;
                IsStreaming = false;
            }
            else
            {
                if (IsUsbAdapter)
                {
                    if (IsHmvMode && (string.IsNullOrEmpty(SelectedLeftPort) || string.IsNullOrEmpty(SelectedRightPort)))
                    {
                        MessageBox.Show("Please select both Left and Right COM ports for HMV mode.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    else if (IsSinglePortMode && string.IsNullOrEmpty(SelectedPort))
                    {
                        MessageBox.Show("Please select a COM port.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Update and save settings
                    _settings.SetComPort(SelectedPort);
                    if (IsHmvMode)
                    {
                        _settings.Settings.LeftComPort = SelectedLeftPort;
                        _settings.Settings.RightComPort = SelectedRightPort;
                    }
                    _settings.SetCanBaudRate(SelectedBaudRate);
                    _settings.SaveSettings();
                }
                else if (IsPcanAdapter)
                {
                    MessageBox.Show("PCAN support not fully implemented in this refactor view.", "Info");
                    return;
                }

                try 
                {
                    _systemManager.ConnectAll();
                    IsConnected = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Connection failed: {ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnStartStream(object? parameter)
        {
            TransmissionRate rate = GetStreamingRateValue(SelectedStreamingRate);
            _settings.SetTransmissionRate(SelectedStreamingRate);
            _settings.SaveSettings();
            
            _systemManager.StartStreamAll();
            IsStreaming = true;
        }

        private TransmissionRate GetStreamingRateValue(string rateString)
        {
            return rateString switch
            {
                "1 Hz" => TransmissionRate.Hz1,
                "100 Hz" => TransmissionRate.Hz100,
                "500 Hz" => TransmissionRate.Hz500,
                "1 kHz" => TransmissionRate.Hz1000,
                _ => TransmissionRate.Hz1000
            };
        }

        private void OnStopStream(object? parameter)
        {
            _systemManager.StopStreamAll();
            IsStreaming = false;
        }

        private ushort GetBaudRateValue(string rateString)
        {
            return rateString switch
            {
                "125 kbps" => 125,
                "250 kbps" => 250,
                "500 kbps" => 500,
                "1 Mbps" => 1000,
                _ => 250
            };
        }
        private string GetBaudRateString(CanBaudRate rate)
        {
            return rate switch
            {
                CanBaudRate.Bps125k => "125 kbps",
                CanBaudRate.Bps250k => "250 kbps",
                CanBaudRate.Bps500k => "500 kbps",
                CanBaudRate.Bps1M => "1 Mbps",
                _ => "250 kbps"
            };
        }

        private string GetStreamingRateString(TransmissionRate rate)
        {
            return rate switch
            {
                TransmissionRate.Hz1 => "1 Hz",
                TransmissionRate.Hz100 => "100 Hz",
                TransmissionRate.Hz500 => "500 Hz",
                TransmissionRate.Hz1000 => "1 kHz",
                _ => "1 kHz"
            };
        }
    }
}


using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows;
using ATS_WPF.Services;
using ATS_WPF.Services.Interfaces;
using ATS.CAN.Engine.Services.Interfaces;
using ATS_WPF.ViewModels.Base;
using ATS_WPF.Core;
using ATS.CAN.Engine.Core;
using ATS_WPF.Models;
using ATS.CAN.Engine.Models;

namespace ATS_WPF.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        // Services
        private readonly SystemManager _systemManager;
        private readonly IDataLoggerService _dataLogger;
        private readonly ISettingsService _settings;
        private readonly INavigationService _navigationService;

        // Child ViewModels - connection/logging/status panels
        public ConnectionViewModel Connection { get; }
        public SystemStatusPanelViewModel SystemStatus { get; }
        public LoggingPanelViewModel Logging { get; }
        public AppStatusBarViewModel StatusBar { get; }
        public SettingsViewModel Settings { get; }

        // === SINGLE DASHBOARD ARCHITECTURE ===
        // All axles are stored here, but only ONE is "active" and shown in the UI.
        private readonly ObservableCollection<AxleViewModel> _allAxles = new();

        private AxleViewModel? _activeAxle;
        public AxleViewModel? ActiveAxle
        {
            get => _activeAxle;
            private set => SetProperty(ref _activeAxle, value);
        }

        // Indicates which axle tab is "selected" (for styling the toggle button)
        private AxleType _selectedAxleTab = AxleType.Total;
        public AxleType SelectedAxleTab
        {
            get => _selectedAxleTab;
            private set
            {
                if (SetProperty(ref _selectedAxleTab, value))
                {
                    OnPropertyChanged(nameof(IsLeftActive));
                    OnPropertyChanged(nameof(IsRightActive));
                }
            }
        }

        public bool IsLeftActive => SelectedAxleTab == AxleType.Left;
        public bool IsRightActive => SelectedAxleTab == AxleType.Right;

        // Toggle buttons are only visible for multi-axle modes
        public bool IsMultiAxleMode => _systemManager.LogicalAxles.Count > 1;

        // Timer for UI updates
        private readonly DispatcherTimer _uiTimer;

        // Vehicle Mode Switcher
        public ObservableCollection<VehicleMode> AvailableVehicleModes { get; } = new()
        {
            VehicleMode.TwoWheeler,
            VehicleMode.LMV,
            VehicleMode.HMV
        };

        private VehicleMode _selectedVehicleMode;
        public VehicleMode SelectedVehicleMode
        {
            get => _selectedVehicleMode;
            set
            {
                if (SetProperty(ref _selectedVehicleMode, value))
                    OnVehicleModeChanged(value);
            }
        }

        // Commands
        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenConfigViewerCommand { get; }
        public ICommand OpenBootloaderCommand { get; }
        public ICommand OpenMonitorCommand { get; }
        public ICommand OpenLogsCommand { get; }
        public ICommand OpenStatusHistoryCommand { get; }
        public ICommand StopAllCommand { get; }
        public ICommand SelectLeftAxleCommand { get; }
        public ICommand SelectRightAxleCommand { get; }

        // Events
        public event Action? OpenSettingsRequested;
        public event Action? OpenConfigViewerRequested;

        public MainWindowViewModel(
            SystemManager systemManager,
            IDataLoggerService dataLogger,
            ISettingsService settings,
            INavigationService navigationService,
            IUpdateService updateService,
            IDialogService dialogService,
            IStatusMonitorService statusMonitor,
            StatusHistoryManager historyManager,
            ICANService canService)
        {
            _systemManager = systemManager;
            _dataLogger = dataLogger;
            _settings = settings;
            _navigationService = navigationService;

            // Initialize SystemManager with configured vehicle mode

            // Child ViewModels
            Connection = new ConnectionViewModel(_systemManager, settings);
            SystemStatus = new SystemStatusPanelViewModel(_systemManager, canService, navigationService, statusMonitor, dialogService, historyManager);
            Logging = new LoggingPanelViewModel(dataLogger, _systemManager);
            StatusBar = new AppStatusBarViewModel(_systemManager, updateService, dialogService);
            Settings = new SettingsViewModel(settings);

            _selectedVehicleMode = _systemManager.CurrentMode;

            // Commands
            OpenSettingsCommand = new RelayCommand(_ => OnOpenSettings());
            OpenConfigViewerCommand = new RelayCommand(_ => OpenConfigViewerRequested?.Invoke());
            OpenBootloaderCommand = new RelayCommand(_ => _navigationService.ShowBootloaderManager());
            OpenMonitorCommand = new RelayCommand(_ => _navigationService.ShowMonitorWindow());
            OpenLogsCommand = new RelayCommand(_ => _navigationService.ShowLogsWindow());
            OpenStatusHistoryCommand = new RelayCommand(_ => _navigationService.ShowStatusHistory());
            StopAllCommand = new RelayCommand(OnStopAll);
            SelectLeftAxleCommand = new RelayCommand(_ => SelectAxle(AxleType.Left));
            SelectRightAxleCommand = new RelayCommand(_ => SelectAxle(AxleType.Right));

            // Build internal axle list and select the default
            RebuildAxleViewModels();

            // UI Timer
            _uiTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_settings.Settings.UIUpdateRateMs)
            };
            _uiTimer.Tick += OnUiTimerTick;
            _uiTimer.Start();

            _settings.SettingsChanged += OnSettingsChanged;
        }

        // ─── Axle Selection Logic ──────────────────────────────────────────────

        private void SelectAxle(AxleType type)
        {
            var target = _allAxles.FirstOrDefault(a => a.Type == type);
            if (target != null)
            {
                ActiveAxle = target;
                SelectedAxleTab = type;

                // Unified hardware sync (handles both HMV port switching and LMV relay switching)
                _systemManager.SetActiveAxle(type);
            }
        }

        private void RebuildAxleViewModels()
        {
            _allAxles.Clear();
            foreach (var logicalAxle in _systemManager.LogicalAxles)
                _allAxles.Add(new AxleViewModel(logicalAxle, _settings, _navigationService));

            // For multi-axle: default to Left. For single-axle: just show the only axle.
            if (_allAxles.Count > 1)
            {
                // Select Left by default
                ActiveAxle = _allAxles.FirstOrDefault(a => a.Type == AxleType.Left) ?? _allAxles.First();
                SelectedAxleTab = ActiveAxle.Type;
            }
            else if (_allAxles.Count == 1)
            {
                ActiveAxle = _allAxles.First();
                SelectedAxleTab = ActiveAxle.Type;
            }
            else
            {
                ActiveAxle = null;
            }

            OnPropertyChanged(nameof(IsMultiAxleMode));
        }

        // ─── Mode Change ───────────────────────────────────────────────────────

        private async void OnVehicleModeChanged(VehicleMode mode)
        {
            // Update and save settings immediately
            _settings.Settings.VehicleMode = mode;
            _settings.SaveSettings();

            // Prompt user for restart
            bool restart = MessageBox.Show(
                $"Vehicle mode changed to {mode}. The application must restart to apply hardware and service changes.\n\nRestart now?",
                "Vehicle Mode Change",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;

            if (restart)
            {
                // Safety: Disconnect all nodes explicitly to release OS handles
                _systemManager.DisconnectAll();
                
                // Small delay to allow Windows to catch up with port closure
                await System.Threading.Tasks.Task.Delay(500);

                // Launch new instance
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(exePath))
                {
                    System.Diagnostics.Process.Start(exePath);
                    Application.Current.Shutdown();
                }
            }
            else
            {
                // Revert selection in UI if user cancels
                _selectedVehicleMode = _systemManager.CurrentMode;
                OnPropertyChanged(nameof(SelectedVehicleMode));
            }
        }

        // ─── Timer / Refresh ───────────────────────────────────────────────────

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            _uiTimer.Interval = TimeSpan.FromMilliseconds(_settings.Settings.UIUpdateRateMs);
        }

        private void OnUiTimerTick(object? sender, EventArgs e)
        {
            ActiveAxle?.Refresh();
            Logging.Refresh();
            StatusBar.Refresh();
            SystemStatus.Refresh();
        }

        private void OnStopAll(object? parameter)
        {
            _systemManager.StopStreamAll();
            _dataLogger.StopLogging();
            Connection.IsStreaming = false;
        }

        private void OnOpenSettings()
        {
            if (ActiveAxle != null)
            {
                Settings.Calibration.ActiveAxleType = ActiveAxle.Type;
            }
            OpenSettingsRequested?.Invoke();
        }

        public void Cleanup()
        {
            _uiTimer.Stop();
            _systemManager.DisconnectAll();
        }
    }
}

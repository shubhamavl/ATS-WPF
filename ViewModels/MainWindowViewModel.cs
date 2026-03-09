using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows;
using ATS_WPF.Services;
using ATS_WPF.Services.Interfaces;
using ATS_WPF.ViewModels.Base;
using ATS_WPF.Core;
using ATS_WPF.Models;

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
            StatusHistoryManager historyManager)
        {
            _systemManager = systemManager;
            _dataLogger = dataLogger;
            _settings = settings;
            _navigationService = navigationService;

            // Initialize SystemManager with configured vehicle mode
            _systemManager.Initialize(_settings.Settings.VehicleMode);

            // Child ViewModels
            Connection = new ConnectionViewModel(_systemManager, settings);
            SystemStatus = new SystemStatusPanelViewModel(_systemManager, navigationService, statusMonitor, dialogService);
            Logging = new LoggingPanelViewModel(dataLogger, _systemManager);
            StatusBar = new AppStatusBarViewModel(_systemManager, updateService, dialogService);
            Settings = new SettingsViewModel(settings);

            _selectedVehicleMode = _systemManager.CurrentMode;

            // Commands
            OpenSettingsCommand = new RelayCommand(_ => OnOpenSettings());
            OpenConfigViewerCommand = new RelayCommand(_ => OpenConfigViewerRequested?.Invoke());
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

        private void OnVehicleModeChanged(VehicleMode mode)
        {
            _systemManager.SetVehicleMode(mode);
            RebuildAxleViewModels();
            Connection.RefreshMode();
            SystemStatus.ReattachNodes();
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
            OpenSettingsRequested?.Invoke();
        }

        public void Cleanup()
        {
            _uiTimer.Stop();
            _systemManager.DisconnectAll();
        }
    }
}

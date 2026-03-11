using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ATS_WPF.Services.Interfaces;
using ATS_WPF.Core;
using ATS_WPF.Models;

namespace ATS_WPF.Services
{
    public class StatusMonitorService : IStatusMonitorService
    {
        private readonly SystemManager _systemManager;
        private readonly IDialogService _dialogService;
        private readonly DispatcherTimer _timer;
        private bool _isSystemAvailable = true;
        private bool _wasConnected = false;

        // Always resolve the current primary node dynamically so mode changes don't leave a stale reference.
        private ICANService? PrimaryCanService =>
            _systemManager.PhysicalNodes.Count > 0 ? _systemManager.PhysicalNodes[0].CanService : null;

        public event EventHandler<bool>? AvailabilityChanged;

        public bool IsSystemAvailable { get => _isSystemAvailable; private set => _isSystemAvailable = value; }

        public StatusMonitorService(SystemManager systemManager, IDialogService dialogService)
        {
            _systemManager = systemManager;
            _dialogService = dialogService;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTimerTick;
        }

        public void StartMonitoring()
        {
            _timer.Start();
            ProductionLogger.Instance.LogInfo("Status monitoring started", "StatusMonitor");
        }

        public void StopMonitoring()
        {
            _timer.Stop();
            ProductionLogger.Instance.LogInfo("Status monitoring stopped", "StatusMonitor");
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            try
            {
                var canService = PrimaryCanService;
                if (canService == null || !canService.IsConnected)
                {
                    if (_wasConnected)
                    {
                        UpdateAvailability(false);
                        _wasConnected = false;
                    }
                    return;
                }

                _wasConnected = true;

                var now = DateTime.Now;
                var timeSinceRx = now - canService.LastRxTime;
                var timeSinceStatus = now - canService.LastSystemStatusTime;

                if (timeSinceStatus.TotalSeconds > 2)
                {
                    canService.RequestSystemStatus(log: false);
                }

                bool isDataFlowing = timeSinceRx.TotalSeconds < 3;
                bool isStatusFlowing = timeSinceStatus.TotalSeconds < 5;

                bool isAvailable = false;

                if (canService.IsStreaming)
                {
                    isAvailable = isDataFlowing;
                }
                else
                {
                    isAvailable = isStatusFlowing || isDataFlowing;
                }

                UpdateAvailability(isAvailable);
            }
            catch (Exception ex)
            {
                ProductionLogger.Instance.LogError($"Monitor timer error: {ex.Message}", "StatusMonitor");
            }
        }

        private void UpdateAvailability(bool isAvailable)
        {
            if (_isSystemAvailable != isAvailable)
            {
                _isSystemAvailable = isAvailable;
                AvailabilityChanged?.Invoke(this, isAvailable);

                // Only show popup if we were previously OK and still think we are "Connected" (serial port open)
                // Using Dispatcher to ensure UI thread if showing dialog
                // Warning: Blocking dialogs in timer tick can be dangerous.
                // We rely on ViewModel to show visual indication (Red text).
                _ = PrimaryCanService?.IsConnected; // no-op: avoids dialog spam

                ProductionLogger.Instance.LogInfo($"System availability changed: {isAvailable}", "StatusMonitor");
            }
        }

        public void Dispose()
        {
            _timer.Stop();
        }
    }
}


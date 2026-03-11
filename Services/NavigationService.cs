using System;
using System.Windows;
using ATS_WPF.Services.Interfaces;
using ATS_WPF.Views;
using ATS_WPF.Core;
using ATS_WPF.ViewModels;
using ATS_WPF.Models;

namespace ATS_WPF.Services
{
    public class NavigationService : INavigationService
    {
        public void ShowBootloaderManager()
        {
            var canService = ServiceRegistry.GetService<ICANService>();
            var firmwareService = ServiceRegistry.GetService<IFirmwareUpdateService>();
            var diagService = ServiceRegistry.GetService<IBootloaderDiagnosticsService>();
            var dialogService = ServiceRegistry.GetService<IDialogService>();

            if (canService == null || firmwareService == null || diagService == null || dialogService == null)
            {
                dialogService?.ShowError("Required services for Bootloader not found.", "Service Error");
                return;
            }

            var vm = new BootloaderViewModel(canService, firmwareService, diagService, dialogService);
            var window = new BootloaderManagerWindow(vm);
            window.Owner = Application.Current.MainWindow;
            window.ShowDialog();
        }



        public void ShowCalibrationDialog(bool isBrakeMode = false)
        {
            var canService = ServiceRegistry.GetService<ICANService>();
            var settings = ServiceRegistry.GetService<ISettingsService>();
            var dialogService = ServiceRegistry.GetService<IDialogService>();
            var logger = ServiceRegistry.GetService<IProductionLoggerService>();
            var weightProcessor = ServiceRegistry.GetService<IWeightProcessorService>();

            var vm = new CalibrationDialogViewModel(canService, settings, dialogService, logger, weightProcessor, (byte)(canService.CurrentADCMode == AdcMode.Ads1115 ? 1 : 0), 500, isBrakeMode);
            var diag = new CalibrationDialog(vm);
            diag.Owner = Application.Current.MainWindow;
            diag.ShowDialog();
        }

        public void ShowMonitorWindow()
        {
            var canService = ServiceRegistry.GetService<ICANService>() as CANService;
            var win = new MonitorWindow(canService);
            win.Owner = Application.Current.MainWindow;
            win.Show();
        }

        public void ShowLogsWindow()
        {
            var logger = ServiceRegistry.GetService<IProductionLoggerService>();
            var dataLogger = ServiceRegistry.GetService<IDataLoggerService>();
            var dialog = ServiceRegistry.GetService<IDialogService>();

            var vm = new LogsViewModel(logger, dataLogger, dialog);
            var win = new LogsWindow(null, null); // Pass nulls as they are no longer used by the new logic
            win.DataContext = vm;
            win.Owner = Application.Current.MainWindow;
            win.Show();
        }

        public void ShowStatusHistory()
        {
            var statusManager = ServiceRegistry.GetService<Core.StatusHistoryManager>();
            var vm = new StatusHistoryViewModel(statusManager);
            var win = new StatusHistoryWindow();
            win.DataContext = vm;
            win.Owner = Application.Current.MainWindow;
            win.Show();
        }

        public void CloseWindow(object window)
        {
            if (window is Window win)
            {
                win.Close();
            }
        }
    }
}


using System.Windows;
using System.IO;
using ATS_WPF.Views;
using ATS_WPF.Services;
using ATS_WPF.Services.Interfaces;
using ATS_WPF.Core;
using Microsoft.Extensions.DependencyInjection;

namespace ATS_WPF
{
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Initialize legacy ServiceRegistry bridge
            ATS_WPF.Core.ServiceRegistry.SetProvider(ServiceProvider);

            // Enhanced global exception handling
            this.DispatcherUnhandledException += (sender, args) =>
            {
                var logger = ServiceProvider.GetService<IProductionLoggerService>() ?? ProductionLogger.Instance;
                logger.LogError($"Unhandled Exception: {args.Exception.GetType().Name} - {args.Exception.Message}", "Global");
                logger.LogError($"Stack Trace: {args.Exception.StackTrace}", "Global");

                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{args.Exception.Message}\n\nCheck logs for details.",
                    "System Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                args.Handled = true;
            };

            // Handle background thread exceptions
            System.AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var logger = ServiceProvider.GetService<IProductionLoggerService>() ?? ProductionLogger.Instance;
                var ex = args.ExceptionObject as System.Exception;
                logger.LogError($"Unhandled Thread Exception: {ex?.Message}", "Global");
                if (ex != null)
                {
                    logger.LogError($"Stack Trace: {ex.StackTrace}", "Global");
                }
            };

            // Initialize System Manager early to populate nodes for legacy ICANService bridge
            var systemManager = ServiceProvider.GetRequiredService<SystemManager>();
            var settings = ServiceProvider.GetRequiredService<ISettingsService>();
            systemManager.Initialize(settings.Settings.VehicleMode);

            // Resolve and show MainWindow
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                var mainWin = MainWindow as MainWindow;
                if (mainWin?.DataContext is ATS_WPF.ViewModels.MainWindowViewModel vm)
                {
                    vm.Cleanup();
                }
            }
            catch { /* Ignore errors on exit */ }

            base.OnExit(e);
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Infrastructure Services
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IUpdateService, UpdateService>();
            services.AddSingleton<IDataLoggerService, DataLogger>();
            services.AddSingleton<IProductionLoggerService>(ProductionLogger.Instance);
            services.AddSingleton<StatusHistoryManager>();
            services.AddSingleton<ISettingsService>(SettingsManager.Instance);

            // Unified System Manager (Depends on Settings and DataLogger)
            services.AddSingleton<SystemManager>();

            // Legacy bridge (points to primary node/axle)
            services.AddSingleton<ICANService>(provider => provider.GetRequiredService<SystemManager>().PhysicalNodes[0].CanService);
            
            // Weight & Tare are now managed dynamically per-Axle by SystemManager.

            // Bootloader Services
            services.AddSingleton<IBootloaderDiagnosticsService, BootloaderDiagnosticsService>();
            services.AddSingleton<IFirmwareUpdateService, FirmwareUpdateService>();

            // Status Monitor
            services.AddSingleton<IStatusMonitorService>(provider => {
                var sm = new StatusMonitorService(
                    provider.GetRequiredService<ICANService>(),
                    provider.GetRequiredService<IDialogService>()
                );
                sm.StartMonitoring();
                return sm;
            });

            // ViewModels
            services.AddTransient<ATS_WPF.ViewModels.ConnectionViewModel>();
            services.AddTransient<ATS_WPF.ViewModels.SettingsViewModel>();
            services.AddTransient<ATS_WPF.ViewModels.BootloaderViewModel>();
            services.AddTransient<ATS_WPF.ViewModels.LogsViewModel>();
            services.AddSingleton<ATS_WPF.ViewModels.MainWindowViewModel>();

            // Views
            services.AddSingleton<MainWindow>();
        }
    }
}


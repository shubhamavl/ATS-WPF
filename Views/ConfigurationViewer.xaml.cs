using System.Windows;
using ATS_WPF.ViewModels;
using ATS_WPF.Core;
using ATS.CAN.Engine.Core;
using ATS_WPF.Services.Interfaces;
using ATS.CAN.Engine.Services.Interfaces;

namespace ATS_WPF.Views
{
    public partial class ConfigurationViewer : Window
    {
        public ConfigurationViewer()
        {
            InitializeComponent();

            // Resolve ViewModel
            var settings = ServiceRegistry.GetService<ISettingsService>();
            var dialog = ServiceRegistry.GetService<IDialogService>();
            DataContext = new ConfigurationViewerViewModel(settings, dialog);
        }
    }
}


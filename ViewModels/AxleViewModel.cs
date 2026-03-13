using System;
using ATS_WPF.Core;
using ATS.CAN.Engine.Core;
using ATS_WPF.Models;
using ATS.CAN.Engine.Models;
using ATS_WPF.Services.Interfaces;
using ATS.CAN.Engine.Services.Interfaces;
using ATS_WPF.ViewModels.Base;

namespace ATS_WPF.ViewModels
{
    public class AxleViewModel : BaseViewModel
    {
        private readonly AxleSystem _axleSystem;
        public AxleType Type => _axleSystem.Type;
        public string Header => _axleSystem.Type.ToString();

        public DashboardViewModel Dashboard { get; }
        public CalibrationViewModel Calibration { get; }

        public AxleViewModel(
            AxleSystem axleSystem, 
            ISettingsService settings,
            INavigationService navigationService)
        {
            _axleSystem = axleSystem;

            Dashboard = new DashboardViewModel(_axleSystem.WeightProcessor, _axleSystem.PrimaryNode.CanService, settings);
            Calibration = new CalibrationViewModel(_axleSystem.Type, _axleSystem.WeightProcessor, _axleSystem.PrimaryNode.CanService, settings, navigationService);
        }

        public void Refresh()
        {
            Dashboard.Refresh();
        }
    }
}

using System;

using ATS_WPF.Models;
using ATS.CAN.Engine.Models;
using ATS.CAN.Engine.Services.Interfaces;

namespace ATS_WPF.Services.Interfaces
{
    public interface INavigationService
    {
        void ShowBootloaderManager();
        void ShowCalibrationDialog(AxleType axleType, IWeightProcessorService weightProcessor, bool isBrakeMode = false);
        void ShowMonitorWindow();
        void ShowLogsWindow();
        void ShowStatusHistory();
        void CloseWindow(object window);
    }
}


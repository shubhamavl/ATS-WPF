using System;

namespace ATS_WPF.Services.Interfaces
{
    public interface INavigationService
    {
        void ShowBootloaderManager();
        void ShowWeightTestWindow();
        void ShowCalibrationDialog(bool isBrakeMode = false);
        void ShowMonitorWindow();
        void ShowLogsWindow();
        void ShowStatusHistory();
        void CloseWindow(object window);
    }
}


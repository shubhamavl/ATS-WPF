using System;

namespace ATS_WPF.Models
{
    public class CalibrationDialogResultsEventArgs : EventArgs
    {
        public string InternalEquation { get; set; } = "";
        public string AdsEquation { get; set; } = "";
        public bool IsSuccessful { get; set; }
    }
}


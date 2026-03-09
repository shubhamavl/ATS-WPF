using System.Collections.Generic;
using ATS_WPF.Models;

namespace ATS_WPF.Core
{
    public interface ISystemManager
    {
        VehicleMode CurrentMode { get; }
        IReadOnlyList<CANNode> PhysicalNodes { get; }
        IReadOnlyList<AxleSystem> LogicalAxles { get; }

        void Initialize(VehicleMode mode);
        void ConnectAll();
        void DisconnectAll();
        void StartStreamAll();
        void StopStreamAll();
        void SetVehicleMode(VehicleMode mode);
        void SetBrakeModeAll(bool isBrakeMode);
    }
}

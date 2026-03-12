using System;
using System.Collections.Generic;
using ATS_WPF.Models;
using ATS_WPF.Services.Interfaces;

namespace ATS_WPF.Core
{
    public interface ISystemManager
    {
        VehicleMode CurrentMode { get; }
        int ActiveNodeIndex { get; }
        ICANService ActiveNodeService { get; }
        event EventHandler? ActiveNodeChanged;
        event EventHandler? NodesInitialized;
        
        void SetActiveNode(int index);
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

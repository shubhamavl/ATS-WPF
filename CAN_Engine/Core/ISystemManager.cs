using System;
using System.Collections.Generic;
using ATS.CAN.Engine.Models;
using ATS.CAN.Engine.Services.Interfaces;

namespace ATS.CAN.Engine.Core
{
    public interface ISystemManager
    {
        VehicleMode CurrentMode { get; }
        ICanSettings Settings { get; }
        int ActiveNodeIndex { get; }
        ICANService ActiveNodeService { get; }
        event EventHandler? ActiveNodeChanged;
        event EventHandler? NodesInitialized;
        
        void SetActiveNode(int index);
        void SetActiveAxle(AxleType type);
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

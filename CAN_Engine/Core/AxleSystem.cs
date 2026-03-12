using ATS_WPF.Services;
using ATS_WPF.Models;
using ATS_WPF.Services.Interfaces;
using System;
using ATS.CAN.Engine.Services;
using ATS.CAN.Engine.Services.Interfaces;

using ATS.CAN.Engine.Models;

namespace ATS.CAN.Engine.Core
{
    public class AxleSystem
    {
        public AxleType Type { get; }
        public CANNode PrimaryNode { get; }
        public TareManager TareManager { get; }
        public IWeightProcessorService WeightProcessor { get; }

        public AxleSystem(AxleType type, CANNode primaryNode, TareManager tareManager, IWeightProcessorService weightProcessor)
        {
            Type = type;
            PrimaryNode = primaryNode;
            TareManager = tareManager;
            WeightProcessor = weightProcessor;
        }
    }
}

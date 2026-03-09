using System;
using ATS_WPF.Services;
using ATS_WPF.Services.Interfaces;

using ATS_WPF.Models;

namespace ATS_WPF.Core
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

using ATS.CAN.Engine.Models;
using System;
using ATS.CAN.Engine.Services.Interfaces;

namespace ATS.CAN.Engine.Core
{
    public class CANNode
    {
        public string NodeId { get; }
        public ICANService CanService { get; }

        public CANNode(string nodeId, ICANService canService)
        {
            NodeId = nodeId;
            CanService = canService;
        }
    }
}

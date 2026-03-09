using System;
using ATS_WPF.Services.Interfaces;

namespace ATS_WPF.Core
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

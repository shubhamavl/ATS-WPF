using ATS_WPF.Services;
using ATS_WPF.Models;
using ATS_WPF.Services.Interfaces;
using System;
using System.Collections.Generic;
using ATS.CAN.Engine.Models;
using ATS.CAN.Engine.Adapters;
using ATS.CAN.Engine.Services;
using ATS.CAN.Engine.Services.Interfaces;

namespace ATS.CAN.Engine.Core
{
    public class SystemManager : ISystemManager, IDisposable
    {
        private readonly List<CANNode> _nodes = new();
        private readonly List<AxleSystem> _axles = new();
        private readonly ICanSettings _settings;
        private readonly ICanDataLogger _dataLogger;

        private int _activeNodeIndex = 0;
        public int ActiveNodeIndex => _activeNodeIndex;
        public ICANService ActiveNodeService => (_nodes.Count > _activeNodeIndex) ? _nodes[_activeNodeIndex].CanService : (_nodes.Count > 0 ? _nodes[0].CanService : null!);
        public event EventHandler? ActiveNodeChanged;
        public event EventHandler? NodesInitialized;

        public VehicleMode CurrentMode { get; private set; }
        public IReadOnlyList<CANNode> PhysicalNodes => _nodes.AsReadOnly();
        public IReadOnlyList<AxleSystem> LogicalAxles => _axles.AsReadOnly();

        private readonly ICanLogger _logger; // Added ICanLogger field

        public SystemManager(ICanSettings settings, ICanDataLogger dataLogger, ICanLogger? logger = null)
        {
            _settings = settings;
            _dataLogger = dataLogger;
            _logger = logger ?? DefaultCanLogger.Instance;
        }

        public void SetActiveNode(int index)
        {
            if (index >= 0 && index < _nodes.Count && _activeNodeIndex != index)
            {
                _activeNodeIndex = index;
                ActiveNodeChanged?.Invoke(this, EventArgs.Empty);
                _logger.LogInfo($"Active CAN node switched to index: {index}", "SystemManager"); // Replaced ProductionLogger
            }
        }

        public void Initialize(VehicleMode mode)
        {
            CurrentMode = mode;
            Cleanup();

            var settings = _settings;

            if (mode == VehicleMode.TwoWheeler)
            {
                // 1 Node, 1 Axle
                var node = CreateNode("Main", settings.ComPort); // Uses legacy/shared port
                _nodes.Add(node);

                var axle = CreateAxle(AxleType.Total, node);
                _axles.Add(axle);
            }
            else if (mode == VehicleMode.LMV)
            {
                // 1 Node, 2 Axles (Left/Right)
                var node = CreateNode("Main", settings.ComPort); // Uses single shared port
                _nodes.Add(node);

                var leftAxle = CreateAxle(AxleType.Left, node);
                var rightAxle = CreateAxle(AxleType.Right, node);
                _axles.Add(leftAxle);
                _axles.Add(rightAxle);
            }
            else if (mode == VehicleMode.HMV)
            {
                // 2 Nodes, 2 Axles (Left/Right)
                var leftNode = CreateNode("Left", settings.LeftComPort);
                var rightNode = CreateNode("Right", settings.RightComPort);
                _nodes.Add(leftNode);
                _nodes.Add(rightNode);

                var leftAxle = CreateAxle(AxleType.Left, leftNode);
                var rightAxle = CreateAxle(AxleType.Right, rightNode);
                _axles.Add(leftAxle);
                _axles.Add(rightAxle);
            }

            NodesInitialized?.Invoke(this, EventArgs.Empty);
        }

        private CANNode CreateNode(string nodeId, string portName)
        {
            var canService = new CANService(_logger); // Pass injected logger
            return new CANNode(nodeId, canService);
        }

        private AxleSystem CreateAxle(AxleType axleType, CANNode node)
        {
            // Create dedicated TareManager and WeightProcessor for this axle
            var tareManager = new TareManager(axleType);
            tareManager.LoadFromFile(CurrentMode); 

            var weightProcessor = new WeightProcessor(axleType, CurrentMode, node.CanService, tareManager, _settings, _dataLogger, _logger);
            
            return new AxleSystem(axleType, node, tareManager, weightProcessor);
        }

        private void Cleanup()
        {
            foreach (var axle in _axles)
            {
                axle.WeightProcessor.Stop();
            }
            
            foreach (var node in _nodes)
            {
                node.CanService.Disconnect();
            }

            _nodes.Clear();
            _axles.Clear();
            _activeNodeIndex = 0;
        }

        public void ConnectAll()
        {
            _logger.LogInfo($"Connecting all nodes for mode: {CurrentMode}", "SystemManager");
            
            foreach (var node in _nodes)
            {
                string port = node.NodeId == "Right" ? _settings.RightComPort : 
                               (node.NodeId == "Left" ? _settings.LeftComPort : _settings.ComPort);
                              
                _logger.LogInfo($"Connecting node '{node.NodeId}' on port '{port}'...", "SystemManager");

                var config = new UsbSerialCanAdapterConfig
                {
                    PortName = port,
                    BitrateKbps = _settings.CanBitrateKbps, 
                    SerialBaudRate = 2000000
                };
                
                if (node.CanService.Connect(config, out string error))
                {
                    _logger.LogInfo($"Node '{node.NodeId}' connected successfully.", "SystemManager");
                }
                else
                {
                    _logger.LogError($"Node '{node.NodeId}' failed to connect on {port}: {error}", "SystemManager");
                }
            }
            
            foreach (var axle in _axles)
            {
                axle.WeightProcessor.Start();
            }
        }

        public void DisconnectAll()
        {
            foreach (var axle in _axles)
            {
                axle.WeightProcessor.Stop();
            }

            foreach (var node in _nodes)
            {
                node.CanService.Disconnect();
            }
        }

        public void StartStreamAll()
        {
            if (CurrentMode == VehicleMode.LMV)
            {
                // LMV: Start main stream (0x040) on the single Main node. 
                // Selection of left/right side (0x048) is handled sequentially in UI or WeightProcessor.
                if (_nodes.Count > 0)
                {
                    _nodes[0].CanService.StartStream((TransmissionRate)_settings.TransmissionRateCode, CANMessageProcessor.CAN_MSG_ID_START_STREAM);
                }
            }
            else
            {
                foreach (var node in _nodes)
                {
                    node.CanService.StartStream((TransmissionRate)_settings.TransmissionRateCode);
                }
            }
        }

        public void StopStreamAll()
        {
            foreach (var node in _nodes)
            {
                node.CanService.StopAllStreams();
            }
        }

        public void SetVehicleMode(VehicleMode mode)
        {
            if (CurrentMode == mode && _nodes.Count > 0) return;
            
            DisconnectAll();
            Initialize(mode);
            
            CurrentMode = mode;
            _logger.LogInfo($"SystemManager initialized in {mode} mode", "SystemManager");
        }

        public void SetBrakeModeAll(bool isBrakeMode)
        {
            foreach (var node in _nodes)
            {
                node.CanService.SwitchSystemMode(isBrakeMode ? SystemMode.Brake : SystemMode.Weight);
            }
        }

        public void Dispose()
        {
            DisconnectAll();
        }

        private ushort GetBitrateValue(CanBaudRate baudRate)
        {
            return baudRate switch
            {
                CanBaudRate.Bps125k => 125,
                CanBaudRate.Bps250k => 250,
                CanBaudRate.Bps500k => 500,
                CanBaudRate.Bps1M => 1000,
                _ => 250
            };
        }
    }
}

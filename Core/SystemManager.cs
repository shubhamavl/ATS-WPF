using System;
using System.Collections.Generic;
using ATS_WPF.Models;
using ATS_WPF.Adapters;
using ATS_WPF.Services;
using ATS_WPF.Services.Interfaces;

namespace ATS_WPF.Core
{
    public class SystemManager : ISystemManager, IDisposable
    {
        private readonly List<CANNode> _nodes = new();
        private readonly List<AxleSystem> _axles = new();
        private readonly ISettingsService _settingsService;
        private readonly IDataLoggerService _dataLogger;

        public VehicleMode CurrentMode { get; private set; }
        public IReadOnlyList<CANNode> PhysicalNodes => _nodes.AsReadOnly();
        public IReadOnlyList<AxleSystem> LogicalAxles => _axles.AsReadOnly();

        public SystemManager(ISettingsService settingsService, IDataLoggerService dataLogger)
        {
            _settingsService = settingsService;
            _dataLogger = dataLogger;
        }

        public void Initialize(VehicleMode mode)
        {
            CurrentMode = mode;
            Cleanup();

            var settings = _settingsService.Settings;

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
        }

        private CANNode CreateNode(string nodeId, string portName)
        {
            var canService = new CANService(); // Create dedicated service per node
            // Note: Settings may need to supply specific port info per CANService instance later
            return new CANNode(nodeId, canService);
        }

        private AxleSystem CreateAxle(AxleType axleType, CANNode node)
        {
            // Create dedicated TareManager and WeightProcessor for this axle
            var tareManager = new TareManager(axleType);
            tareManager.LoadFromFile(); // Will need update to support separate files

            var weightProcessor = new WeightProcessor(axleType, CurrentMode, node.CanService, tareManager, _settingsService, _dataLogger);
            
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
        }

        public void ConnectAll()
        {
            foreach (var node in _nodes)
            {
                string port = node.NodeId == "Right" ? _settingsService.Settings.RightComPort : 
                              (node.NodeId == "Left" ? _settingsService.Settings.LeftComPort : _settingsService.Settings.ComPort);
                              
                var config = new UsbSerialCanAdapterConfig
                {
                    PortName = port,
                    BitrateKbps = (ushort)_settingsService.Settings.CanBaudRate, // Correctly use settings
                    SerialBaudRate = 2000000
                };
                
                node.CanService.Connect(config, out _);
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
                    _nodes[0].CanService.StartStream(_settingsService.Settings.TransmissionRate, CANMessageProcessor.CAN_MSG_ID_START_STREAM);
                }
            }
            else
            {
                foreach (var node in _nodes)
                {
                    node.CanService.StartStream(_settingsService.Settings.TransmissionRate);
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
            
            // Persist to settings
            _settingsService.Settings.VehicleMode = mode;
            _settingsService.SaveSettings();
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
    }
}

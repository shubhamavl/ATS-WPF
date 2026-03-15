# Unified ATS Weight System

A professional Windows WPF application for real-time weight monitoring, calibration, and measurement across multiple vehicle modes (Two-Wheeler, LMV, and HMV).

## 🚀 Features

### Core Functionality
- **Multi-Vehicle Support**: Single application for Two-Wheeler, LMV (Light Mobility Vehicle), and HMV (High Mobility Vehicle) testing.
- **Dynamic Dashboard**: Automatically adapts UI elements, stream controls, and logic based on the selected `VehicleMode`.
- **HMV Dual-Node Logic**: Support for heavy vehicles using two independent CAN nodes and dual COM ports.
- **LMV Logical Streams**: Smart routing for single-node systems with multiplexed Left/Right measurement streams.
- **Multi-Point Calibration**: Advanced least-squares regression with mode-aware persistence (Internal vs. ADS1115).
- **Scalable Tare Management**: Dictionary-based tare system for handling multiple ADC modes across all axles.
- **Real-time DSP**: Integrated EMA and SMA filters for smooth, stable weight readings even at 1kHz sampling.
- **Automatic Updates**: Integrated `ATS_Updater` for seamless background deployment.

### Technical Excellence
- **MVVM Architecture**: Clean separation of concerns for maintainability and testability.
- **Asynchronous I/O**: Multi-threaded CAN communication and data logging to ensure UI responsiveness.
- **Portable Deployment**: Self-contained single-file executables (.NET 8 runtime included).
- **Comprehensive Logging**: Separate Production (diagnostic) and Data (measurement) logging systems.

## 📋 System Requirements

- **OS**: Windows 10/11 (64-bit)
- **Framework**: .NET 8.0 (included in portable ZIP)
- **Hardware**: Compatible USB-CAN adapter (PCAN or USB-CAN-A)
- **Deployment**: Portable (No installation required)

## 🔧 Getting Started

1. **Download**: Get the latest `ATS_WPF_Portable.zip` from GitHub Releases.
2. **Extract**: Unzip to any folder.
3. **Run**: Launch `ATS_WPF.exe`.
4. **Connect**:
   - Select your **Vehicle Mode**.
   - For **Two-Wheeler/LMV**: Select one COM port.
   - For **HMV**: Select Left and Right COM ports.
5. **Calibrate**: Perform multi-point calibration for each axle in the Calibration tab.

## 🔄 Version Information

- **Current Version**: 0.1.0 (Baseline Unified Release)
- **Release Date**: 2026-03-09
- **Namespace**: `ATS_WPF`

## 📚 Documentation

- [Architecture Overview](docs/ARCHITECTURE.md) - Deep dive into the MVVM pattern and system design.
- [CAN Protocol](docs/CAN_PROTOCOL.md) - Technical details of the binary communication protocol.
- [Calibration & Math](docs/CALIBRATION.md) - Detailed explanation of regression and measurement logic.
- [Developer Guide](docs/DEVELOPER_GUIDE.md) - Setup instructions and coding standards for contributors.
- [FAQ](docs/FAQ.md) - Frequently asked questions and troubleshooting.
- [User Guide](USER_GUIDE.md) - Instructions for end-users on operating the dashboard.

## 📞 Support

For technical support or feature requests, please refer to the [FAQ](docs/FAQ.md) or open an issue on GitHub.

---

**Unified ATS** - *Precision Measurement for Every Fleet*

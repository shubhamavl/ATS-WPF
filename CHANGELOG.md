# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-03-09

### Added
- **Unified ATS Architecture**: Initial release of the unified codebase supporting Two-Wheeler, LMV, and HMV vehicle types.
- **Multi-Axle Measurement System**: Flexible abstraction for nodes and axles allowing independent measurements.
- **Dynamic Dashboard UI**: Adaptive interface that adjusts based on the selected vehicle mode.
- **Dual-Node HMV Support**: Support for high-mobility vehicles with multiple CAN nodes and COM ports.
- **Scalable Tare Management**: Dictionary-based tare system for handling multiple ADC modes across axles.
- **Portable Deployment**: Self-contained single-file publish configuration for easy distribution.
- **Automatic Updater**: Integrated `ATS_Updater` for seamless background application updates.
- **Immutable Data Modeling**: Used C# records for high-performance, thread-safe weight data processing.

# Developer Guide

## Development Environment Setup

### Requirements
- **Visual Studio 2022** (with .NET Desktop Development workload)
- **.NET 8.0 SDK**
- **Git**

### Getting the Code
1. Clone the repository:
   ```bash
   git clone https://github.com/your-repo/ATS_WPF.git
   ```
2. Open `ATS_WPF.sln` in Visual Studio.

## Project Structure

- **ATS_WPF (Main Project)**
  - `Core/`: Application lifecycle, DI container, and converters.
  - `Models/`: UI-specific data models.
  - `Services/`: Implementation of logging, settings, and navigation.
  - `ViewModels/`: Application logic and UI state management.
  - `Views/`: XAML definitions for windows and controls.
- **CAN_Engine (Library)**
  - `Adapters/`: Driver implementations for various CAN hardware.
  - `Core/`: High-level system management and math.
  - `Models/`: Core domain models (Weight, Calibration, Enums).
  - `Services/`: Low-level communication and data processing.

## Building the Project

### Using Visual Studio
- Set `ATS_WPF` as the startup project.
- Press `F5` to build and run in Debug mode.

### Using the Build Script
The project includes a `build-portable.bat` script that creates a self-contained, single-file executable:
```batch
build-portable.bat
```
The output will be located in `bin/Release/net8.0-windows/win-x64/publish/`.

## Coding Guidelines

- **MVVM**: Do not put business logic in the View code-behind (`.xaml.cs`). Use ViewModels.
- **Async/Await**: Use asynchronous programming for all I/O operations (file, serial, network) to keep the UI responsive.
- **Logging**: Use `ProductionLogger` for diagnostic info and `DataLogger` for measurement data.
- **Naming**: Follow standard C# naming conventions (PascalCase for classes/methods, camelCase for local variables).

## Testing
- Ensure that any changes to the `CAN_Engine` are verified with appropriate unit tests.
- Verify UI changes across different `VehicleMode` settings in the `DashboardViewModel`.

## Deployment
The application is deployed as a **Portable App**. It does not require installation and stores all its configuration, calibration, and log data in a `Data/` subfolder relative to the executable.

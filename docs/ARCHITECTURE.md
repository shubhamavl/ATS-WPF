# System Architecture

## Overview
The Unified ATS Weight System is built using a decoupled architecture that separates the graphical user interface (GUI) from the core hardware communication and data processing logic. The project is divided into two main components:

1.  **ATS_WPF**: The main application project, responsible for the user interface, application life cycle, and high-level service orchestration.
2.  **CAN_Engine**: A specialized class library that handles CAN bus communication, raw data processing, calibration mathematics, and data logging.

## MVVM Pattern
The application strictly follows the **Model-View-ViewModel (MVVM)** design pattern to ensure maintainability, testability, and a clear separation of concerns.

-   **View**: Defined in XAML and code-behind (`Views/`). Responsible for the visual representation and user interaction.
-   **ViewModel**: Located in `ViewModels/`. Acts as a bridge between the View and the Model. It exposes data through properties and functionality through commands.
-   **Model**: Located in `Models/` (and `CAN_Engine/Models/`). Represents the data structures and business logic of the application.

## CAN_Engine Library
The `CAN_Engine` is the "brain" of the system. It is designed to be independent of the WPF framework, allowing for potential reuse in other project types (e.g., console apps or web services).

### Key Components:
-   **SystemManager**: The central orchestrator for hardware nodes. It manages the lifecycle of physical CAN connections and maps them to logical axles.
-   **CANService**: Handles the low-level serial/CAN communication. It supports multiple adapters (USB-CAN-A, PCAN, RS485).
-   **WeightProcessor**: A high-performance, multi-threaded processor that transforms raw ADC counts into calibrated weight values.
-   **TareManager**: Manages persistent zero-offsets (tare) for various sensor configurations.
-   **LinearCalibration**: Implements the mathematical models for converting raw signals to physical units using regression or piecewise logic.

## Vehicle Mode Logic
The `SystemManager` adapts the system's logical structure based on the selected `VehicleMode`:

| Vehicle Mode | Physical Nodes | Logical Axles | Communication Strategy |
| :--- | :--- | :--- | :--- |
| **Two-Wheeler** | 1 | 1 (Total) | Single stream from one node. |
| **LMV** | 1 | 2 (Left/Right) | Multiplexed streams via hardware relays. |
| **HMV** | 2 | 2 (Left/Right) | Dual independent nodes and ports (or shared bus). |

## Dependency Injection
The application uses a `ServiceRegistry` (located in `Core/`) to manage service lifetimes and handle dependency injection. This allows ViewModels to receive necessary services (like `ICANService` or `ISettingsService`) via their constructors, promoting a loosely coupled design.

## Data Flow
1.  **Capture**: `CANService` receives raw bytes from the serial port and decodes them into `CANMessage` objects.
2.  **Dispatch**: `CANMessageProcessor` identifies the message type, and `CANEventDispatcher` fires specific events.
3.  **Process**: `WeightProcessor` receives raw ADC data, applies calibration and filters, and calculates the final tared weight.
4.  **Display**: The UI (via `DashboardViewModel`) polls the `WeightProcessor` for the latest data and updates the screen.

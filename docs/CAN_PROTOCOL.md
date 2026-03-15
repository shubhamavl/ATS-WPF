# CAN Communication Protocol

## Overview
The ATS system uses a custom binary protocol over a USB-to-CAN adapter (USB-CAN-A). The protocol is optimized for high-speed streaming of weight data (up to 1kHz) while maintaining low latency for control commands.

## Frame Format (USB-Serial)
The serial communication with the USB-CAN-A adapter uses a fixed 20-byte frame format for both transmission and reception.

| Byte Index | Field | Description |
| :--- | :--- | :--- |
| 0 | Header | Always `0xAA` |
| 1 | Type/Length | Bit 7-4: Frame Type, Bit 3-0: Data Length |
| 2 | ID Low | CAN ID (bits 0-7) |
| 3 | ID High | CAN ID (bits 8-10) |
| 4-11 | Data | 8 bytes of CAN data payload |
| 12-19 | Footer/Padding | Reserved for future use |

## RS485 / Virtual CAN Framing
When using the RS485 transport, the system uses a 14-byte "Virtual CAN" frame format:

| Byte Index | Field | Description |
| :--- | :--- | :--- |
| 0 | Header | `0xAA` |
| 1 | ID High | CAN ID (bits 8-10) |
| 2 | ID Low | CAN ID (bits 0-7) |
| 3 | DLC | Data Length Code (0-8) |
| 4-11 | Data | 8 bytes of CAN data payload |
| 12 | CRC | XOR of bytes 0-11 |
| 13 | Footer | `0x55` |

## Semantic Message IDs
The system uses specific CAN IDs to categorize different types of messages:

| ID (Hex) | Name | Direction | Description |
| :--- | :--- | :--- | :--- |
| `0x200` | `TOTAL_RAW_DATA` | In | Raw ADC value (2 bytes, Little Endian) |
| `0x300` | `SYSTEM_STATUS` | In | ADC Mode, Relay State, Error Flags |
| `0x302` | `SYS_PERF` | In | CAN TX frequency and ADC sampling rate |
| `0x301` | `VERSION_RESPONSE` | In | Firmware version string |
| `0x040` | `START_STREAM` | Out | Start data streaming (Data: `0x01`=100Hz, `0x03`=1kHz) |
| `0x044` | `STOP_ALL_STREAMS` | Out | Stop all data streaming |
| `0x032` | `STATUS_REQUEST` | Out | Request current system status |
| `0x030` | `MODE_INTERNAL` | Out | Switch to STM32 Internal ADC |
| `0x031` | `MODE_ADS1115` | Out | Switch to ADS1115 External ADC |
| `0x050` | `SET_SYSTEM_MODE` | Out | Set mode (`0x00`=Weight, `0x01`=Brake) |

## HMV ID Shifting
For HMV (Heavy Mobility Vehicle) modes where two CAN nodes might exist on the same physical bus, the system uses an ID shifting logic to avoid collisions:

- **Node 1 (Left)**: Uses the base IDs listed above.
- **Node 2 (Right)**: Uses an offset of `+0x80`.
  - Example: Raw Data for Right side is `0x280`.
  - Example: Status Request for Right side is `0x0B2`.

## Streaming Control
The `START_STREAM` (0x040) command accepts a single byte payload to set the transmission frequency:

- `0x01`: 100 Hz
- `0x02`: 500 Hz
- `0x03`: 1000 Hz (1 kHz)
- `0x05`: 1 Hz (Diagnostic mode)

## Error Handling
The `SYSTEM_STATUS` (0x300) message contains error flags that the application monitors:
- **Bit 0**: ADC Error
- **Bit 1**: CAN Bus Warning
- **Bit 2**: Internal Buffer Overflow
- **Bit 3**: Sensor Disconnected

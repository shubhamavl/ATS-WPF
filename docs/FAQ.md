# Frequently Asked Questions (FAQ)

## Hardware and Connection

### Q: Which CAN adapters are supported?
**A:** The system primarily supports the **USB-CAN-A** (serial-based) adapter. It also has preliminary support for **PEAK-System PCAN-USB** adapters and serial **RS485** communication.

### Q: Why is my status light red?
**A:** A red status light indicates that the application has not received any valid CAN messages for more than 5 seconds. Check your cable connections, ensure the STM32 is powered, and verify that the correct COM port is selected in Settings.

### Q: The COM port list is empty. What should I do?
**A:** Ensure you have installed the **CH341** drivers for the USB-CAN-A adapter. If the drivers are installed, try unplugging and re-plugging the device.

---

## Calibration and Weight

### Q: Why am I seeing "Calibrate first" instead of a weight?
**A:** The application requires a valid calibration profile to be saved before it can display weight. Go to the **Calibration** tab and perform at least a single-point calibration.

### Q: My weight readings are negative. Is this a bug?
**A:** No. If the current load is less than the load present when you clicked **Tare**, the weight will be negative. Click **Reset Tare** or perform a new **Tare** with an empty platform.

### Q: The weight is "jumping around" on the screen.
**A:** This is usually due to mechanical vibration or electrical noise. Go to **Settings** and increase the **Filter Alpha** (for EMA) or increase the **Window Size** (for SMA) to stabilize the reading.

---

## Deployment and Updates

### Q: Where are my calibration files and logs stored?
**A:** Since the app is portable, everything is stored in the `/Data` folder located in the same directory as the `ATS_WPF.exe`.

### Q: How do I update the application?
**A:** The application has an integrated updater. If an update is available, you will see a notification in the status bar. The updater will download the latest release from GitHub and replace the executable automatically.

### Q: Can I run this on Windows 7?
**A:** No. The application is built on **.NET 8**, which requires **Windows 10 (1607 or later)** or **Windows 11**.

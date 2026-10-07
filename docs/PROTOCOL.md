# Nevermor Q590-SD / SD-Q590 numeric display protocol

Investigated OEM software: Digital 1.0.0.3, `DeviceDriver.exe`. The protocol was recovered by inspecting the vendor executable using its shipped symbols and reading the active HID descriptors. No OEM application code or firmware is redistributed in this repository.

| HID property | Required value |
|---|---|
| Vendor ID | 0x1A2C |
| Product ID | 0x4E84 |
| Usage page | 0xFF01 |
| Usage | 1 |
| Feature report ID | 7 |
| Tested report length, including ID | 64 bytes |

The tested Windows interface is MI_01 / Col07 and may identify itself as `USB Gaming Keyboard`, manufacturer `SEMICO`. The app enumerates HID interfaces and checks attributes, usage, and the report ID. It refuses to send when multiple matching interfaces are found. No serial COM connection is used.

## Update frame

Send a feature report through `HidD_SetFeature`:

| Byte | Value |
|---|---|
| 0 | 0x07, report ID |
| 1 | Lower number: tens digit |
| 2 | Lower number: ones digit |
| 3 | Upper number: thousands digit |
| 4 | Upper number: hundreds digit |
| 5 | Upper number: tens digit |
| 6 | Upper number: ones digit |
| 7 onward | Zero |

Digits are numeric values 0–9, not ASCII or segment masks. Lower range: 0–99; upper range: 0–9999. For lower 53 and upper 913:

```text
07 05 03 00 09 01 03 00 00 ...
```

The OEM implementation supplied 65 bytes, although the tested descriptor reports 64 including the report ID. The independent controller uses the descriptor length, and successful 64-byte sends were verified on hardware. The owner visually confirmed new CPU-load/GPU-temperature values.

Clock 13:15 is represented as 1315 in 24-hour mode or 0115 in 12-hour mode. A date is DDMM. The known update command provides six decimal digits; fixed printed labels, punctuation, leading-zero rendering, and letter support are not controlled by this frame.

## Scope

Only the numeric-update feature report is implemented. No firmware update or unknown device commands are sent. A reported CH340/CH341 VID 1A86 PID 7523 device is not the interface selected by the investigated OEM update routine. Compatibility with other revisions or other coolers using the same USB chip has not been established.

The OEM app originally obtained CPU temperature and motherboard fan RPM from its LibreHardwareMonitor helper. The independent controller does not launch that helper or read its shared memory.

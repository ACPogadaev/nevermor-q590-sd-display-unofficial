# Nevermor Q590-SD / SD-Q590 — independent unofficial display software

**Проект является независимым неофициальным программным обеспечением и не связан с Nevermor. Все товарные знаки принадлежат их владельцам. Использование осуществляется на собственный риск. Перед применением на реальном оборудовании необходимо проверить совместимость и требования производителя.**

Independent, unofficial software, not affiliated with Nevermor. All trademarks belong to their owners. Use at your own risk; verify compatibility and manufacturer requirements before using on real hardware.

**Nevermor Display 1.3.1** is a configurable Windows controller for the numeric display on the **Nevermor Q590-SD**, also sold as **Nevermor SD-Q590**. CPU temperature, fan RPM, CPU/GPU load, memory usage, a 12/24-hour clock, seconds, a date, or your own fixed number can be selected for the two fields.

**Программа для дисплея кулера Nevermor Q590-SD / SD-Q590.** Исходники и единый установщик: приложение + библиотека датчиков + подписанный драйвер. Запросы «Q590-SD программа», «Nevermor SD-Q590 драйвер дисплея», «Q590-SD display software» относятся к этому проекту.

[Русская инструкция](docs/README.ru.md) · [Почему Defender блокирует Digital](docs/DIGITAL-BLOCKING.ru.md) · [USB protocol](docs/PROTOCOL.md) · [Validation](docs/VALIDATION.md) · [Changelog](CHANGELOG.md)

## Download and install

Download [NevermorDisplay-1.3.1-Setup.exe](https://github.com/ACPogadaev/nevermor-q590-sd-display-unofficial/releases/download/v1.3.1/NevermorDisplay-1.3.1-Setup.exe) from the [release page](https://github.com/ACPogadaev/nevermor-q590-sd-display-unofficial/releases/tag/v1.3.1). **Code → Download ZIP** downloads source code.

1. Exit Digital from its tray menu. Close other hardware-monitoring utilities for the initial check.
2. Run the installer and approve the normal Windows UAC prompt. No components are downloaded during installation.
3. Restart Windows if the installer requests it. A USB transmission error after setup also warrants a restart; one tested device recovered that way.
4. Start Nevermor Display. Select the upper/lower values and refresh interval, then Save. Closing the window hides it in the tray; **Выход** exits it.

Windows 10/11 x64, .NET Framework 4.7.2 or newer. The UI is currently Russian. Installed to `%ProgramFiles%\Nevermor Display`; settings are in `%LOCALAPPDATA%\NevermorDisplay`. Default: CPU load above, CPU temperature below, 2-second refresh, Windows clock format, autostart off.

The installer includes **LibreHardwareMonitor 0.9.6** and the unchanged official **PawnIO 2.2.0** installer. No AIDA64, Digital, Python, or separate monitoring application is required. PawnIO is a third-party driver by namazso. Its original installer and components are signed; our controller and combined installer are currently **unsigned**. The combined installer verifies the original PawnIO hash and signature. It does not disable Windows security or use the unrestricted driver edition.

## Display choices

| Metric | Upper: 4 digits | Lower: 2 digits |
|---|---|---|
| CPU temperature / CPU load | Yes | Yes |
| CPU clock in MHz | Yes | — |
| GPU temperature / GPU load | Yes | Yes |
| GPU clock in MHz | Yes | — |
| RAM usage: percent / whole GB | Yes | Yes |
| Selected motherboard fan RPM | Yes | — |
| Clock: Windows / 12-hour / 24-hour | Yes | — |
| Clock seconds | Yes | Yes |
| Date: DDMM | Yes | — |
| Fixed number | Yes | Yes |

Upper range 0–9999; lower range 0–99. Values are rounded/clamped, so 100% appears as 99 below. The physical labels and °C symbol remain. The implemented command sends digits; AM/PM is shown in the app preview, not as letters on the cooler. Select **12 часов** for 13:15 → 1:15 PM. A fan must be selected explicitly when the CPU channel is not identified. Missing sensor values pause transmission rather than becoming false zeroes.

## Low overhead and autostart

CPU load and RAM use come from Windows APIs. NVIDIA metrics use the installed GPU driver's NVML. Other hardware groups are opened only when needed. One polling operation runs at a time; interval is 1–60 seconds, usually 2–5 seconds for sensors. The hidden window stops repainting. Normal operation makes no network requests and starts no separate polling service. The UI shows poll time and memory; measurements and limits are in [validation](docs/VALIDATION.md).

CPU temperature, CPU clock and motherboard fan sensors require administrator rights and PawnIO. Manual launch requests elevation when those metrics are selected. Autostart can use a current-user elevated scheduled task with a 10-second login delay. Autostart stays off until enabled. Disable Digital's autostart separately; this app pauses sending if `DeviceDriver.exe` is detected.

## Why Digital stopped reading sensors

In the investigated installation, Defender quarantined Digital's old WinRing0 sensor driver as `VulnerableDriver:WinNT/Winring0`. Microsoft's advisory confirms this is a known vulnerable driver. Our first local build shared that dependency; 1.3.1 uses PawnIO instead. See the [evidence, explanation and limits](docs/DIGITAL-BLOCKING.ru.md). No antivirus exclusions are added by this project.

## USB compatibility

Tested display: **HID VID 1A2C / PID 4E84**, usage page FF01, usage 1, Feature Report 7. It may appear as USB Gaming Keyboard / SEMICO. The app validates the vendor collection and refuses ambiguous matches. CH340/CH341 **VID 1A86 / PID 7523** is not used by the investigated display path. See [protocol notes](docs/PROTOCOL.md). One Q590-SD owner confirmed CPU temperature and physical display operation after installing 1.3.1 and restarting; fan-channel mapping and other revisions require verification.

## Build from source

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

The Windows .NET Framework C# compiler is used; Visual Studio is not required. Build-time downloads restore pinned official NuGet packages and the official PawnIO installer; normal installation is offline. `-Offline` is available after populating the cache. `build/app` contains the app; `dist` contains the single installer and SHA-256 checksum. Self-tests do not send USB reports or enable autostart.

## License

Our controller and installer source: [MIT](LICENSE). Dependencies have separate licenses; signed PawnIO binaries are proprietary and redistributed unmodified under the publisher's redistribution terms. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md), [dependencies.json](dependencies.json), and [licenses](licenses). OEM executables, firmware, personal settings and diagnostic logs are not distributed.

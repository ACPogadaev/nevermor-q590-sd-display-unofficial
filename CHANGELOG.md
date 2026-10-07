# Changelog

## 1.3.1 — 2026-10-07

- Offline single installer: controller, LibreHardwareMonitor 0.9.6 and signed official PawnIO 2.2.0, no separate AIDA64/Digital requirement.
- Replace the old WinRing0 sensor backend. Preserve Windows security settings and verify the original driver installer's hash/signature.
- Install, update and uninstall support; preserve settings and an existing compatible shared driver.
- Fix Cyrillic Start menu shortcuts through IShellLinkW/IPersistFile on an STA thread. Optional shortcut failure no longer aborts installation.
- CPU temperature confirmed by the owner; physical USB display recovered after a Windows restart. USB protocol unchanged.
- Document Defender's confirmed WinRing0 detection and distinguish it from a separate transient USB failure.

## Earlier local versions

1.1 added Windows/12/24-hour clocks, AM/PM preview, seconds and DDMM dates. Initial versions provided selectable metrics, interval, tray mode and autostart. The first local sensor backend used WinRing0 and is superseded by 1.3.1.

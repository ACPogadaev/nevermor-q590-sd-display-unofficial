# Validation and limits

Hardware: Nevermor Q590-SD display, Windows x64, Ryzen 7 7700, RTX 4070 SUPER. This is validation on one physical device, not a compatibility claim for all board/cooler revisions.

## Completed on the initial local builds

- Numeric USB HID reports accepted: 16 update snapshots over 30 seconds at a 2-second interval. The owner confirmed changed digits on the cooler.
- Version 1.1 recheck: 8 successful update snapshots over 8.017 seconds, 12-hour clock above and NVIDIA temperature below, 1-second interval.
- C# build, digit order/ranges/zero padding, settings validation/XML roundtrip, and scheduled-task XML/quoting.
- Runtime metric changes, pause/resume, preview mode without USB writes, unavailable sensor handling, graceful tray exit, and visual UI review.
- 12/24-hour conversion, 13:15 → 1:15 PM, midnight/noon/evening, Windows culture patterns and quoted literals, AM/PM preview, old XML migration, and date/seconds from a single time sample.

## Initial tray resource measurement

CPU load + NVIDIA temperature, 2-second refresh, actual USB updates, hidden window, 16 logical CPUs:

| Measurement | Result |
|---|---|
| Duration | 45.032 seconds |
| Process CPU time | 0.015625 seconds |
| Percent of one logical CPU | 0.0347% |
| Percent of total CPU | 0.0022% |
| Working set | 72.58 MB |
| Private bytes | 52.89 MB |

The initial local build used the OEM-distributed LibreHardwareMonitor DLL. The public build restores the official NuGet dependency instead. The preceding short measurement is not a new measurement of the public dependency build, and did not include CPU-temperature or motherboard-fan monitoring.

## Remaining hardware/account checks

CPU temperature, CPU clock, and fan RPM with elevation were not independently validated in the replacement app. The OEM utility successfully displayed temperature/RPM, according to its owner. Availability of equivalent readings through the official public dependency can vary by board/driver access.

Ordinary/elevated autostart code and XML are present; actual login autostart under the interactive user's account has not been validated. The restricted development account denied writes to HKCU Run. No user autostart was enabled by the tests.

The built-in self-test does not write USB reports or register autostart. Hardware benchmarking with `--send` is an explicit diagnostic action; the OEM display writer should be stopped first.

## Version 1.3.1 — owner verification, 2026-10-07

The owner installed the combined package and supplied a screenshot showing CPU temperature 44 °C. The initial USB send failed with Win32 31 even in clock mode; after restarting Windows, the owner confirmed operation. No alternative report length was deployed and the USB protocol was not changed. The exact transient failure cause remains unknown.

The screenshot showed about 0.99 ms per polling operation and 61 MB memory at a 2-second interval. This is a single observation, not a controlled CPU-usage benchmark. Earlier measurements above apply only to their stated builds/configurations.

Installer tests cover safe ZIP paths and hashes, original driver signature, version/exit-code handling, file deployment rollback, preservation of unknown files, real Cyrillic shortcut save/readback from an MTA worker and continuation after optional shortcut failure. CPU fan channel mapping and actual login autostart remain unverified. The earlier CPU-temperature validation limitation above is superseded by this owner confirmation, not by a claim that all sensor modes work on every board.

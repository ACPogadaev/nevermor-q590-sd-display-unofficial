# Third-party components

Nevermor Display controller and installer source are MIT-licensed. Dependencies and drivers keep their own licenses.

| Component | Version | Terms / upstream |
|---|---|---|
| LibreHardwareMonitorLib | 0.9.6 | MPL 2.0; [source and release](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/tag/v0.9.6) |
| DiskInfoToolkit | 1.1.2 | MPL 2.0; NuGet manifest in licenses |
| BlackSharp.Core | 1.0.7 | MPL 2.0; NuGet manifest in licenses |
| RAMSPDToolkit-NDD | 1.4.2 | MPL 2.0; NuGet manifest in licenses |
| HidSharp | 2.6.4 | Apache 2.0; see licenses/HidSharp.LICENSE.txt |
| System.* support libraries | Pinned per dependencies.json | MIT; package manifests, licenses and notices in licenses |
| PawnIO signed official installer | 2.2.0 | Proprietary binary, redistributed UNMODIFIED with permission to redistribute the installer; [publisher terms](https://github.com/namazso/PawnIO.Modules/wiki/Using-PawnIO-Modules) |

Exact NuGet versions, selected frameworks, DLLs and archive SHA-256 hashes are in `dependencies.json`. Source/notice references are preserved in the original package manifests. MPL 2.0 text is included in `licenses/LICENSE-MPL-2.0.txt`. No dependency binary is modified by this project.

PawnIO is a third-party component by namazso, not developed or owned by this project. Its signed binary distribution terms differ from the GPL-2.0-with-exception / LGPL-2.1 source-edition terms. See `licenses/PawnIO-distribution.txt`; this project's MIT license does not relicense PawnIO. Only the ordinary signed installer is bundled; no unrestricted version or modified modules.

NVIDIA NVML comes from the user's installed GPU driver and is not redistributed. Windows DLLs are not redistributed. Digital OEM software, symbols, firmware and OEM DLLs are not included.

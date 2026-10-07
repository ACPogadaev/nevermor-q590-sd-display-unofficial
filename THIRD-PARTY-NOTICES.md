# Third-party dependencies

Nevermor Display source is MIT-licensed. That license does not replace the licenses of the unmodified dependencies bundled with the portable app.

| NuGet package | Pinned version | Purpose / license source |
|---|---|---|
| LibreHardwareMonitorLib | 0.9.3 | Optional CPU/GPU/motherboard monitoring; MPL 2.0. [Package](https://www.nuget.org/packages/LibreHardwareMonitorLib/0.9.3), [exact package source commit](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/67ff1135158c669fab2cbcdff2086ffa57fe621c) |
| HidSharp | 2.1.0 | LibreHardwareMonitor dependency, Apache 2.0, Copyright 2010-2019 James F. Bellinger. [Package metadata](https://www.nuget.org/packages/HidSharp/2.1.0), [publisher license](https://www.zer7.com/files/oss/hidsharp/LICENSE.txt) |
| System.Management | 8.0.0 | MIT, Microsoft. On .NET Framework the package resolves to the framework's existing System.Management assembly. [Package metadata](https://www.nuget.org/packages/System.Management/8.0.0) |
| System.CodeDom | 8.0.0 | MIT, Microsoft, framework-compatible support library. [Package metadata](https://www.nuget.org/packages/System.CodeDom/8.0.0) |

The restore script uses official NuGet packages, copies their compatible .NET Framework DLLs without modification, and retains package manifests and shipped license/notice files in the portable app’s `licenses` folder. The MPL 2.0 text is included as `licenses/LICENSE-LibreHardwareMonitor.txt`. Package manifests contain upstream copyright, license and repository references; upstream source and notices remain available at those references.

NVIDIA NVML is provided by the NVIDIA driver already installed on the machine; it is not redistributed. Windows system DLLs are not redistributed. OEM Digital executables, symbols, firmware, and DLLs are not bundled in the public release.

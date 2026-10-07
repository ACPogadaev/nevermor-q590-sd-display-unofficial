$ErrorActionPreference='Stop'
$applicationPath=Split-Path -Parent $PSScriptRoot
$sources=@(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | ForEach-Object {$_.FullName})
& (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe') /nologo /target:winexe /platform:x64 /optimize+ /debug- /warn:4 ("/out:"+(Join-Path $applicationPath 'NevermorDisplay.exe')) ("/win32manifest:"+(Join-Path $PSScriptRoot 'app.manifest')) ("/reference:"+(Join-Path $applicationPath 'LibreHardwareMonitorLib.dll')) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:Microsoft.CSharp.dll $sources
if($LASTEXITCODE -ne 0){throw 'Compilation failed'}

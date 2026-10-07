param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$repositoryPath = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'restore.ps1') -Offline:$Offline
$applicationPath = Join-Path $repositoryPath 'build\app'
$dependencyPath = Join-Path $repositoryPath 'build\dependencies'
New-Item -ItemType Directory -Path $applicationPath -Force | Out-Null
Copy-Item -Path (Join-Path $dependencyPath '*.dll') -Destination $applicationPath -Force
Copy-Item -LiteralPath (Join-Path $dependencyPath 'licenses') -Destination $applicationPath -Recurse -Force
foreach ($document in @('LICENSE','THIRD-PARTY-NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $repositoryPath $document) -Destination $applicationPath -Force
}
Copy-Item -LiteralPath (Join-Path $repositoryPath 'docs\README.ru.md') -Destination (Join-Path $applicationPath 'README.ru.md') -Force
Copy-Item -LiteralPath (Join-Path $repositoryPath 'README.md') -Destination (Join-Path $applicationPath 'README.en.md') -Force
Copy-Item -LiteralPath (Join-Path $repositoryPath 'docs') -Destination $applicationPath -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repositoryPath 'assets') -Destination $applicationPath -Recurse -Force
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sourcePath = Join-Path $repositoryPath 'source'
$sources = @(Get-ChildItem -LiteralPath $sourcePath -Filter '*.cs' -File | ForEach-Object {$_.FullName})
& $compilerPath /nologo /target:winexe /platform:x64 /optimize+ /debug- /warn:4 ("/out:" + (Join-Path $applicationPath 'NevermorDisplay.exe')) ("/win32manifest:" + (Join-Path $sourcePath 'app.manifest')) ("/reference:" + (Join-Path $dependencyPath 'LibreHardwareMonitorLib.dll')) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:Microsoft.CSharp.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
Write-Host 'Built build/app/NevermorDisplay.exe (no private-path PDB).'

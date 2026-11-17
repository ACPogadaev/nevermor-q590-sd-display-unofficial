param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw '.NET Framework x64 C# compiler is required.'
}

$destination = New-Item -ItemType Directory -Force -Path $OutputDirectory
$binary = Join-Path $destination.FullName 'NevermorKeyboardSwitch.exe'
$manifest = Join-Path $PSScriptRoot 'app.manifest'
$program = Join-Path $PSScriptRoot 'Program.cs'
$devices = Join-Path $PSScriptRoot 'Devices.cs'
$compilerArguments = @(
    '/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/warnaserror+', '/codepage:65001'
    "/out:$binary", "/win32manifest:$manifest"
    '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll'
    $program, $devices
)
& $compiler @compilerArguments
if ($LASTEXITCODE -ne 0) {
    throw 'Compilation failed.'
}

Write-Output $binary

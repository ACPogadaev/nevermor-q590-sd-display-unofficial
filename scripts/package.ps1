param([switch]$Offline,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repositoryPath=Split-Path -Parent $PSScriptRoot
$applicationPath=Join-Path $repositoryPath 'build\app'
if(!(Test-Path -LiteralPath (Join-Path $applicationPath 'NevermorDisplay.exe'))){throw 'Build first.'}
foreach($name in @('settings.xml','diagnostics.txt','last-error.txt','installed.flag')){
    if(Test-Path -LiteralPath (Join-Path $applicationPath $name)){throw ('Refusing to package local state: '+$name)}
}
$driver=Join-Path $repositoryPath '.packages\PawnIO_setup-2.2.0.exe'
if(!(Test-Path -LiteralPath $driver)){
    if($Offline){throw 'Missing cached official PawnIO installer'}
    Invoke-WebRequest -Uri 'https://github.com/namazso/PawnIO.Setup/releases/download/2.2.0/PawnIO_setup.exe' -OutFile $driver -UseBasicParsing -TimeoutSec 60
}
$distributionPath=if($OutputDirectory){[IO.Path]::GetFullPath($OutputDirectory)}else{Join-Path $repositoryPath 'dist'}
New-Item -ItemType Directory -Path $distributionPath -Force | Out-Null
$output=Join-Path $distributionPath 'NevermorDisplay-1.3.1-Setup.exe'
& (Join-Path $applicationPath 'installer\build.ps1') -DriverInstaller $driver -BuildDirectory (Join-Path $repositoryPath 'build\setup') -OutputPath $output
Copy-Item -LiteralPath ($output+'.SHA256.txt') -Destination (Join-Path $distributionPath 'SHA256SUMS.txt') -Force

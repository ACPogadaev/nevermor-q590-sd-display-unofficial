$ErrorActionPreference = 'Stop'
$repositoryPath = Split-Path -Parent $PSScriptRoot
$applicationPath = Join-Path $repositoryPath 'build\app\NevermorDisplay.exe'
$reportPath = Join-Path $repositoryPath 'build\self-test.txt'
if (!(Test-Path -LiteralPath $applicationPath)) { throw 'Run scripts/build.ps1 first.' }
$testProcess = Start-Process -FilePath $applicationPath -ArgumentList @('--self-test',('"' + $reportPath + '"')) -WindowStyle Hidden -Wait -PassThru
if ($testProcess.ExitCode -ne 0) { throw 'Self-test failed. See build/app/last-error.txt.' }
Get-Content -LiteralPath $reportPath

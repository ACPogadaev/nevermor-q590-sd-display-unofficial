$ErrorActionPreference = 'Stop'
$repositoryPath = Split-Path -Parent $PSScriptRoot
$applicationPath = Join-Path $repositoryPath 'build\app'
if (!(Test-Path -LiteralPath (Join-Path $applicationPath 'NevermorDisplay.exe'))) { throw 'Build first.' }
foreach ($privateFile in @('settings.xml','settings.xml.tmp','last-error.txt','diagnostics.txt')) {
    if (Test-Path -LiteralPath (Join-Path $applicationPath $privateFile)) { throw ('Refusing to package local state: ' + $privateFile) }
}
if (Get-ChildItem -LiteralPath $applicationPath -Recurse -Filter '*.pdb' -File) { throw 'Refusing to package debug symbols.' }
$distributionPath = Join-Path $repositoryPath 'dist'
New-Item -ItemType Directory -Path $distributionPath -Force | Out-Null
$archivePath = Join-Path $distributionPath 'NevermorDisplay-v1.1.0-win-x64.zip'
Compress-Archive -Path (Join-Path $applicationPath '*') -DestinationPath $archivePath -Force
$hash = Get-FileHash -LiteralPath $archivePath -Algorithm SHA256
($hash.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($archivePath)) | Set-Content -LiteralPath (Join-Path $distributionPath 'SHA256SUMS.txt') -Encoding ascii
Write-Host ('Packaged: ' + $archivePath)

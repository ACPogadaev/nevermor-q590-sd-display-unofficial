param([switch]$Offline)
$ErrorActionPreference='Stop'
$repositoryPath=Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'restore.ps1') -Offline:$Offline
$applicationPath=Join-Path $repositoryPath 'build\app'
New-Item -ItemType Directory -Path $applicationPath -Force | Out-Null
Copy-Item -Path (Join-Path $repositoryPath 'build\dependencies\*.dll') -Destination $applicationPath -Force
foreach($folder in @('source','installer','licenses')){
    Copy-Item -LiteralPath (Join-Path $repositoryPath $folder) -Destination $applicationPath -Recurse -Force
}
foreach($file in @('LICENSE','THIRD-PARTY-NOTICES.md','dependencies.json')){
    Copy-Item -LiteralPath (Join-Path $repositoryPath $file) -Destination $applicationPath -Force
}
Copy-Item -LiteralPath (Join-Path $repositoryPath 'docs\README.ru.md') -Destination (Join-Path $applicationPath 'README.ru.md') -Force
Copy-Item -LiteralPath (Join-Path $repositoryPath 'README.md') -Destination (Join-Path $applicationPath 'README.en.md') -Force
Copy-Item -LiteralPath (Join-Path $repositoryPath 'source\NevermorDisplay.exe.config') -Destination $applicationPath -Force
& (Join-Path $applicationPath 'source\build.ps1')
Write-Host 'Built build/app/NevermorDisplay.exe without private-path debug symbols.'

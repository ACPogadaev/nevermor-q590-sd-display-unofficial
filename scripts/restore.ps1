param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$repositoryPath = Split-Path -Parent $PSScriptRoot
$cachePath = Join-Path $repositoryPath '.packages'
$dependencyPath = Join-Path $repositoryPath 'build\dependencies'
$noticePath = Join-Path $dependencyPath 'licenses'
New-Item -ItemType Directory -Path $cachePath,$dependencyPath,$noticePath -Force | Out-Null
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$packages = @(
    @{ Id='LibreHardwareMonitorLib'; Version='0.9.3'; Frameworks=@('net472') },
    @{ Id='HidSharp'; Version='2.1.0'; Frameworks=@('net35','net45','net40','net20') },
    @{ Id='System.Management'; Version='8.0.0'; Frameworks=@('net462','net472','net461') },
    @{ Id='System.CodeDom'; Version='8.0.0'; Frameworks=@('net462','net472','net461') }
)
foreach ($package in $packages) {
    $key = $package.Id + '.' + $package.Version
    $archivePath = Join-Path $cachePath ($key + '.zip')
    $packagePath = Join-Path $cachePath $key
    if (!(Test-Path -LiteralPath $archivePath)) {
        if ($Offline) { throw ('Missing cached package: ' + $key) }
        $downloadUrl = 'https://www.nuget.org/api/v2/package/' + $package.Id + '/' + $package.Version
        Write-Host ('Downloading official NuGet package: ' + $key)
        Invoke-WebRequest -Uri $downloadUrl -OutFile $archivePath -UseBasicParsing -TimeoutSec 45
    }
    if (!(Test-Path -LiteralPath (Join-Path $packagePath ($package.Id + '.nuspec')))) {
        Expand-Archive -LiteralPath $archivePath -DestinationPath $packagePath -Force
    }
    $libraryPath = $null
    foreach ($framework in $package.Frameworks) {
        $candidate = Join-Path $packagePath ('lib\' + $framework)
        if (Test-Path -LiteralPath $candidate) { $libraryPath = $candidate; break }
    }
    if (!$libraryPath) { throw ('No compatible .NET Framework library in ' + $key) }
    Copy-Item -Path (Join-Path $libraryPath '*.dll') -Destination $dependencyPath -Force
    $packageNotices = Join-Path $noticePath $key
    New-Item -ItemType Directory -Path $packageNotices -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $packagePath ($package.Id + '.nuspec')) -Destination $packageNotices -Force
    Get-ChildItem -LiteralPath $packagePath -File | Where-Object { $_.Name -match '(?i)license|copying|notice' } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $packageNotices -Force
    }
    [xml]$metadata = Get-Content -LiteralPath (Join-Path $packagePath ($package.Id + '.nuspec'))
    $namespace = New-Object Xml.XmlNamespaceManager($metadata.NameTable)
    $namespace.AddNamespace('p',$metadata.DocumentElement.NamespaceURI)
    $licenseNode = $metadata.SelectSingleNode('/p:package/p:metadata/p:license',$namespace)
    if ($licenseNode -and $licenseNode.type -eq 'file') {
        $licenseFile = Join-Path $packagePath $licenseNode.InnerText
        $checkedLicensePath = [IO.Path]::GetFullPath($licenseFile)
        if (!$checkedLicensePath.StartsWith([IO.Path]::GetFullPath($packagePath) + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid package license path' }
        Copy-Item -LiteralPath $checkedLicensePath -Destination $packageNotices -Force
    }
}
Copy-Item -Path (Join-Path $repositoryPath 'licenses\*') -Destination $noticePath -Force
Write-Host 'Official dependencies restored.'

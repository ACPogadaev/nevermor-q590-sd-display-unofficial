param([switch]$Offline)
$ErrorActionPreference='Stop'
$repositoryPath=Split-Path -Parent $PSScriptRoot
$cachePath=Join-Path $repositoryPath '.packages'
$dependencyPath=Join-Path $repositoryPath 'build\dependencies'
New-Item -ItemType Directory -Path $cachePath,$dependencyPath -Force | Out-Null
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.IO.Compression.FileSystem
$packages=Get-Content -LiteralPath (Join-Path $repositoryPath 'dependencies.json') -Raw | ConvertFrom-Json
foreach($package in $packages){
    $key=$package.package+'.'+$package.version
    $archivePath=Join-Path $cachePath ($key+'.nupkg')
    if(!(Test-Path -LiteralPath $archivePath)){
        if($Offline){throw ('Missing cached package: '+$key)}
        $url='https://api.nuget.org/v3-flatcontainer/'+$package.package.ToLowerInvariant()+'/'+$package.version+'/'+$key.ToLowerInvariant()+'.nupkg'
        Invoke-WebRequest -Uri $url -OutFile $archivePath -UseBasicParsing -TimeoutSec 60
    }
    if((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $package.sha256){throw ('Package checksum mismatch: '+$key)}
    $archive=[IO.Compression.ZipFile]::OpenRead($archivePath)
    try{
        foreach($dll in $package.dlls){
            $entry=$archive.GetEntry('runtimes/win-x64/lib/'+$package.framework+'/'+$dll)
            if(!$entry){$entry=$archive.GetEntry('lib/'+$package.framework+'/'+$dll)}
            if(!$entry){throw ('Missing framework DLL: '+$key+'/'+$dll)}
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $dependencyPath $dll),$true)
        }
    }finally{$archive.Dispose()}
}
Write-Host 'Pinned official NuGet dependencies verified and restored.'

param(
    [Parameter(Mandatory=$true)][string]$DriverInstaller,
    [Parameter(Mandatory=$true)][string]$BuildDirectory,
    [string]$OutputPath
)
$ErrorActionPreference='Stop'
$applicationPath=Split-Path -Parent $PSScriptRoot
if(!$OutputPath){$OutputPath=Join-Path (Split-Path -Parent $applicationPath) 'NevermorDisplay-1.3.1-Setup.exe'}
$expectedDriverHash='1f519a22e47187f70a1379a48ca604981c4fcf694f4e65b734aaa74a9fba3032'
if((Get-FileHash -LiteralPath $DriverInstaller -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedDriverHash){throw 'PawnIO checksum mismatch'}
$signature=Get-AuthenticodeSignature -LiteralPath $DriverInstaller
if($signature.Status -ne 'Valid'){throw ('PawnIO signature invalid: '+$signature.Status)}
& (Join-Path $applicationPath 'source\build.ps1')
New-Item -ItemType Directory -Path $BuildDirectory -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$payloadPath=Join-Path $BuildDirectory 'payload.zip'
$stream=[IO.File]::Open($payloadPath,[IO.FileMode]::Create,[IO.FileAccess]::ReadWrite)
$archive=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$false)
try {
    $appFiles=@(Get-ChildItem -LiteralPath $applicationPath -File | Where-Object { $_.Name -notin @('settings.xml','diagnostics.txt','last-error.txt','installed.flag') -and ($_.Extension -in @('.dll','.exe','.config','.md','.json') -or $_.Name -eq 'LICENSE') })
    foreach($directoryName in @('source','licenses')){$appFiles+=@(Get-ChildItem -LiteralPath (Join-Path $applicationPath $directoryName) -File -Recurse)}
    $appFiles+=@(Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object {$_.Name -in @('Setup.cs','AssemblyInfo.cs','setup.manifest','build.ps1','Tests.cs')})
    foreach($file in $appFiles | Sort-Object FullName){
        $relative=$file.FullName.Substring($applicationPath.Length+1).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,('app/'+$relative),[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    $marker=$archive.CreateEntry('app/installed.flag')
    $writer=[IO.StreamWriter]::new($marker.Open(),[Text.UTF8Encoding]::new($false))
    try{$writer.Write('9cd89d32-5fa0-4215-b788-30dfedec272a')}finally{$writer.Dispose()}
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$DriverInstaller,'driver/PawnIO_setup.exe',[IO.Compression.CompressionLevel]::Optimal) | Out-Null
}finally{$archive.Dispose();$stream.Dispose()}
$payloadHash=(Get-FileHash -LiteralPath $payloadPath -Algorithm SHA256).Hash.ToLowerInvariant()
$buildInfo=Join-Path $BuildDirectory 'BuildInfo.cs'
[IO.File]::WriteAllText($buildInfo,('namespace NevermorSetup { internal static class BuildInfo { internal const string PayloadHash="'+$payloadHash+'"; } }'),[Text.UTF8Encoding]::new($false))
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 /main:NevermorSetup.Program ("/out:"+$OutputPath) ("/win32manifest:"+(Join-Path $PSScriptRoot 'setup.manifest')) ("/resource:"+$payloadPath+',payload.zip') /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:Microsoft.CSharp.dll (Join-Path $PSScriptRoot 'Setup.cs') (Join-Path $PSScriptRoot 'AssemblyInfo.cs') $buildInfo
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'}
$setupHash=(Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($OutputPath+'.SHA256.txt'),($setupHash+'  '+[IO.Path]::GetFileName($OutputPath)+[Environment]::NewLine),[Text.UTF8Encoding]::new($false))
Write-Output ('Built '+$OutputPath+'; embedded payload SHA256 '+$payloadHash)

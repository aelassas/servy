# chocolateyinstall.ps1 contains URLs and checksums for the latest release available on GitHub releases page.
# URLs and checksums are auto-updated on each new release on GitHub through choco.yml workflow.

$ErrorActionPreference = 'Stop'

$packageName   = 'servy'
$installerType = 'exe'
$checksumType  = 'sha256'
$silentArgs    = '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /CLOSEAPPLICATIONS /NOCANCEL'

$url64         = 'https://github.com/aelassas/servy/releases/download/v9.1/servy-9.1-x64-installer.exe'
$checksum64    = 'EE309E2E6024828500552A53325E1D5255AC629B9E9BB1E809BEE9C48BCBD92E'

$urlArm64      = 'https://github.com/aelassas/servy/releases/download/v9.1/servy-9.1-arm64-installer.exe'
$checksumArm64 = '6934CF1179D5AB2CE5C1651C8D3D829D1C5660CB66FCFB4E47A4B14AA40BF635'

# Detect OS architecture dynamically
$isArm64 = ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') -or ($env:PROCESSOR_ARCHITEW6432 -eq 'ARM64')

if ($isArm64) {
    $targetUrl      = $urlArm64
    $targetChecksum = $checksumArm64
} else {
    $targetUrl      = $url64
    $targetChecksum = $checksum64
}

$installArgs = @{
    PackageName    = $packageName
    FileType       = $installerType
    SilentArgs     = $silentArgs
    Url            = $targetUrl
    Checksum       = $targetChecksum
    ChecksumType   = $checksumType
}

Install-ChocolateyPackage @installArgs

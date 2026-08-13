# chocolateyinstall.ps1 contains URLs and checksums for the latest release available on GitHub releases page.
# URLs and checksums are auto-updated on each new release on GitHub through choco.yml workflow.

$ErrorActionPreference = 'Stop'

$packageName   = 'servy'
$installerType = 'exe'
$checksumType  = 'sha256'
$silentArgs    = '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /CLOSEAPPLICATIONS /NOCANCEL'

$url64         = 'https://github.com/aelassas/servy/releases/download/v9.4/servy-9.4-x64-installer.exe'
$checksum64    = '6DD204A4D1656976E49E8460F6C264DC3E9826C6722D8CE9C9544F659BEC860A'

$urlArm64      = 'https://github.com/aelassas/servy/releases/download/v9.4/servy-9.4-arm64-installer.exe'
$checksumArm64 = '05FBB8067964AD854C729C5045692F1F05738B3AAADFF6D830CFC883134474D4'

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

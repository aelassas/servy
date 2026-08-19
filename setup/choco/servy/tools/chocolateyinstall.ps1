# chocolateyinstall.ps1 contains URLs and checksums for the latest release available on GitHub releases page.
# URLs and checksums are auto-updated on each new release on GitHub through choco.yml workflow.

$ErrorActionPreference = 'Stop'

$packageName   = 'servy'
$installerType = 'exe'
$checksumType  = 'sha256'
$silentArgs    = '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /CLOSEAPPLICATIONS /NOCANCEL'

$url64         = 'https://github.com/aelassas/servy/releases/download/v9.6/servy-9.6-x64-installer.exe'
$checksum64    = 'FA4E2D6F469FD3B8254F522998C3FADABDD23AE7ABFC3786D6FF35969532669A'

$urlArm64      = 'https://github.com/aelassas/servy/releases/download/v9.6/servy-9.6-arm64-installer.exe'
$checksumArm64 = 'E0EDC95ECD331E416E649E3E89BE30467800D2C0AC47DC683B2FD89E67856793'

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

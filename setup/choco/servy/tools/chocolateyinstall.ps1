# chocolateyinstall.ps1 contains URLs and checksums for the latest release available on GitHub releases page.
# URLs and checksums are auto-updated on each new release on GitHub through choco.yml workflow.

$ErrorActionPreference = 'Stop'

$packageName   = 'servy'
$installerType = 'exe'
$checksumType  = 'sha256'
$silentArgs    = '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /CLOSEAPPLICATIONS /NOCANCEL'

$url64         = 'https://github.com/aelassas/servy/releases/download/v10.4/servy-10.4-x64-installer.exe'
$checksum64    = '4431B346DD213BF0FC644D177D44DF377C02049386A5F3F288EAD7028E9E1561'

$urlArm64      = 'https://github.com/aelassas/servy/releases/download/v10.4/servy-10.4-arm64-installer.exe'
$checksumArm64 = 'A2FFF60096F53AC3C0CC9F0BF12169093DFBE43A98715BEAD23CC864AC910F7D'

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

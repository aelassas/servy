# chocolateyinstall.ps1 contains URLs and checksums for the latest release available on GitHub releases page.
# URLs and checksums are auto-updated on each new release on GitHub through choco.yml workflow.

$ErrorActionPreference = 'Stop'

$packageName   = 'servy'
$installerType = 'exe'
$checksumType  = 'sha256'
$silentArgs    = '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /CLOSEAPPLICATIONS /NOCANCEL'

$url64         = 'https://github.com/aelassas/servy/releases/download/v10.5/servy-10.5-x64-installer.exe'
$checksum64    = '7D91ED8B41A7D542DD292DA0A2B503E5EBFF4F558E8DAFD1C002670C1B1A5399'

$urlArm64      = 'https://github.com/aelassas/servy/releases/download/v10.5/servy-10.5-arm64-installer.exe'
$checksumArm64 = '856AC0E8236F216EFD55BDA33258BBB8720F4503FDD2CCFD50B58AEF55774EF3'

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

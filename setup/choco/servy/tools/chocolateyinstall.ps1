$ErrorActionPreference = 'Stop'

$packageName    = 'servy'
$toolsDir       = Split-Path -Parent $MyInvocation.MyCommand.Definition
$installerType  = 'exe'
\ = 'https://github.com/aelassas/servy/releases/download/v1.1/servy-1.1-net8.0-x64-installer.exe'
\ = 'A2F73BFCEF4F81DE1F785EC8164CC33A3498862B92DA5952797F04E7EC8FD7E2'
$checksumType64 = 'sha256'
$silentArgs     = '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES'

# Use splatting for better readability
$installParams = @{
    PackageName  = $packageName
    FileType     = $installerType
    SilentArgs   = $silentArgs
    Url          = $url64
    Checksum     = $checksum64
    ChecksumType = $checksumType64
}

Install-ChocolateyPackage @installParams



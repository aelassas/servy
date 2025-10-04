$ErrorActionPreference = 'Stop'

$packageName    = 'servy'
$toolsDir       = Split-Path -Parent $MyInvocation.MyCommand.Definition
$installerType  = 'exe'
$url64 = 'https://github.com/aelassas/servy/releases/download/v1.8/servy-1.8-x64-installer.exe'
$checksum64 = '8CDCEC1320A16CBA243B84096F68E8A377975B2B521A3FA2E33C759972EE1F34'
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

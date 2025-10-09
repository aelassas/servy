$ErrorActionPreference = 'Stop'

$packageName    = 'servy'
$toolsDir       = Split-Path -Parent $MyInvocation.MyCommand.Definition
$installerType  = 'exe'
$url64 = 'https://github.com/aelassas/servy/releases/download/v2.2/servy-2.2-x64-installer.exe'
$checksum64 = '983156A09BC17E1EBEBDA5EA90E364427550B8F2FF2D56A20617E63A9465040A'
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

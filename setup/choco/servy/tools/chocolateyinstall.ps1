$ErrorActionPreference = 'Stop'

$packageName    = 'servy'
$toolsDir       = Split-Path -Parent $MyInvocation.MyCommand.Definition
$installerType  = 'exe'
$url64 = 'https://github.com/aelassas/servy/releases/download/v1.3/servy-1.3-net8.0-x64-installer.exe'
$checksum64 = 'C0174BFA502F900885ECAABB52DB0DD1C6C066C9874E6DCCA4941E1206BF481D'
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

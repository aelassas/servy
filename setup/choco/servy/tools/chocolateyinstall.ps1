$ErrorActionPreference = 'Stop'

$packageName    = 'servy'
$toolsDir       = Split-Path -Parent $MyInvocation.MyCommand.Definition
$installerType  = 'exe'
$url64 = 'https://github.com/aelassas/servy/releases/download/v2.3/servy-2.3-x64-installer.exe'
$checksum64 = '35CC536F575CE4DFE9993B919BB04E6832CDECCD59AFAB6B2A8EA696DC533A80'
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

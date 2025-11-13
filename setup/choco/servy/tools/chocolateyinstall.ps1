$ErrorActionPreference = 'Stop'

$packageName   = 'servy'
$installerType = 'exe'
$url64         = 'https://github.com/aelassas/servy/releases/download/v3.4/servy-3.4-x64-installer.exe'
$checksum64    = '364A9B1A137EF1C46453ED534A7AE3AB73AC3567B6F4F5E72CD9A7B4122ED9F2'
$checksumType  = 'sha256'
$silentArgs    = '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES /SP- /CLOSEAPPLICATIONS /NOCANCEL'

$installArgs = @{
    PackageName     = $packageName
    FileType        = $installerType
    SilentArgs      = $silentArgs
    Url64bit        = $url64
    Checksum64      = $checksum64
    ChecksumType64  = $checksumType
}

Install-ChocolateyPackage @installArgs

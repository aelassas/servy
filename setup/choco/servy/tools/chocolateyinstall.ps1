$ErrorActionPreference = 'Stop'

$packageName   = 'servy'
$installerType = 'exe'
$url64         = 'https://github.com/aelassas/servy/releases/download/v7.2/servy-7.2-x64-installer.exe'
$checksum64    = 'CCA530E4C05A04A665B5BFD72E64AE158F547ACE8B0DAD646AF69D07E182FBC0'
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

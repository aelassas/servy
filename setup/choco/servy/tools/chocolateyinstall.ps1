$ErrorActionPreference = 'Stop'

$packageName   = 'servy'
$installerType = 'exe'
$url64         = 'https://github.com/aelassas/servy/releases/download/v4.9/servy-4.9-x64-installer.exe'
$checksum64    = '8A29AE76FE904F347C99D217C0F5EA4C4C7416E52BA0919C6C20453AFDE9BC46'
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

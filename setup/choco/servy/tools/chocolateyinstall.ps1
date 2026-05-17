# chocolateyinstall.ps1contains $url64 and $checksum64 of the latest release available on GitHub releases page.
# URL and checksum are auto updated on each new release on GitHub by choco.yml workflow.

$ErrorActionPreference = 'Stop'

$packageName   = 'servy'
$installerType = 'exe'
$url64         = 'https://github.com/aelassas/servy/releases/download/v8.4/servy-8.4-x64-installer.exe'
$checksum64    = '61E989201EE977864EAF5A6B2E2EB046B1C6F6F629CECA2C324477116F3E7C51'
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

# chocolateyinstall.ps1 contains URL and checksum of the latest release available on GitHub releases page.
# URL and checksum are auto-updated on each new release on GitHub through choco.yml workflow.

$ErrorActionPreference = 'Stop'

$packageName   = 'servy'
$installerType = 'exe'
$url64         = 'https://github.com/aelassas/servy/releases/download/v8.5/servy-8.5-x64-installer.exe'
$checksum64    = 'C66ED0661D25853131347783A7F3F94997EDBC62B1F9BFB3F70DAD0DB128CCC8'
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

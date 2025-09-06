$ErrorActionPreference = 'Stop'

$packageName    = 'servy'
$toolsDir       = Split-Path -Parent $MyInvocation.MyCommand.Definition
$installerType  = 'exe'
$url64          = 'https://github.com/aelassas/servy/releases/download/v1.0/servy-1.0-net8.0-x64-installer.exe'
$checksum64     = '4AFD0680789126FD43FB79AA92E4AF914B443C514852AFC158CAC1C7590D2DAD'
$checksumType64 = 'sha256'
$silentArgs     = '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES'

Install-ChocolateyPackage $packageName $installerType $silentArgs $url64 `
  -Checksum $checksum64 -ChecksumType $checksumType64

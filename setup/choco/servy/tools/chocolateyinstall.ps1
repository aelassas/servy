$ErrorActionPreference = 'Stop'

$packageName    = 'servy'
$installerType  = 'exe'
$url64          = 'https://github.com/aelassas/servy/releases/download/v1.0/servy-1.0-net48-x64-installer.exe'
$checksum64     = 'F510EAE335BFCD0D68BB81F3B5C0EDD17703EC4C7C55E36183EF86CF5464E002'
$checksumType64 = 'sha256'
$silentArgs     = '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES'

Install-ChocolateyPackage $packageName $installerType $silentArgs $url64 `
  -Checksum $checksum64 -ChecksumType $checksumType64

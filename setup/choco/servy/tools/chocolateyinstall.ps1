$ErrorActionPreference = 'Stop'

$packageName    = 'servy'
$installerType  = 'exe'
$url64          = 'https://github.com/aelassas/servy/releases/download/v1.0/servy-1.0-net48-x64-installer.exe'
$checksum64     = '31AC48876DA5994D2D708F5C22715247B8814C9C162F7B2E232CF2B6ED6E3C01'
$checksumType64 = 'sha256'
$silentArgs     = '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES'

Install-ChocolateyPackage $packageName $installerType $silentArgs $url64 `
  -Checksum $checksum64 -ChecksumType $checksumType64

$ErrorActionPreference = 'Stop'

$packageName    = 'servy'
$installerType  = 'exe'
$url64          = 'https://github.com/aelassas/servy/releases/download/v1.0/servy-1.0-net48-x64-installer.exe'
$checksum64     = '02D653D58ED1CB143A3B197252E864B2FC6CDE5FC8431AF1DB72D2B1F36815D2'
$checksumType64 = 'sha256'
$silentArgs     = '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES'

Install-ChocolateyPackage $packageName $installerType $silentArgs $url64 `
  -Checksum $checksum64 -ChecksumType $checksumType64

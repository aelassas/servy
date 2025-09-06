$ErrorActionPreference = 'Stop'

$packageName    = 'servy'
$installerType  = 'exe'
$url64          = 'https://github.com/aelassas/servy/releases/download/v1.0/servy-1.0-net48-x64-installer.exe'
$checksum64     = 'EBB67865BBA6331DF5906390E6DB124F4F572E5D1EBB245571A2FD3529C0F183'
$checksumType64 = 'sha256'
$silentArgs     = '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES'

Install-ChocolateyPackage $packageName $installerType $silentArgs $url64 `
  -Checksum $checksum64 -ChecksumType $checksumType64

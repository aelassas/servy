$ErrorActionPreference = 'Stop'

$packageName    = 'servy'
$installerType  = 'exe'
$url64          = 'https://github.com/aelassas/servy/releases/download/v1.0/servy-1.0-net48-x64-installer.exe'
$checksum64     = 'CE60EAC89E1ACAE4BC9E7DBFBBEBB9EA78CB2DF8BC76D1039DBD2B8081C2AE08'
$checksumType64 = 'sha256'
$silentArgs     = '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES'

Install-ChocolateyPackage $packageName $installerType $silentArgs $url64 `
  -Checksum $checksum64 -ChecksumType $checksumType64

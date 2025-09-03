$ErrorActionPreference = 'Stop'

$packageName   = 'servy'
$installerType = 'exe'

# Chocolatey remembers where the installer was downloaded to
# but for uninstall, we point to the installed product in Programs & Features
$uninstallKey = Get-UninstallRegistryKey -SoftwareName 'Servy*'

if ($uninstallKey) {
    $silentArgs = '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES'
    $file = $uninstallKey.UninstallString

    if ($file -and (Test-Path $file)) {
        Uninstall-ChocolateyPackage $packageName $installerType $silentArgs $file
    } else {
        Write-Warning "Uninstall string not found or file missing for $packageName."
    }
} else {
    Write-Warning "$packageName is not installed or no uninstall key found."
}

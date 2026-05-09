$ErrorActionPreference = 'Stop'

$packageName   = 'servy'
$installerType = 'exe'

# Chocolatey remembers where the installer was downloaded to
# but for uninstall, we point to the installed product in Programs & Features
$uninstallKeys = @(Get-UninstallRegistryKey -SoftwareName 'Servy*')

if (-not $uninstallKeys) {
    Write-Warning "$packageName is not installed or no uninstall key found."
    return
}

if ($uninstallKeys.Count -gt 1) {
    Write-Warning "Multiple Servy registrations found ($($uninstallKeys.Count)). Uninstalling each:"
}

foreach ($key in $uninstallKeys) {
    $file = $key.UninstallString
    if ($file -and (Test-Path $file)) {
        Uninstall-ChocolateyPackage $packageName $installerType '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES' $file
    } else {
        Write-Warning "Uninstall string not found or file missing for $($key.DisplayName)."
    }
}

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
    $raw = [string]$key.UninstallString
    if ([string]::IsNullOrWhiteSpace($raw)) {
        Write-Warning "Empty UninstallString for $($key.DisplayName)."
        continue
    }

    # Strip surrounding quotes and trim any trailing arguments
    if ($raw -match '^"([^"]+)"\s*(.*)$') {
        $file      = $matches[1]
        $extraArgs = $matches[2]
    } else {
        # Unquoted - take everything up to the first space as the path
        $parts     = $raw -split ' ', 2
        $file      = $parts[0]
        $extraArgs = if ($parts.Length -gt 1) { $parts[1] } else { '' }
    }

    if (Test-Path $file) {
        Uninstall-ChocolateyPackage $packageName $installerType '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES' $file
    } else {
        Write-Warning "Uninstaller binary not found at '$file' for $($key.DisplayName)."
    }
}
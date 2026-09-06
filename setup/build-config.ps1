<#
.SYNOPSIS
    Centralized build configuration defaults for Servy publishing scripts.
.DESCRIPTION
    Provides a single source of truth for Version, TFM, and build environments
    to prevent drift across multiple orchestration scripts.
#>

function ConvertTo-NormalizedConfig {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Config
    )

    $normalized = @{}
    foreach ($key in $Config.Keys) {
        $val = $Config[$key]
        $normalized[$key] = if ($val -is [string]) { $val.Trim() } else { $val }
    }

    return $normalized
}

$rawConfig = @{
    Version            = "10.0"
    Tfm                = "net10.0-windows"
    BuildConfiguration = "Release"
    Runtime            = "win-x64" # "win-x64" or "win-arm64"
}

return (ConvertTo-NormalizedConfig -Config $rawConfig)

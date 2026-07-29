<#
.SYNOPSIS
    Centralized build configuration defaults for Servy publishing scripts.
.DESCRIPTION
    Provides a single source of truth for Version, TFM, and build environments
    to prevent drift across multiple orchestration scripts.
#>
$config = @{
    Version            = "8.9"
    Tfm                = "net10.0-windows"
    BuildConfiguration = "Release"
    Runtime            = "win-x64" # "win-x64" or "win-arm64"
}

# Ensure all string values are normalized across all downstream consumers
$normalized = @{}
foreach ($key in $config.Keys) {
    $val = $config[$key]
    $normalized[$key] = if ($val -is [string]) { $val.Trim() } else { $val }
}

return $normalized
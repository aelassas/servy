<#
.SYNOPSIS
    Centralized build configuration defaults for Servy publishing scripts.
.DESCRIPTION
    Provides a single source of truth for Version, TFM, and build environments
    to prevent drift across multiple orchestration scripts.
#>
@{
    Version            = "8.7"
    Tfm                = "net10.0-windows"
    BuildConfiguration = "Release"
    Runtime            = "win-x64" # "win-x64" or "win-arm64"
}
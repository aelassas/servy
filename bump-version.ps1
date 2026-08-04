#Requires -Version 5.0
<#
.SYNOPSIS
    Updates the version of Servy across scripts, AppConfig, and project files.

.DESCRIPTION
    This script updates the version of Servy in multiple locations:
    - setup\build-config.ps1   (Version hashtable key)
    - All *.csproj files recursively   (<Version>, <FileVersion>, <AssemblyVersion>)
    - src\Servy.CLI\Servy.psd1   (ModuleVersion)

.PARAMETER Version
    The new version to apply in 'Major.Minor' format (e.g., "8.0").

.PARAMETER DryRun
    If specified, previews the files that would be modified without performing any writes to disk.

.EXAMPLE
    .\bump-version.ps1 -Version 4.0
    .\bump-version.ps1 4.0

Updates all relevant files to version 4.0.

.EXAMPLE
    .\bump-version.ps1 -Version 4.0 -DryRun

Previews all version modifications that would be applied for version 4.0 without writing changes to disk.

.NOTES
    - The script overwrites files in-place.
    - Ensure you have backups or version control before running.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern("^\d+\.\d+$")]
    [string]$Version,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$script:HadFailure      = $false

# Base directory of the script
$baseDir = $PSScriptRoot

# ----------------------------------------------------------------------
# Dot-source shared helpers
# ----------------------------------------------------------------------
$helperFile = "Update-FileHelpers.ps1"
$helperPath = Join-Path $baseDir $helperFile

if (Test-Path $helperPath) {
    . $helperPath
} else {
    throw "Critical dependency missing: '$helperFile' was not found at '$helperPath'. Ensure the helper is in the same directory as this script."
}

# Statistics counters
$script:totalFilesScanned = 0
$script:filesModified     = 0
$script:totalReplacements = 0

# -----------------------------
# Convert short version to full versions
# -----------------------------
$fullVersion = "$Version.0"
$fileVersion = "$Version.0.0"

if ($DryRun) {
    Write-Host "DRY-RUN: Previewing Servy version update to $Version..." -ForegroundColor Yellow
} else {
    Write-Host "Updating Servy version to $Version..."
}

# -----------------------------
# 1. Update setup\build-config.ps1
# -----------------------------
$buildConfigPath = Join-Path $baseDir 'setup\build-config.ps1'
Update-FilesContent `
    -Files @($buildConfigPath) `
    -Pattern '(Version\s*=\s*")[^"]*(")' `
    -Replacement { param($m) "$($m.Groups[1].Value)$Version$($m.Groups[2].Value)" } `
    -ExpectMatch `
    -DryRun:$DryRun

# -----------------------------
# 2. Update all *.csproj files recursively
# -----------------------------
$csprojFiles = Get-ChildItem -Path $baseDir -Recurse -Filter *.csproj -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch $script:BuildArtifactExclusionRegex }

$csprojEdits = @(
    @{ Pattern = '(<Version(?:\s+[^>]*)?>)[^<]*(</Version>)';         Replacement = { param($m) "$($m.Groups[1].Value)$fullVersion$($m.Groups[2].Value)" } },
    @{ Pattern = '(<FileVersion(?:\s+[^>]*)?>)[^<]*(</FileVersion>)';     Replacement = { param($m) "$($m.Groups[1].Value)$fileVersion$($m.Groups[2].Value)" } },
    @{ Pattern = '(<AssemblyVersion(?:\s+[^>]*)?>)[^<]*(</AssemblyVersion>)'; Replacement = { param($m) "$($m.Groups[1].Value)$fileVersion$($m.Groups[2].Value)" } }
)

Update-FilesContent -Files $csprojFiles -Edits $csprojEdits -DryRun:$DryRun

# -----------------------------
# 3. Update src\Servy.CLI\Servy.psd1
# -----------------------------
$psd1Path = Join-Path $baseDir "src\Servy.CLI\Servy.psd1"

Update-FilesContent `
    -Files @($psd1Path) `
    -Pattern "(ModuleVersion\s*=\s*')[^']*(')" `
    -Replacement { param($m) "$($m.Groups[1].Value)$fullVersion$($m.Groups[2].Value)" } `
    -ExpectMatch `
    -DryRun:$DryRun

if ($script:HadFailure) {
    Write-Host "Version update process completed with errors." -ForegroundColor Red
    exit 1
}

if ($DryRun) {
    Write-Host "DRY-RUN: Version update preview completed successfully." -ForegroundColor Yellow
} else {
    Write-Host "All version updates completed successfully." -ForegroundColor Green
}
#Requires -Version 5.0
<#
.SYNOPSIS
    Updates .NET runtime target version across scripts, workflow, and project files.

.DESCRIPTION
    This script recursively updates `netX.Y` target framework versions 
    inside:
    - PowerShell scripts (*.ps1)
    - Inno Setup files (*.iss)
    - .csproj project files
    - .github/workflows/publish.yml
    - global.json

    Note: AppConfig.cs is intentionally not targeted because it derives the TFM 
    dynamically at runtime via assembly metadata (BuiltWithFramework).

    Use -DryRun to preview changes without modifying anything.

.PARAMETER Version
    The .NET runtime version (e.g. "10.0").

.PARAMETER SdkPatch
    The SDK feature-band/patch component appended to the global.json version (default "100"), producing e.g. "10.0.100".

.PARAMETER DryRun
    Shows what would change without writing to disk.

.EXAMPLE
    ./bump-runtime.ps1 -Version 10.0

.EXAMPLE
    ./bump-runtime.ps1 -Version 10.0 -SdkPatch 300

.EXAMPLE
    ./bump-runtime.ps1 10.0

.EXAMPLE
    ./bump-runtime.ps1 -Version 10.0 -DryRun
    Shows all changes without modifying files.

.EXAMPLE
    ./bump-runtime.ps1 10.0 -DryRun

.NOTES
    This script modifies files in-place unless -DryRun is used.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern("^\d+\.\d+$")]
    [string]$Version,
    [ValidatePattern("^\d+$")]
    [string]$SdkPatch = "100",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$script:HadFailure     = $false

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

# -----------------------------
# Variables
# -----------------------------
$currentVersionRegex = '(?<![A-Za-z0-9])net\d+\.\d+(?![A-Za-z0-9.])'
$netVersion = "net$Version"

Write-Host "Updating .NET runtime to $netVersion..." -ForegroundColor Cyan
if ($DryRun) { Write-Host "(Dry Run Mode - no files will be modified)" -ForegroundColor Yellow }

# Statistics counters
$script:totalFilesScanned = 0
$script:filesModified     = 0
$script:totalReplacements = 0

# ----------------------------------------------------------------------
# Execution Logic
# ----------------------------------------------------------------------

# 1. Bulk file updates (PowerShell, Inno, Projects)
$bulkFiles = Get-ChildItem -Path $baseDir -Recurse -Include *.ps1, *.iss, *.csproj -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch $script:BuildArtifactExclusionRegex }
Update-FilesContent -Files $bulkFiles -Pattern $currentVersionRegex -Replacement $netVersion -DryRun:$DryRun

# 2. Explicitly-targeted files that must contain the version pattern (publish.yml TFM env)
$workflowFiles = @($(Join-Path $baseDir ".github\workflows\publish.yml"))
Update-FilesContent -Files $workflowFiles -Pattern $currentVersionRegex -Replacement $netVersion -DryRun:$DryRun -ExpectMatch

# 3. Update global.json SDK version to match the new TFM major via regex to perfectly preserve original file formatting
$globalJsonFile = Join-Path $baseDir "global.json"
if (Test-Path $globalJsonFile) {
    $globalJsonPattern     = '("version"\s*:\s*")\d+\.\d+\.\d+'
    $globalJsonReplacement = "`${1}$Version.$SdkPatch"
    
    Update-FilesContent -Files @($globalJsonFile) -Pattern $globalJsonPattern -Replacement $globalJsonReplacement -DryRun:$DryRun -ExpectMatch
}

# -----------------------------
# Summary
# -----------------------------
Write-Host "`n========================================="
Write-Host "            SUMMARY"
Write-Host "========================================="
if ($DryRun) {
    Write-Host "Files scanned:                    $script:totalFilesScanned"
    Write-Host "Files that would be modified:     $script:filesModified"
    Write-Host "Replacements that would be made:  $script:totalReplacements"
} else {
    Write-Host "Files scanned:      $script:totalFilesScanned"
    Write-Host "Files modified:     $script:filesModified"
    Write-Host "Total replacements: $script:totalReplacements"
}

if ($script:HadFailure) {
    if ($DryRun) {
        Write-Host "`nDry run complete with errors. No files were modified." -ForegroundColor Yellow
    } else {
        Write-Host ".NET runtime migration to v$Version completed with errors." -ForegroundColor Red
    } 
    exit 1
}

if ($DryRun) {
    Write-Host "`nDry run complete. No files were modified." -ForegroundColor Yellow
} else {
    Write-Host "`n.NET runtime migration to v$Version successful." -ForegroundColor Green
}

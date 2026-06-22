#requires -Version 5.0
<#
.SYNOPSIS
    Updates .NET runtime target version across scripts, AppConfig, and project files.

.DESCRIPTION
    This script recursively updates `netX.Y` target framework versions 
    inside:
    - PowerShell scripts (*.ps1)
    - Inno Setup files (*.iss)
    - .csproj project files
    - src/Servy.Core/Config/AppConfig.cs
    - .github/workflows/*.yml
    - global.json

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

# -----------------------------
# Variables
# -----------------------------
$currentVersionRegex = '(?<![A-Za-z0-9])net\d+\.\d+(?![A-Za-z0-9.])'
$netVersion = "net$Version"
$baseDir = $PSScriptRoot

Write-Host "Updating .NET runtime to $netVersion..." -ForegroundColor Cyan
if ($DryRun) { Write-Host "(Dry Run Mode - no files will be modified)" -ForegroundColor Yellow }

# Statistics counters
$script:totalFilesScanned = 0
$script:filesModified     = 0
$script:totalReplacements = 0

# ----------------------------------------------------------------------
# Dot-source shared helpers
# ----------------------------------------------------------------------
$helperFile = "Get-FileEncoding.ps1"
$helperPath = Join-Path $baseDir $helperFile

if (Test-Path $helperPath) {
    . $helperPath
} else {
    throw "Critical dependency missing: '$helperFile' was not found at '$helperPath'. Ensure the helper is in the same directory as this script."
}

# ----------------------------------------------------------------------
# Helper: Update-Files
# ----------------------------------------------------------------------
function Update-Files {
    param(
        [Parameter(Mandatory)] $Files,
        [Parameter(Mandatory)] [string]$Pattern,
        [Parameter(Mandatory)] [string]$Replacement,
        [Parameter(Mandatory)] [bool]$DryRun,
        [switch]$ExpectMatch
    )

    foreach ($file in $Files) {
        if ($null -eq $file) { continue }
        $path = if ($file -is [string]) { $file } else { $file.FullName }
        
        if (-not (Test-Path $path)) {
            Write-Warning "Skipping missing file: $path"
            if ($ExpectMatch) {
                $script:HadFailure = $true
            }
            continue
        }

        $script:totalFilesScanned++

        try {
            $encoding = Get-FileEncoding $path
            $content = [System.IO.File]::ReadAllText($path, $encoding)

            $regexMatches = [regex]::Matches($content, $Pattern)
            $matchCount = $regexMatches.Count

            if ($matchCount -gt 0) {
                $script:filesModified++
                $script:totalReplacements += $matchCount

                if ($DryRun) {
                    Write-Host "DRY-RUN: Would update $path ($matchCount matches)" -ForegroundColor Gray
                } else {
                    $newContent = [regex]::Replace($content, $Pattern, $Replacement)
                    [System.IO.File]::WriteAllText($path, $newContent, $encoding)
                    Write-Host "UPDATED ($($encoding.BodyName)): $path" -ForegroundColor Green
                }
            } elseif ($ExpectMatch) {
                # A pattern missing condition on a targeted file is treated as an unrecoverable migration failure.
                Write-Warning "No version patterns matching '$Pattern' were located in explicitly-targeted path: $path"
                $script:HadFailure = $true
            }
        }
        catch {
            Write-Warning "Failed to update file: $path. $_"
            $script:HadFailure = $true
        }
    }
}

# ----------------------------------------------------------------------
# Execution Logic
# ----------------------------------------------------------------------

# 1. Bulk file updates (PowerShell, Inno, Projects)
$bulkFiles = Get-ChildItem -Path $baseDir -Recurse -Include *.ps1, *.iss, *.csproj -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj|packages|node_modules|\.git|TestResults)[\\/]' }
Update-Files -Files $bulkFiles -Pattern $currentVersionRegex -Replacement $netVersion -DryRun:$DryRun

# 2. Specific Config/Workflow updates (Safe Pathing & Broadened Workflows)
$workflowFiles = @($(Join-Path $baseDir ".github\workflows\publish.yml"))

# Explicit targets must match version patterns to proceed safely
Update-Files -Files $workflowFiles -Pattern $currentVersionRegex -Replacement $netVersion -DryRun:$DryRun -ExpectMatch

# 3. Update global.json SDK version to match the new TFM major via regex to perfectly preserve original file formatting
$globalJsonFile = Join-Path $baseDir "global.json"
if (Test-Path $globalJsonFile) {
    # Captures the JSON property context and ensures we do not cross boundaries or break numerical groupings
    $globalJsonPattern     = '("version"\s*:\s*")\d+\.\d+\.\d+'
    $globalJsonReplacement = "`${1}$Version.$SdkPatch"
    
    Update-Files -Files @($globalJsonFile) -Pattern $globalJsonPattern -Replacement $globalJsonReplacement -DryRun:$DryRun -ExpectMatch
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
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

Use -DryRun to preview changes without modifying anything.

.PARAMETER Version
The .NET runtime version (e.g. "10.0").

.PARAMETER DryRun
Shows what would change without writing to disk.

.EXAMPLE
./update-runtime.ps1 -Version 10.0

.EXAMPLE
./update-runtime.ps1 10.0

.EXAMPLE
./update-runtime.ps1 -Version 10.0 -DryRun
Shows all changes without modifying files.

.EXAMPLE
./update-runtime.ps1 10.0 -DryRun

.NOTES
This script modifies files in-place unless -DryRun is used.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern("^\d+\.\d+$")]
    [string]$Version,

    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# -----------------------------
# Variables
# -----------------------------
$currentVersionRegex = '(?<![A-Za-z0-9])net\d+\.\d+(?![A-Za-z0-9])'
$netVersion = "net$Version"
$baseDir = $PSScriptRoot

Write-Host "Updating .NET runtime to $netVersion..." -ForegroundColor Cyan
if ($DryRun) { Write-Host "(Dry Run Mode - no files will be modified)" -ForegroundColor Yellow }

# Statistics counters
$script:totalFilesScanned = 0
$script:filesModified     = 0
$script:totalReplacements = 0

# ----------------------------------------------------------------------
# Helper: Get-FileEncoding (Preserves UTF-8 BOM status)
# ----------------------------------------------------------------------
function Get-FileEncoding {
    param([string]$Path)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        return [System.Text.Encoding]::UTF8 # With BOM
    }
    return New-Object System.Text.UTF8Encoding($false) # Without BOM
}

# ----------------------------------------------------------------------
# Helper: Update-Files
# ----------------------------------------------------------------------
function Update-Files {
    param(
        [Parameter(Mandatory)] $Files,
        [Parameter(Mandatory)] [string]$Pattern,
        [Parameter(Mandatory)] [string]$Replacement,
        [Parameter(Mandatory)] [bool]$DryRun
    )

    foreach ($file in $Files) {
        if ($null -eq $file) { continue }
        $path = if ($file -is [string]) { $file } else { $file.FullName }
        
        if (-not (Test-Path $path)) {
            Write-Warning "Skipping missing file: $path"
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
            }
        }
        catch {
            Write-Error "Failed to update file: $path. $_"
        }
    }
}

# ----------------------------------------------------------------------
# Execution Logic
# ----------------------------------------------------------------------

# 1. Bulk file updates (PowerShell, Inno, Projects)
$bulkFiles = Get-ChildItem -Path $baseDir -Recurse -Include *.ps1, *.iss, *.csproj -ErrorAction SilentlyContinue
Update-Files -Files $bulkFiles -Pattern $currentVersionRegex -Replacement $netVersion -DryRun:$DryRun

# 2. Specific Config/Workflow updates (Safe Pathing)
$specificFiles = @(
    (Join-Path $baseDir "src\Servy.Core\Config\AppConfig.cs"),
    (Join-Path $baseDir ".github\workflows\publish.yml")
)
Update-Files -Files $specificFiles -Pattern $currentVersionRegex -Replacement $netVersion -DryRun:$DryRun

# 3. GitHub Action specific update
$actionFile = Join-Path $baseDir ".github\actions\setup-dotnet\action.yml"
$actionPattern = "(default:\s*')\d+\.\d+(')"

# Use ${1} to prevent the engine from thinking you want group #11
$actionReplacement = "`${1}$Version`${2}"

Update-Files -Files @($actionFile) -Pattern $actionPattern -Replacement $actionReplacement -DryRun:$DryRun

# -----------------------------
# Summary
# -----------------------------
Write-Host "`n========================================="
Write-Host "              SUMMARY"
Write-Host "========================================="
Write-Host "Files scanned:      $script:totalFilesScanned"
Write-Host "Files modified:     $script:filesModified"
Write-Host "Total replacements: $script:totalReplacements"

if ($DryRun) {
    Write-Host "`nDry run complete. No files were modified." -ForegroundColor Yellow
} else {
    Write-Host "`n.NET runtime migration to v$Version successful." -ForegroundColor Green
} 
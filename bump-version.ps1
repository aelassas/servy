#requires -Version 5.0
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

.EXAMPLE
    .\bump-version.ps1 -Version 4.0
    .\bump-version.ps1 4.0

Updates all relevant files to version 4.0.

.NOTES
    - The script overwrites files in-place.
    - Ensure you have backups or version control before running.
#>

param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern("^\d+\.\d+$")]
    [string]$Version
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

Write-Host "Updating Servy version to $Version..."

# -----------------------------
# 1. Update setup\build-config.ps1
# -----------------------------
$buildConfigPath = Join-Path $baseDir 'setup\build-config.ps1'
Update-FilesContent `
    -Files @($buildConfigPath) `
    -Pattern '(Version\s*=\s*")[^"]*(")' `
    -Replacement { param($m) "$($m.Groups[1].Value)$Version$($m.Groups[2].Value)" } `
    -ExpectMatch

# -----------------------------
# 2. Update all *.csproj files recursively
# -----------------------------
Get-ChildItem -Path $baseDir -Recurse -Filter *.csproj -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch $global:BuildArtifactExclusionRegex } |
    ForEach-Object {

    $csproj = $_.FullName
    try {
        $encoding = Get-FileEncoding $csproj
        $content = [System.IO.File]::ReadAllText($csproj, $encoding)

        $totalReplacements = 0
        $versionTags = @('Version', 'FileVersion', 'AssemblyVersion')

        foreach ($tag in $versionTags) {
            $replacementValue = switch ($tag) {
                "Version"         { $fullVersion }
                "FileVersion"     { $fileVersion }
                "AssemblyVersion" { $fileVersion }
            }

            $tagPattern = "(<$tag(?:\s+[^>]*)?>)[^<]*(</$tag>)"
            $tagMatches = [regex]::Matches($content, $tagPattern)
        
            if ($tagMatches.Count -gt 0) {
                $totalReplacements += $tagMatches.Count
                $content = [regex]::Replace($content, $tagPattern, { 
                    param($m) "$($m.Groups[1].Value)$replacementValue$($m.Groups[2].Value)" 
                })
            }
        }

        if ($totalReplacements -gt 0) {
            [System.IO.File]::WriteAllText($csproj, $content, $encoding)
            $script:filesModified++
            $script:totalReplacements += $totalReplacements
            Write-Host "UPDATED ($($encoding.BodyName)): $csproj" -ForegroundColor Green
        } else {
            Write-Warning "Skipped project: No versioning identifiers found in $csproj. Verify if this project requires version metadata."
        }
        $script:totalFilesScanned++
    }
    catch {
        Write-Warning "Failed to update project: $csproj. $($_.Exception.Message)"
        $script:HadFailure = $true
    }
}

# -----------------------------
# 3. Update src\Servy.CLI\Servy.psd1
# -----------------------------
$psd1Path = Join-Path $baseDir "src\Servy.CLI\Servy.psd1"

Update-FilesContent `
    -Files @($psd1Path) `
    -Pattern "(ModuleVersion\s*=\s*')[^']*(')" `
    -Replacement { param($m) "$($m.Groups[1].Value)$fullVersion$($m.Groups[2].Value)" } `
    -ExpectMatch

if ($script:HadFailure) {
    Write-Host "Version update process completed with errors." -ForegroundColor Red
    exit 1
}

Write-Host "All version updates completed successfully." -ForegroundColor Green

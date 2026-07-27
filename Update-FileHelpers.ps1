#requires -Version 5.0
<#
.SYNOPSIS
    Shared helper routines for encoding-preserving file update operations.

.DESCRIPTION
    Provides unified file update logic (Update-FilesContent), shared build artifact 
    exclusion patterns, and bootstraps Get-FileEncoding.ps1 for versioning scripts.
#>

# Shared exclusion pattern for transient build artifacts, dependencies, and vcs metadata
$global:BuildArtifactExclusionRegex = '[\\/](bin|obj|packages|node_modules|\.git|TestResults)[\\/]'

# Bootstrap Get-FileEncoding.ps1
$helperFile = "Get-FileEncoding.ps1"
$helperPath = Join-Path $PSScriptRoot $helperFile

if (Test-Path $helperPath) {
    . $helperPath
} else {
    throw "Critical dependency missing: '$helperFile' was not found at '$helperPath'. Ensure the helper is in the same directory as this script."
}

function Update-FilesContent {
    <#
    .SYNOPSIS
        Safely updates file content using regex patterns while preserving original file encoding.
    #>
    param(
        [Parameter(Mandatory = $true)]
        $Files,

        [Parameter(Mandatory = $true)]
        [string]$Pattern,

        [Parameter(Mandatory = $true)]
        $Replacement,

        [bool]$DryRun = $false,

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
                    $newContent = if ($Replacement -is [scriptblock] -or $Replacement -is [System.Text.RegularExpressions.MatchEvaluator]) {
                        [regex]::Replace($content, $Pattern, $Replacement)
                    } else {
                        [regex]::Replace($content, $Pattern, [string]$Replacement)
                    }

                    [System.IO.File]::WriteAllText($path, $newContent, $encoding)
                    Write-Host "UPDATED ($($encoding.BodyName)): $path" -ForegroundColor Green
                }
            } elseif ($ExpectMatch) {
                Write-Warning "No version patterns matching '$Pattern' were located in explicitly-targeted path: $path"
                $script:HadFailure = $true
            }
        }
        catch {
            Write-Warning "Failed to update file: $path. $($_.Exception.Message)"
            $script:HadFailure = $true
        }
    }
}

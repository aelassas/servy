#Requires -Version 5.0

<#
.SYNOPSIS
    Shared helper routines for encoding-preserving file update operations.

.DESCRIPTION
    Provides unified file update logic (Update-FilesContent), shared build artifact
    exclusion patterns, central BOM required extension definitions, and bootstraps
    Get-FileEncoding.ps1 for versioning and formatting scripts.
#>

# Shared exclusion array and regex pattern for transient build artifacts, dependencies, and vcs metadata
$script:BuildArtifactExclusionDirs = @('bin', 'obj', 'packages', '.git', '.vs', 'node_modules', 'coveragereport', 'TestResults')
$script:BuildArtifactExclusionRegex = '[\\/](bin|obj|packages|\.git|\.vs|node_modules|coveragereport|TestResults)[\\/]'

# Centralized list of file extensions that require UTF-8 with BOM encoding (must agree with .editorconfig)
$script:BomRequiredExtensions = @('.ps1', '.psm1', '.psd1', '.xml', '.config')

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

    .DESCRIPTION
        Iterates over the provided files and applies regex replacements while preserving byte-order marks
        and original file encodings via Get-FileEncoding.

    .NOTES
        Contract & Scoped Variables:
        This function expects and mutates the following caller-scoped ($script:) variables:
          - $script:HadFailure        [bool]   - Set to $true if any file read, write, or expected match fails.
          - $script:totalFilesScanned [int]    - Incremented for each evaluated file path.
          - $script:filesModified     [int]    - Incremented for each file with matching replacements.
          - $script:totalReplacements [int]    - Accumulated count of all successful regex match replacements.

        Calling scripts must initialize these variables prior to calling Update-FilesContent.
    #>
    [CmdletBinding(DefaultParameterSetName = 'SingleEdit')]
    param(
        [Parameter(Mandatory = $true)]
        $Files,

        [Parameter(Mandatory = $true, ParameterSetName = 'SingleEdit')]
        [string]$Pattern,

        [Parameter(Mandatory = $true, ParameterSetName = 'SingleEdit')]
        $Replacement,

        [Parameter(ParameterSetName = 'SingleEdit')]
        [int]$ExpectMatchCount,

        [Parameter(Mandatory = $true, ParameterSetName = 'MultiEdit')]
        [array]$Edits,

        [switch]$DryRun,

        [switch]$ExpectMatch
    )

    # Normalize single pattern/replacement input to the Edits array format
    if ($PSCmdlet.ParameterSetName -eq 'SingleEdit') {
        $editHashtable = @{ Pattern = $Pattern; Replacement = $Replacement }
        if ($PSBoundParameters.ContainsKey('ExpectMatchCount')) {
            $editHashtable['ExpectedCount'] = $ExpectMatchCount
        }
        $Edits = @($editHashtable)
    }

    foreach ($file in $Files) {
        if ($null -eq $file) { continue }
        $path = if ($file -is [string]) { $file } else { $file.FullName }

        if (-not (Test-Path $path)) {
            Write-Warning "Skipping missing file: $path"
            if ($ExpectMatch -or $PSBoundParameters.ContainsKey('ExpectMatchCount')) {
                $script:HadFailure = $true
            }
            continue
        }

        $script:totalFilesScanned++

        try {
            $encoding = Get-FileEncoding $path
            $content = [System.IO.File]::ReadAllText($path, $encoding)

            $matchCount = 0
            $newContent = $content

            foreach ($edit in $Edits) {
                $editPattern = $edit.Pattern
                $editReplacement = $edit.Replacement

                # Resolve expected count setting if defined in hashtable (supports ExpectedCount or ExpectedMatchCount)
                $expectedCount = $null
                if ($edit.ContainsKey('ExpectedCount')) {
                    $expectedCount = [int]$edit.ExpectedCount
                } elseif ($edit.ContainsKey('ExpectedMatchCount')) {
                    $expectedCount = [int]$edit.ExpectedMatchCount
                }

                $regexMatches = [regex]::Matches($newContent, $editPattern)
                $found = $regexMatches.Count

                # Verify exact match count constraint if specified
                if ($null -ne $expectedCount) {
                    if ($found -ne $expectedCount) {
                        Write-Warning "Match count mismatch for pattern '$editPattern' in $path. Expected: $expectedCount, Found: $found"
                        $script:HadFailure = $true
                    }
                } elseif ($found -eq 0 -and $ExpectMatch) {
                    Write-Warning "No matches found for pattern '$editPattern' in explicitly-targeted path: $path"
                    $script:HadFailure = $true
                }

                if ($found -gt 0) {
                    $matchCount += $found
                    if ($editReplacement -is [scriptblock] -or $editReplacement -is [System.Text.RegularExpressions.MatchEvaluator]) {
                        $newContent = [regex]::Replace($newContent, $editPattern, $editReplacement)
                    } else {
                        $newContent = [regex]::Replace($newContent, $editPattern, [string]$editReplacement)
                    }
                }
            }

            if ($matchCount -gt 0) {
                $script:filesModified++
                $script:totalReplacements += $matchCount

                if ($DryRun) {
                    Write-Host "DRY-RUN: Would update $path ($matchCount matches)" -ForegroundColor Gray
                } else {
                    [System.IO.File]::WriteAllText($path, $newContent, $encoding)
                    Write-Host "UPDATED ($($encoding.BodyName)): $path" -ForegroundColor Green
                }
            }
        }
        catch {
            Write-Warning "Failed to update file: $path. $($_.Exception.Message)"
            $script:HadFailure = $true
        }
    }
}

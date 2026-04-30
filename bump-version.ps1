#requires -Version 5.0
<#
.SYNOPSIS
    Updates the version of Servy across scripts, AppConfig, and project files.

.DESCRIPTION
    This script updates the version of Servy in multiple locations:
    - setup\publish.ps1
    - src\Servy.Core\Config\AppConfig.cs
    - All *.csproj files recursively

    It updates:
    - The script version variable in publish.ps1, publish-sc.ps1 and publish-fd.ps1
    - The public static Version string in AppConfig.cs
    - The <Version>, <FileVersion>, and <AssemblyVersion> XML elements in csproj files

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

# -----------------------------
# Convert short version to full versions
# -----------------------------
$fullVersion = "$Version.0"
$fileVersion = "$Version.0.0"

Write-Host "Updating Servy version to $Version..."

# Base directory of the script
$baseDir = $PSScriptRoot

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
# Helper: Update-FileContent
# Safely updates file content while preserving the original encoding.
# ----------------------------------------------------------------------
function Update-FileContent {
    param([string]$Path, [string]$Pattern, [string]$Replacement)
    
    if (Test-Path $Path) {
        $encoding = Get-FileEncoding $Path
        $content = [System.IO.File]::ReadAllText($Path, $encoding)
        
        # Count matches before attempting replacement
        $regexMatches = [regex]::Matches($content, $Pattern)
        if ($regexMatches.Count -eq 0) {
            Write-Error "No matches for pattern in $Path. The identifier may have been renamed or removed. Pattern: $Pattern"
            return
        }
        
        # Perform replacement
        $newContent = [regex]::Replace($content, $Pattern, { 
            param($m) "$($m.Groups[1].Value)$Replacement$($m.Groups[2].Value)" 
        })
        
        [System.IO.File]::WriteAllText($Path, $newContent, $encoding)
        Write-Host "Successfully updated ($($encoding.BodyName)): $Path ($($regexMatches.Count) replacements)" -ForegroundColor Green
    } else {
        Write-Warning "Skipping missing file: $Path"
    }
}

# -----------------------------
# 1. Update setup\publish.ps1, publish-sc.ps1, publish-fd.ps1
# -----------------------------
foreach ($f in @('setup\publish.ps1', 'setup\publish-sc.ps1', 'setup\publish-fd.ps1')) {
    Update-FileContent `
        -Path (Join-Path $baseDir $f) `
        -Pattern '(\[string\]\$Version\s*=\s*")[^"]*(")' `
        -Replacement $Version
}

# -----------------------------
# 2. Update src\Servy.Core\Config\AppConfig.cs
# -----------------------------
Update-FileContent `
    -Path (Join-Path $baseDir "src\Servy.Core\Config\AppConfig.cs") `
    -Pattern '(public static readonly string Version\s*=\s*")[^"]*(";)' `
    -Replacement $Version

# -----------------------------
# 3. Update all *.csproj files recursively
# -----------------------------
Get-ChildItem -Path $baseDir -Recurse -Filter *.csproj -ErrorAction SilentlyContinue | ForEach-Object {
    $csproj = $_.FullName
    
    # Detect encoding first to prevent corruption
    $encoding = Get-FileEncoding $csproj
    $content = [System.IO.File]::ReadAllText($csproj, $encoding)

    # LOGIC: Track total matches across all tags to ensure the script is not silent on no-match.
    # This prevents the "worst failure mode" where projects appear updated but remain on old versions.
    $totalReplacements = 0
    $versionTags = @('Version', 'FileVersion', 'AssemblyVersion')

    foreach ($tag in $versionTags) {
        $replacementValue = ""
        
        switch ($tag) {
            "Version" { 
                $replacementValue = $fullVersion 
                break 
            }
            "FileVersion" { 
                $replacementValue = $fileVersion 
                break 
            }
            "AssemblyVersion" { 
                $replacementValue = $fileVersion 
                break 
            }
        }

        $tagPattern = "(<$tag>)[^<]*(</$tag>)"
        $tagMatches = [regex]::Matches($content, $tagPattern)
        
        if ($tagMatches.Count -gt 0) {
            $totalReplacements += $tagMatches.Count
            $content = [regex]::Replace($content, $tagPattern, { 
                param($m) "$($m.Groups[1].Value)$replacementValue$($m.Groups[2].Value)" 
            })
        }
    }

    if ($totalReplacements -gt 0) {
        # Write back using the detected encoding only if at least one tag was successfully replaced
        [System.IO.File]::WriteAllText($csproj, $content, $encoding)
        Write-Host "Successfully updated project ($($encoding.BodyName)): $csproj ($totalReplacements replacements)" -ForegroundColor Green
    } else {
        # LOG: Warn instead of Error, as non-shipping helper projects may legitimately lack version tags.
        # This mirrors the visibility of Update-FileContent without strictly terminating the script.
        Write-Warning "Skipped project: No versioning identifiers found in $csproj. Verify if this project requires version metadata."
    }
}

# -----------------------------
# 4. Update src\Servy.CLI\Servy.psd1
# -----------------------------
$psd1Path = Join-Path $baseDir "src\Servy.CLI\Servy.psd1"

Update-FileContent -Path $psd1Path -Pattern "(ModuleVersion\s*=\s*')[^']*(')" -Replacement $fullVersion

Write-Host "All version updates complete."

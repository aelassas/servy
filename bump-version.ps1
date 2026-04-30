#requires -Version 5.0
<#
.SYNOPSIS
    Updates the Servy version across all project files.

.DESCRIPTION
    This script updates version numbers in several Servy files based on a provided
    short version (e.g. 1.4). It expands the version into full semantic versions
    and rewrites script variables, C# configuration constants, and assembly metadata.

.PARAMETER Version
    The new version to apply in 'Major.Minor' format (e.g., "8.0").

.EXAMPLE
    ./Update-Version.ps1 1.4
    Updates all version references to 1.4 / 1.4.0 / 1.4.0.0 depending on the file.

.NOTES
    Author: Akram El Assas
    Project: Servy
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern("^\d+\.\d+$")]
    [string]$Version
)

# Convert short version to full versions
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
# Helper: Update-FileContent (Hardened & Encoding-Aware)
# ----------------------------------------------------------------------
function Update-FileContent {
    <#
    .SYNOPSIS
        Safely updates file content while preserving the original encoding and validating matches.
    
    .DESCRIPTION
        This helper reads the file using detected encoding, verifies that the target regex pattern exists,
        and performs a capture-group aware replacement before writing back to disk.
    #>
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

# 1. Update setup\publish.ps1
Update-FileContent `
    -Path (Join-Path $baseDir "setup\publish.ps1") `
    -Pattern '(\$version\s*=\s*")[^"]*(")' `
    -Replacement $Version

# 2. Update src\Servy.Core\Config\AppConfig.cs
Update-FileContent `
    -Path (Join-Path $baseDir "src\Servy.Core\Config\AppConfig.cs") `
    -Pattern '(public static readonly string Version\s*=\s*")[^"]*(";)' `
    -Replacement $Version

# -------------------------------------------------------------
# 3. Update all AssemblyInfo.cs files (Recursive)
# -------------------------------------------------------------
Get-ChildItem -Path $baseDir -Recurse -Filter AssemblyInfo.cs -ErrorAction SilentlyContinue | ForEach-Object {
    $path = $_.FullName
    
    # Detect encoding first to prevent corruption
    $encoding = Get-FileEncoding $path
    $content = [System.IO.File]::ReadAllText($path, $encoding)

    # LOGIC: Track total replacements to prevent silent failures in recursive loops.
    $totalReplacements = 0
    $assemblyTags = @('AssemblyVersion', 'AssemblyFileVersion')

    foreach ($tag in $assemblyTags) {
        $replacementValue = ""

        switch ($tag) {
            "AssemblyVersion" { 
                $replacementValue = $fileVersion 
                break 
            }
            "AssemblyFileVersion" { 
                $replacementValue = $fileVersion 
                break 
            }
        }

        # Case-insensitive pattern for [assembly: AssemblyVersion("...")]
        $pattern = "(\[assembly:\s*$tag\(\"")[^""]*(\""\)\])"
        $matches = [regex]::Matches($content, $pattern, "IgnoreCase")

        if ($matches.Count -gt 0) {
            $totalReplacements += $matches.Count
            $content = [regex]::Replace($content, $pattern, { 
                param($m) "$($m.Groups[1].Value)$replacementValue$($m.Groups[2].Value)" 
            }, "IgnoreCase")
        }
    }

    if ($totalReplacements -gt 0) {
        # Commit changes only if the file was actually modified
        [System.IO.File]::WriteAllText($path, $content, $encoding)
        Write-Host "Updated AssemblyInfo ($($encoding.BodyName)): $path ($totalReplacements replacements)" -ForegroundColor Green
    } else {
        # LOG: Alert the operator if an AssemblyInfo file exists but lacks version metadata.
        Write-Warning "No version attributes found in: $path"
    }
}

# 4. Update src\Servy.CLI\Servy.psd1
Update-FileContent `
    -Path (Join-Path $baseDir "src\Servy.CLI\Servy.psd1") `
    -Pattern "(ModuleVersion\s*=\s*')[^']*(')" `
    -Replacement $fullVersion

Write-Host "`nAll legacy and metadata version updates complete." -ForegroundColor Green
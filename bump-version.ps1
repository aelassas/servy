<#
.SYNOPSIS
    Updates the Servy version across all project files.

.DESCRIPTION
    This script updates version numbers in several Servy files based on a provided
    short version (e.g. 1.4). It expands the version into full semantic versions
    and rewrites:
      - setup\publish.ps1
      - src\Servy.Core\Config\AppConfig.cs
      - All AssemblyInfo.cs files under the project tree

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

# 3. Update all AssemblyInfo.cs files (Recursive)
Get-ChildItem -Path $baseDir -Recurse -Filter AssemblyInfo.cs -ErrorAction SilentlyContinue | ForEach-Object {
    $path = $_.FullName
    
    # Detect encoding first to prevent corruption
    $encoding = Get-FileEncoding $path
    $content = [System.IO.File]::ReadAllText($path, $encoding)

    # Chain AssemblyVersion and AssemblyFileVersion updates
    $content = [regex]::Replace($content, '(\[assembly:\s*AssemblyVersion\(")[^"]*("\)\])', { param($m) "$($m.Groups[1].Value)$fileVersion$($m.Groups[2].Value)" }, "IgnoreCase")
    $content = [regex]::Replace($content, '(\[assembly:\s*AssemblyFileVersion\(")[^"]*("\)\])', { param($m) "$($m.Groups[1].Value)$fileVersion$($m.Groups[2].Value)" }, "IgnoreCase")

    [System.IO.File]::WriteAllText($path, $content, $encoding)
    Write-Host "Updated AssemblyInfo: $path" -ForegroundColor Gray
}

# 4. Update src\Servy.CLI\Servy.psd1
Update-FileContent `
    -Path (Join-Path $baseDir "src\Servy.CLI\Servy.psd1") `
    -Pattern "(ModuleVersion\s*=\s*')[^']*(')" `
    -Replacement $fullVersion

Write-Host "`nAll legacy and metadata version updates complete." -ForegroundColor Green
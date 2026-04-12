<#
.SYNOPSIS
Updates the version of Servy across scripts, AppConfig, and project files.

.DESCRIPTION
This script updates the version of Servy in multiple locations:
- setup\publish.ps1
- src\Servy.Core\Config\AppConfig.cs
- All *.csproj files recursively

It updates:
- The script version variable in publish.ps1
- The public static Version string in AppConfig.cs
- The <Version>, <FileVersion>, and <AssemblyVersion> XML elements in csproj files

.PARAMETER Version
The new version to apply. Can be short (e.g. "4.0") or full (e.g. "4.0.0").

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
$fullVersion = if ($Version -match "^\d+\.\d+$") { "$Version.0" } else { $Version }
$fileVersion = if ($Version -match "^\d+\.\d+$") { "$Version.0.0" } else { "$Version.0.0" }

Write-Host "Updating Servy version to $Version..."

# Base directory of the script
$baseDir = $PSScriptRoot

# -----------------------------
# Helper: Safe File Update
# -----------------------------
function Update-FileContent {
    param([string]$Path, [string]$Pattern, [string]$Replacement)
    
    if (Test-Path $Path) {
        $content = [System.IO.File]::ReadAllText($Path)
        $newContent = [regex]::Replace($content, $Pattern, { 
            param($m) "$($m.Groups[1].Value)$Replacement$($m.Groups[2].Value)" 
        })
        [System.IO.File]::WriteAllText($Path, $newContent)
        Write-Host "Successfully updated: $Path" -ForegroundColor Green
    } else {
        # Log warning instead of crashing the entire pipeline
        Write-Warning "Skipping missing file: $Path"
    }
}

# -----------------------------
# 1. Update setup\publish.ps1
# -----------------------------
Update-FileContent `
    -Path (Join-Path $baseDir "setup\publish.ps1") `
    -Pattern '(\[string\]\$Version\s*=\s*")[^"]*(")' `
    -Replacement $Version

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
    $content = [System.IO.File]::ReadAllText($csproj)

    # Chain replacements to avoid multiple IO hits
    $content = [regex]::Replace($content, '(<Version>)[^<]*(</Version>)', { param($m) "$($m.Groups[1].Value)$fullVersion$($m.Groups[2].Value)" })
    $content = [regex]::Replace($content, '(<FileVersion>)[^<]*(</FileVersion>)', { param($m) "$($m.Groups[1].Value)$fileVersion$($m.Groups[2].Value)" })
    $content = [regex]::Replace($content, '(<AssemblyVersion>)[^<]*(</AssemblyVersion>)', { param($m) "$($m.Groups[1].Value)$fileVersion$($m.Groups[2].Value)" })

    [System.IO.File]::WriteAllText($csproj, $content)
    Write-Host "Updated project: $csproj" -ForegroundColor Gray
}

# -----------------------------
# 4. Update src\Servy.CLI\Servy.psd1
# -----------------------------
Update-FileContent `
    -Path (Join-Path $baseDir "src\Servy.CLI\Servy.psd1") `
    -Pattern "(ModuleVersion\s*=\s*')[^']*(')" `
    -Replacement $fullVersioncd 

Write-Host "All version updates complete."

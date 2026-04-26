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
    - The script version variable in publish.ps1
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
# Helper: Get-FileEncoding
# Detects if a file is UTF8 with or without BOM
# ----------------------------------------------------------------------
function Get-FileEncoding {
    param([string]$Path)
    [byte[]]$bytes = [System.IO.File]::ReadAllBytes($Path)

    # UTF-8 with BOM (EF BB BF)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        return [System.Text.Encoding]::UTF8
    }

    # UTF-16 LE / Unicode (FF FE)
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        return [System.Text.Encoding]::Unicode
    }

    # UTF-16 BE / BigEndianUnicode (FE FF)
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
        return [System.Text.Encoding]::BigEndianUnicode
    }

    # Default: UTF-8 without BOM (Standard for modern .NET and Git)
    return New-Object System.Text.UTF8Encoding($false)
}

# ----------------------------------------------------------------------
# Helper: Update-FileContent
# Safely updates file content while preserving the original encoding.
# ----------------------------------------------------------------------
function Update-FileContent {
    param([string]$Path, [string]$Pattern, [string]$Replacement)
    
    if (Test-Path $Path) {
        # 1. Detect existing encoding before we touch the file
        $encoding = Get-FileEncoding $Path
        
        # 2. Read content using the detected encoding
        $content = [System.IO.File]::ReadAllText($Path, $encoding)
        
        # 3. Perform the regex replacement
        $newContent = [regex]::Replace($content, $Pattern, { 
            param($m) "$($m.Groups[1].Value)$Replacement$($m.Groups[2].Value)" 
        })
        
        # 4. Write back using the EXACT same encoding/BOM status
        [System.IO.File]::WriteAllText($Path, $newContent, $encoding)
        
        Write-Host "Successfully updated ($($encoding.BodyName)): $Path" -ForegroundColor Green
    } else {
        # Prevents build pipeline crashes if files are moved/renamed
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
    
    # Detect encoding first to prevent corruption
    $encoding = Get-FileEncoding $csproj
    $content = [System.IO.File]::ReadAllText($csproj, $encoding)

    # Chain replacements to avoid multiple IO hits
    $content = [regex]::Replace($content, '(<Version>)[^<]*(</Version>)', { param($m) "$($m.Groups[1].Value)$fullVersion$($m.Groups[2].Value)" })
    $content = [regex]::Replace($content, '(<FileVersion>)[^<]*(</FileVersion>)', { param($m) "$($m.Groups[1].Value)$fileVersion$($m.Groups[2].Value)" })
    $content = [regex]::Replace($content, '(<AssemblyVersion>)[^<]*(</AssemblyVersion>)', { param($m) "$($m.Groups[1].Value)$fileVersion$($m.Groups[2].Value)" })

    # Write back using the detected encoding
    [System.IO.File]::WriteAllText($csproj, $content, $encoding)
    Write-Host "Updated project ($($encoding.BodyName)): $csproj" -ForegroundColor Gray
}

# -----------------------------
# 4. Update src\Servy.CLI\Servy.psd1
# -----------------------------
$psd1Path = Join-Path $baseDir "src\Servy.CLI\Servy.psd1"

Update-FileContent -Path $psd1Path -Pattern "(ModuleVersion\s*=\s*')[^']*(')" -Replacement $fullVersion

Write-Host "All version updates complete."

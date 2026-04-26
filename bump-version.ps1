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
# Helper: Update-FileContent (Hardened & Encoding-Aware)
# ----------------------------------------------------------------------
function Update-FileContent {
    param([string]$Path, [string]$Pattern, [string]$Replacement)
    
    if (Test-Path $Path) {
        # 1. Detect Encoding
        $encoding = Get-FileEncoding $Path
        
        # 2. Read
        $content = [System.IO.File]::ReadAllText($Path)
        
        # 3. Replace
        $newContent = [regex]::Replace($content, $Pattern, { 
            param($m) "$($m.Groups[1].Value)$Replacement$($m.Groups[2].Value)" 
        }, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        
        # 4. Write back using the same encoding
        [System.IO.File]::WriteAllText($Path, $newContent, $encoding)
        
        Write-Host "Updated ($($encoding.BodyName)): $Path" -ForegroundColor Green
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
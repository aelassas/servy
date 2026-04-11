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
$baseDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Helper: read file preserving encoding, apply transform, write back with same encoding
function Update-FilePreservingEncoding {
    param(
        [string]$Path,
        [scriptblock]$Transform
    )
    $stream = [System.IO.File]::Open($Path, 'Open', 'Read')
    try {
        $reader = New-Object System.IO.StreamReader($stream, $true)
        $content = $reader.ReadToEnd()
        $encoding = $reader.CurrentEncoding
    }
    finally {
        $reader.Close()
        $stream.Close()
    }
    $content = & $Transform $content
    $bytes = $encoding.GetBytes($content)
    [System.IO.File]::WriteAllBytes($Path, $bytes)
}

# -----------------------------
# 1. Update setup\publish.ps1
# -----------------------------
$publishPath = Join-Path $baseDir "setup\publish.ps1"
if (-Not (Test-Path $publishPath)) { Write-Error "File not found: $publishPath"; exit 1 }

Update-FilePreservingEncoding -Path $publishPath -Transform {
    param($c)
    [regex]::Replace(
        $c,
        '(\[string\]\$Version\s*=\s*")[^"]*(")',
        { param($m) "$($m.Groups[1].Value)$Version$($m.Groups[2].Value)" }
    )
}
Write-Host "Updated $publishPath"

# -----------------------------
# 2. Update src\Servy.Core\Config\AppConfig.cs
# -----------------------------
$appConfigPath = Join-Path $baseDir "src\Servy.Core\Config\AppConfig.cs"
if (-Not (Test-Path $appConfigPath)) { Write-Error "File not found: $appConfigPath"; exit 1 }

Update-FilePreservingEncoding -Path $appConfigPath -Transform {
    param($c)
    [regex]::Replace(
        $c,
        '(public static readonly string Version\s*=\s*")[^"]*(";)',
        { param($m) "$($m.Groups[1].Value)$Version$($m.Groups[2].Value)" }
    )
}
Write-Host "Updated $appConfigPath"

# -----------------------------
# 3. Update all *.csproj files recursively
# -----------------------------
Get-ChildItem -Path $baseDir -Recurse -Filter *.csproj | ForEach-Object {
    $csproj = $_.FullName
    Update-FilePreservingEncoding -Path $csproj -Transform {
        param($c)
        $c = [regex]::Replace(
            $c,
            '(<Version>)[^<]*(</Version>)',
            { param($m) "$($m.Groups[1].Value)$fullVersion$($m.Groups[2].Value)" }
        )
        $c = [regex]::Replace(
            $c,
            '(<FileVersion>)[^<]*(</FileVersion>)',
            { param($m) "$($m.Groups[1].Value)$fileVersion$($m.Groups[2].Value)" }
        )
        [regex]::Replace(
            $c,
            '(<AssemblyVersion>)[^<]*(</AssemblyVersion>)',
            { param($m) "$($m.Groups[1].Value)$fileVersion$($m.Groups[2].Value)" }
        )
    }
    Write-Host "Updated $csproj"
}

# -----------------------------
# 4. Update src\Servy.CLI\Servy.psd1
# -----------------------------
$psd1Path = Join-Path $baseDir "src\Servy.CLI\Servy.psd1"
if (-Not (Test-Path $psd1Path)) { Write-Error "File not found: $psd1Path"; exit 1 }

Update-FilePreservingEncoding -Path $psd1Path -Transform {
    param($c)
    [regex]::Replace(
        $c,
        "(ModuleVersion\s*=\s*')[^']*(')",
        { param($m) "$($m.Groups[1].Value)$fullVersion$($m.Groups[2].Value)" }
    )
}
Write-Host "Updated $psd1Path"

Write-Host "All version updates complete."

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
# 1. Update setup\publish.ps1
# -----------------------------
$publishPath = Join-Path $baseDir "setup\publish.ps1"
if (-Not (Test-Path $publishPath)) { Write-Error "File not found: $publishPath"; exit 1 }

$content = [System.IO.File]::ReadAllText($publishPath)
$content = [regex]::Replace(
    $content,
    '(\[string\]\$Version\s*=\s*")[^"]*(")',
    { param($m) "$($m.Groups[1].Value)$Version$($m.Groups[2].Value)" }
)
[System.IO.File]::WriteAllText($publishPath, $content)
Write-Host "Updated $publishPath"

# -----------------------------
# 2. Update src\Servy.Core\Config\AppConfig.cs
# -----------------------------
$appConfigPath = Join-Path $baseDir "src\Servy.Core\Config\AppConfig.cs"
if (-Not (Test-Path $appConfigPath)) { Write-Error "File not found: $appConfigPath"; exit 1 }

$content = [System.IO.File]::ReadAllText($appConfigPath)
$content = [regex]::Replace(
    $content,
    '(public static readonly string Version\s*=\s*")[^"]*(";)',
    { param($m) "$($m.Groups[1].Value)$Version$($m.Groups[2].Value)" }
)
[System.IO.File]::WriteAllText($appConfigPath, $content)
Write-Host "Updated $appConfigPath"

# -----------------------------
# 3. Update all *.csproj files recursively
# -----------------------------
Get-ChildItem -Path $baseDir -Recurse -Filter *.csproj | ForEach-Object {
    $csproj = $_.FullName
    $content = [System.IO.File]::ReadAllText($csproj)

    # Update <Version>
    $content = [regex]::Replace(
        $content,
        '(<Version>)[^<]*(</Version>)',
        { param($m) "$($m.Groups[1].Value)$fullVersion$($m.Groups[2].Value)" }
    )

    # Update <FileVersion>
    $content = [regex]::Replace(
        $content,
        '(<FileVersion>)[^<]*(</FileVersion>)',
        { param($m) "$($m.Groups[1].Value)$fileVersion$($m.Groups[2].Value)" }
    )

    # Update <AssemblyVersion>
    $content = [regex]::Replace(
        $content,
        '(<AssemblyVersion>)[^<]*(</AssemblyVersion>)',
        { param($m) "$($m.Groups[1].Value)$fileVersion$($m.Groups[2].Value)" }
    )

    [System.IO.File]::WriteAllText($csproj, $content)
    Write-Host "Updated $csproj"
}

Write-Host "All version updates complete."

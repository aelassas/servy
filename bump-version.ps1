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
$FullVersion = if ($Version -match "^\d+\.\d+$") { "$Version.0" } else { $Version }
$FileVersion = if ($Version -match "^\d+\.\d+$") { "$Version.0.0" } else { "$Version.0.0" }

Write-Host "Updating Servy version to $Version..."

# Base directory of the script
$BaseDir = $PSScriptRoot

# -----------------------------
# 1. Update setup\publish.ps1
# -----------------------------
$PublishPath = Join-Path $BaseDir "setup\publish.ps1"
if (-Not (Test-Path $PublishPath)) { Write-Error "File not found: $PublishPath"; exit 1 }

$Content = [System.IO.File]::ReadAllText($PublishPath)
$Content = [regex]::Replace(
    $Content,
    '(\[string\]\$Version\s*=\s*")[^"]*(")',
    { param($m) "$($m.Groups[1].Value)$Version$($m.Groups[2].Value)" }
)
[System.IO.File]::WriteAllText($PublishPath, $Content)
Write-Host "Updated $PublishPath"

# -----------------------------
# 2. Update src\Servy.Core\Config\AppConfig.cs
# -----------------------------
$AppConfigPath = Join-Path $BaseDir "src\Servy.Core\Config\AppConfig.cs"
if (-Not (Test-Path $AppConfigPath)) { Write-Error "File not found: $AppConfigPath"; exit 1 }

$Content = [System.IO.File]::ReadAllText($AppConfigPath)
$Content = [regex]::Replace(
    $Content,
    '(public static readonly string Version\s*=\s*")[^"]*(";)',
    { param($m) "$($m.Groups[1].Value)$Version$($m.Groups[2].Value)" }
)
[System.IO.File]::WriteAllText($AppConfigPath, $Content)
Write-Host "Updated $AppConfigPath"

# -----------------------------
# 3. Update all *.csproj files recursively
# -----------------------------
Get-ChildItem -Path $BaseDir -Recurse -Filter *.csproj | ForEach-Object {
    $csproj = $_.FullName
    $Content = [System.IO.File]::ReadAllText($csproj)

    # Update <Version>
    $Content = [regex]::Replace(
        $Content,
        '(<Version>)[^<]*(</Version>)',
        { param($m) "$($m.Groups[1].Value)$FullVersion$($m.Groups[2].Value)" }
    )

    # Update <FileVersion>
    $Content = [regex]::Replace(
        $Content,
        '(<FileVersion>)[^<]*(</FileVersion>)',
        { param($m) "$($m.Groups[1].Value)$FileVersion$($m.Groups[2].Value)" }
    )

    # Update <AssemblyVersion>
    $Content = [regex]::Replace(
        $Content,
        '(<AssemblyVersion>)[^<]*(</AssemblyVersion>)',
        { param($m) "$($m.Groups[1].Value)$FileVersion$($m.Groups[2].Value)" }
    )

    [System.IO.File]::WriteAllText($csproj, $Content)
    Write-Host "Updated $csproj"
}

Write-Host "All version updates complete."

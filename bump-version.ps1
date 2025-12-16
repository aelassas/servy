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
    The short version number in the format X.Y (e.g. 1.4). This is expanded into
    full versions such as 1.4.0 and 1.4.0.0 for file and assembly metadata.

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
$FullVersion = if ($Version -match "^\d+\.\d+$") { "$Version.0" } else { $Version }
$FileVersion = if ($Version -match "^\d+\.\d+$") { "$Version.0.0" } else { "$Version.0.0" }

Write-Host "Updating Servy version to $Version..."

# Base directory of the script
$BaseDir = $PSScriptRoot

# 1. Update setup\publish.ps1
$PublishPath = Join-Path $BaseDir "setup\publish.ps1"
if (-Not (Test-Path $PublishPath)) { Write-Error "File not found: $PublishPath"; exit 1 }
$Content = [System.IO.File]::ReadAllText($PublishPath)
$Content = [regex]::Replace(
    $Content,
    '(\$Version\s*=\s*")[^"]*(")',
    { param($m) "$($m.Groups[1].Value)$Version$($m.Groups[2].Value)" }
)
[System.IO.File]::WriteAllText($PublishPath, $Content)
Write-Host "Updated $PublishPath"

# 2. Update src\Servy.Core\Config\AppConfig.cs
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

# 4. Update all AssemblyInfo.cs files recursively
Get-ChildItem -Path $BaseDir -Recurse -Filter AssemblyInfo.cs | ForEach-Object {
    $assemblyInfo = $_.FullName
    $Content = [System.IO.File]::ReadAllText($assemblyInfo)

    # Update [assembly: AssemblyVersion("1.0.0.0")]
    $Content = [regex]::Replace(
        $Content,
        '(\[assembly:\s*AssemblyVersion\(")[^"]*("\)\])',
        { param($m) "$($m.Groups[1].Value)$FileVersion$($m.Groups[2].Value)" }
    )

    # Update [assembly: AssemblyFileVersion("1.0.0.0")]
    $Content = [regex]::Replace(
        $Content,
        '(\[assembly:\s*AssemblyFileVersion\(")[^"]*("\)\])',
        { param($m) "$($m.Groups[1].Value)$FileVersion$($m.Groups[2].Value)" }
    )

    [System.IO.File]::WriteAllText($assemblyInfo, $Content)
    Write-Host "Updated $assemblyInfo"
}

Write-Host "All version updates complete."

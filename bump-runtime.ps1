<#
.SYNOPSIS
Updates .NET runtime target version across scripts, AppConfig, and project files.

.DESCRIPTION
This script recursively updates `netX.Y` target framework versions 
inside:
- PowerShell scripts (*.ps1)
- Inno Setup files (*.iss)
- .csproj project files
- src/Servy.Core/Config/AppConfig.cs

Use -DryRun to preview changes without modifying anything.

.PARAMETER Version
The .NET runtime version (e.g. "10.0").

.PARAMETER DryRun
Shows what would change without writing to disk.

.EXAMPLE
./update-runtime.ps1 -Version 10.0

.EXAMPLE
./update-runtime.ps1 10.0

.EXAMPLE
./update-runtime.ps1 -Version 10.0 -DryRun
Shows all changes without modifying files.

.EXAMPLE
./update-runtime.ps1 10.0 -DryRun

.NOTES
This script modifies files in-place unless -DryRun is used.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern("^\d+\.\d+$")]
    [string]$Version,

    [switch]$DryRun
)

# -----------------------------
# Variables
# -----------------------------
$currentVersionRegex = "net\d+\.\d+"
$netVersion = "net$Version"
$baseDir = $PSScriptRoot

Write-Host "Updating .NET runtime to $netVersion..." -ForegroundColor Cyan
if ($DryRun) {
    Write-Host "(Dry Run Mode - no files will be modified)" -ForegroundColor Yellow
}

# Statistics counters
$global:totalFilesScanned = 0
$global:filesModified     = 0
$global:totalReplacements = 0

# -----------------------------
# Helper function
# -----------------------------
function Update-Files {
    param(
        [Parameter(Mandatory)]
        [System.IO.FileInfo[]]$Files,

        [Parameter(Mandatory)]
        [string]$Pattern,

        [Parameter(Mandatory)]
        [string]$Replacement,

        [Parameter(Mandatory)]
        [bool]$DryRun
    )

    foreach ($file in $Files) {
        $path = $file.FullName
        $global:totalFilesScanned++

        try {
            # --- Detect encoding ---
            $stream = [System.IO.File]::Open($path, 'Open', 'Read')
            try {
                $reader = New-Object System.IO.StreamReader($stream, $true)
                $content = $reader.ReadToEnd()
                $encoding = $reader.CurrentEncoding
            }
            finally {
                $reader.Close()
                $stream.Close()
            }

            # --- Count matches ---
            $regexMatches  = [regex]::Matches($content, $Pattern)
            $matchCount = $regexMatches.Count

            if ($matchCount -gt 0) {
                $global:filesModified++
                $global:totalReplacements += $matchCount

                if ($DryRun) {
                    Write-Host "DRY-RUN: Would update $path ($matchCount replacements)"
                } else {
                    $NewContent = [regex]::Replace($content, $Pattern, $Replacement)

                    # Write using SAME encoding
                    $Bytes = $encoding.GetBytes($NewContent)
                    [System.IO.File]::WriteAllBytes($path, $Bytes)

                    Write-Host "UPDATED: $path ($matchCount replacements)"
                }
            }
            else {
                Write-Host "NO-CHANGE: $path"
            }
        }
        catch {
            Write-Error "Failed to update file: $path. $_"
            exit 1
        }
    }
}

# -----------------------------
# Process files
# -----------------------------
$ps1Files      = Get-ChildItem -Path $baseDir -Recurse -Filter *.ps1
$issFiles      = Get-ChildItem -Path $baseDir -Recurse -Filter *.iss
$csprojFiles   = Get-ChildItem -Path $baseDir -Recurse -Filter *.csproj
$appConfigPath = Join-Path $baseDir "src\Servy.Core\Config\AppConfig.cs"
$publishPath   = Join-Path $baseDir ".github\workflows\publish.yml"

Update-Files -Files $ps1Files    -Pattern $currentVersionRegex -Replacement $netVersion -DryRun:$DryRun
Update-Files -Files $issFiles    -Pattern $currentVersionRegex -Replacement $netVersion -DryRun:$DryRun
Update-Files -Files $csprojFiles -Pattern $currentVersionRegex -Replacement $netVersion -DryRun:$DryRun
Update-Files -Files (Get-Item $appConfigPath) -Pattern $currentVersionRegex -Replacement $netVersion -DryRun:$DryRun
Update-Files -Files (Get-Item $publishPath)   -Pattern $currentVersionRegex -Replacement $netVersion -DryRun:$DryRun

# -----------------------------
# Summary
# -----------------------------
Write-Host ""
Write-Host "========================================="
Write-Host "              SUMMARY"
Write-Host "========================================="

Write-Host "Files scanned:      $global:totalFilesScanned"
Write-Host "Files modified:     $global:filesModified"
Write-Host "Total replacements: $global:totalReplacements"

if ($DryRun) {
    Write-Host "`nDry run complete. No files were modified." -ForegroundColor Yellow
} else {
    Write-Host "`n.NET runtime updated to $netVersion" -ForegroundColor Green
}

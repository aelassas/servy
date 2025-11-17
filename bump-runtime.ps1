<#
.SYNOPSIS
Updates .NET runtime target version across all projects.

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
./update-runtime.ps1 -Version 12.0

.EXAMPLE
./update-runtime.ps1 -Version 12.0 -DryRun
Shows all changes without modifying files.

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
$CurrentVersionRegex = "net\d+\.\d+"
$NetVersion = "net$Version"
$BaseDir = $PSScriptRoot

Write-Host "Updating .NET runtime to $NetVersion..." -ForegroundColor Cyan
if ($DryRun) {
    Write-Host "(Dry Run Mode — no files will be modified)" -ForegroundColor Yellow
}

# Statistics counters
$global:TotalFilesScanned = 0
$global:FilesModified     = 0
$global:TotalReplacements = 0

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
        $global:TotalFilesScanned++

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
            $matches = [regex]::Matches($content, $Pattern)
            $matchCount = $matches.Count

            if ($matchCount -gt 0) {
                $global:FilesModified++
                $global:TotalReplacements += $matchCount

                if ($DryRun) {
                    Write-Host "DRY-RUN: Would update $path ($matchCount replacements)"
                } else {
                    $newContent = [regex]::Replace($content, $Pattern, $Replacement)

                    # Write using SAME encoding
                    $bytes = $encoding.GetBytes($newContent)
                    [System.IO.File]::WriteAllBytes($path, $bytes)

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
$ps1Files    = Get-ChildItem -Path $BaseDir -Recurse -Filter *.ps1
$issFiles    = Get-ChildItem -Path $BaseDir -Recurse -Filter *.iss
$csprojFiles = Get-ChildItem -Path $BaseDir -Recurse -Filter *.csproj
$AppConfigPath = Join-Path $BaseDir "src\Servy.Core\Config\AppConfig.cs"

Update-Files -Files $ps1Files    -Pattern $CurrentVersionRegex -Replacement $NetVersion -DryRun:$DryRun
Update-Files -Files $issFiles    -Pattern $CurrentVersionRegex -Replacement $NetVersion -DryRun:$DryRun
Update-Files -Files $csprojFiles -Pattern $CurrentVersionRegex -Replacement $NetVersion -DryRun:$DryRun

if (Test-Path $AppConfigPath) {
    Update-Files -Files (Get-Item $AppConfigPath) -Pattern $CurrentVersionRegex -Replacement $NetVersion -DryRun:$DryRun
} else {
    Write-Error "File not found: $AppConfigPath"
}

# -----------------------------
# Summary
# -----------------------------
Write-Host ""
Write-Host "========================================="
Write-Host "              SUMMARY"
Write-Host "========================================="

Write-Host "Files scanned:      $global:TotalFilesScanned"
Write-Host "Files modified:     $global:FilesModified"
Write-Host "Total replacements: $global:TotalReplacements"

if ($DryRun) {
    Write-Host "`nDry run complete. No files were modified." -ForegroundColor Yellow
} else {
    Write-Host "`n.NET runtime updated to $NetVersion" -ForegroundColor Green
}

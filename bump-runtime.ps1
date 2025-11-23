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
$CurrentVersionRegex = "net\d+\.\d+"
$NetVersion = "net$Version"
$BaseDir = $PSScriptRoot

Write-Host "Updating .NET runtime to $NetVersion..." -ForegroundColor Cyan
if ($DryRun) {
    Write-Host "(Dry Run Mode - no files will be modified)" -ForegroundColor Yellow
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
        $Path = $file.FullName
        $global:TotalFilesScanned++

        try {
            # --- Detect encoding ---
            $Stream = [System.IO.File]::Open($Path, 'Open', 'Read')
            try {
                $Reader = New-Object System.IO.StreamReader($Stream, $true)
                $Content = $Reader.ReadToEnd()
                $Encoding = $Reader.CurrentEncoding
            }
            finally {
                $Reader.Close()
                $Stream.Close()
            }

            # --- Count matches ---
            $RegexMatches  = [regex]::Matches($Content, $Pattern)
            $MatchCount = $RegexMatches.Count

            if ($MatchCount -gt 0) {
                $global:FilesModified++
                $global:TotalReplacements += $MatchCount

                if ($DryRun) {
                    Write-Host "DRY-RUN: Would update $Path ($MatchCount replacements)"
                } else {
                    $NewContent = [regex]::Replace($Content, $Pattern, $Replacement)

                    # Write using SAME encoding
                    $Bytes = $Encoding.GetBytes($NewContent)
                    [System.IO.File]::WriteAllBytes($Path, $Bytes)

                    Write-Host "UPDATED: $Path ($MatchCount replacements)"
                }
            }
            else {
                Write-Host "NO-CHANGE: $Path"
            }
        }
        catch {
            Write-Error "Failed to update file: $Path. $_"
            exit 1
        }
    }
}

# -----------------------------
# Process files
# -----------------------------
$Ps1Files      = Get-ChildItem -Path $BaseDir -Recurse -Filter *.ps1
$IssFiles      = Get-ChildItem -Path $BaseDir -Recurse -Filter *.iss
$CsprojFiles   = Get-ChildItem -Path $BaseDir -Recurse -Filter *.csproj
$AppConfigPath = Join-Path $BaseDir "src\Servy.Core\Config\AppConfig.cs"

Update-Files -Files $Ps1Files    -Pattern $CurrentVersionRegex -Replacement $NetVersion -DryRun:$DryRun
Update-Files -Files $IssFiles    -Pattern $CurrentVersionRegex -Replacement $NetVersion -DryRun:$DryRun
Update-Files -Files $CsprojFiles -Pattern $CurrentVersionRegex -Replacement $NetVersion -DryRun:$DryRun

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

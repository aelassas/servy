#Requires -Version 5.0
<#
.SYNOPSIS
    Enforces whitespace hygiene (trailing whitespace removal and final newline insertion) across C# sources and GitHub CI files.

.DESCRIPTION
    Scans C# source files under 'src' and 'tests' directories (excluding obj/ folders and generated files like *.g.cs, *.g.i.cs, *.Designer.cs)
    as well as YAML workflow/action files under '.github', enforcing two EditorConfig rules:
    1. Trims trailing whitespace from all lines (trim_trailing_whitespace = true).
    2. Ensures every file ends with a trailing newline (insert_final_newline = true).

.PARAMETER DryRun
    If specified, previews the files that violate whitespace hygiene rules without modifying them on disk.

.EXAMPLE
    .\Format-SourceHygiene.ps1

Formats all C# source files and GitHub CI YAML files in-place.

.EXAMPLE
    .\Format-SourceHygiene.ps1 -DryRun

Previews files needing whitespace trimming or missing a final newline without writing changes to disk.

.NOTES
    File Name : Format-SourceHygiene.ps1
#>

[CmdletBinding()]
param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$baseDir = $PSScriptRoot

# Evaluate targets individually to preserve compatibility with Windows PowerShell 5.1
$targetDirs = @('src', 'tests', '.github') | ForEach-Object {
    $dir = Join-Path $baseDir $_
    if (Test-Path $dir) { $dir }
}

if (-not $targetDirs) {
    Write-Host "No 'src', 'tests', or '.github' directories found under $baseDir." -ForegroundColor Yellow
    exit 0
}

if ($DryRun) {
    Write-Host "DRY-RUN: Previewing files violating whitespace hygiene..." -ForegroundColor Yellow
} else {
    Write-Host "Scanning and formatting files for whitespace hygiene..." -ForegroundColor Cyan
}

$scannedCount = 0
$modifiedCount = 0
$trimmedOnlyCount = 0
$newlineOnlyCount = 0
$bothCount = 0

# Collect .cs files from src/tests and .yml/.yaml files from .github, excluding obj/ and generated files
$filesToScan = Get-ChildItem -Path $targetDirs -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object {
        $relativePath = $_.FullName.Replace($baseDir, '')
        
        # Exclude obj/ directory
        if ($relativePath -match '[\\/]obj[\\/]') {
            return $false
        }

        # Match target file extensions and exclude generated code
        if ($_.Extension -eq '.cs') {
            return ($_.Name -notlike '*.Designer.cs') -and
                   ($_.Name -notlike '*.g.cs') -and
                   ($_.Name -notlike '*.g.i.cs')
        }
        
        return $_.Extension -in @('.yml', '.yaml')
    }

foreach ($file in $filesToScan) {
    $scannedCount++
    $filePath = $file.FullName
    $rawText = [System.IO.File]::ReadAllText($filePath)

    # Check 1: Missing final newline
    $lacksFinalNewline = ($rawText.Length -gt 0) -and (-not $rawText.EndsWith("`n"))

    # Check 2: Trailing whitespace per line
    $hasTrailingWhitespace = $false
    $lines = Get-Content -Path $filePath -Encoding UTF8
    $trimmedLines = foreach ($line in $lines) {
        $t = $line.TrimEnd()
        if ($t -ne $line) {
            $hasTrailingWhitespace = $true
        }
        $t
    }

    if ($hasTrailingWhitespace -or $lacksFinalNewline) {
        $modifiedCount++
        $relativePath = $file.FullName.Replace($baseDir, '')

        # Detail categorization for progress output
        $reason = if ($hasTrailingWhitespace -and $lacksFinalNewline) {
            $bothCount++
            "trailing whitespace & missing final newline"
        } elseif ($hasTrailingWhitespace) {
            $trimmedOnlyCount++
            "trailing whitespace"
        } else {
            $newlineOnlyCount++
            "missing final newline"
        }

        if ($DryRun) {
            Write-Host "Would format ($reason): $relativePath" -ForegroundColor Yellow
        } else {
            # UTF8Encoding($false) = no BOM, on both Windows PowerShell 5.1 and PowerShell 7+
            $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
            $content   = ($trimmedLines -join "`n") + "`n"
            [System.IO.File]::WriteAllText($filePath, $content, $utf8NoBom)
            Write-Host "Formatted ($reason): $relativePath" -ForegroundColor Gray
        }
    }
}

if ($DryRun) {
    Write-Host "`nDRY-RUN: Scan Complete! (No files modified)" -ForegroundColor Yellow
    Write-Host "Files Scanned        : $scannedCount"
    Write-Host "Files Needing Format : $modifiedCount" -ForegroundColor Yellow
} else {
    Write-Host "`nFormat Complete!" -ForegroundColor Green
    Write-Host "Files Scanned  : $scannedCount"
    Write-Host "Files Modified : $modifiedCount" -ForegroundColor Green
}

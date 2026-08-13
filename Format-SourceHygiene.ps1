#Requires -Version 5.0
<#
.SYNOPSIS
    Enforces whitespace hygiene (trailing whitespace removal and final newline insertion) across repository source files.

.DESCRIPTION
    Scans source code, project configurations, PowerShell scripts, YAML workflows, and markdown files
    across the repository (excluding build outputs like bin/obj, version control folders, and generated files like *.g.cs, *.g.i.cs, *.Designer.cs),
    enforcing two EditorConfig rules:
    1. Trims trailing whitespace from all lines (trim_trailing_whitespace = true).
    2. Ensures every file ends with a trailing newline (insert_final_newline = true).

    Note: .resx files are explicitly excluded because trailing whitespace within XML <value> elements
    can be meaningful string content.

.PARAMETER DryRun
    If specified, previews the files that violate whitespace hygiene rules without modifying them on disk.

.EXAMPLE
    .\Format-SourceHygiene.ps1

Formats all applicable repository files in-place.

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

# Extensions governed by .editorconfig whitespace rules
# NOTE: .resx files are deliberately omitted because trailing spaces inside XML <value> elements
# represent localized string data rather than code formatting.
$textExtensions = @(
    '.cs', '.yml', '.yaml', '.ps1', '.psm1', '.psd1',
    '.csproj', '.props', '.targets', '.xaml',
    '.md', '.iss', '.manifest', '.json', '.sln'
)

# Collect repository text files excluding build output, version control, and generated files
$filesToScan = Get-ChildItem -Path $baseDir -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object {
        $relativePath = $_.FullName.Replace($baseDir, '')

        # Exclude build output, version control, and third-party dependency folders
        if ($relativePath -match '[\\/](obj|bin|\.git|\.vs|packages|node_modules|coveragereport)[\\/]') {
            return $false
        }

        # Match target file extensions and exclude generated C# code
        if ($_.Extension -eq '.cs') {
            return ($_.Name -notlike '*.Designer.cs') -and
                   ($_.Name -notlike '*.g.cs') -and
                   ($_.Name -notlike '*.g.i.cs')
        }

        return $_.Extension -in $textExtensions
    }

foreach ($file in $filesToScan) {
    $scannedCount++
    $filePath = $file.FullName
    $rawText = [System.IO.File]::ReadAllText($filePath)

    # Check 1: Missing final newline (CRLF)
    $lacksFinalNewline = ($rawText.Length -gt 0) -and (-not $rawText.EndsWith("`r`n"))

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
            $content   = ($trimmedLines -join "`r`n") + "`r`n"
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

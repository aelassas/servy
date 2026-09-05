#Requires -Version 5.0

<#
.SYNOPSIS
    Enforces whitespace hygiene (trailing whitespace removal, final newline insertion, line-ending normalization, and UTF-8 BOM policy) across repository source files.

.DESCRIPTION
    Scans source code, project configurations, PowerShell scripts, YAML workflows, and markdown files
    across the repository (excluding build outputs like bin/obj, version control folders, and generated files like *.g.cs, *.g.i.cs, *.Designer.cs),
    enforcing EditorConfig rules:
    1. Trims trailing whitespace from all lines (trim_trailing_whitespace = true).
    2. Ensures every file ends with a trailing newline (insert_final_newline = true).
    3. Normalizes line endings to CRLF (end_of_line = crlf).
    4. Enforces charset encoding policy (UTF-8 with BOM for .ps1, .psm1, .psd1, .xml, .config; UTF-8 without BOM for all others).

    Note: .resx files are explicitly excluded because trailing whitespace within XML <value> elements
    can be meaningful string content. Additionally, trailing whitespace is preserved on .md files to respect
    .editorconfig's [*.md] section where trim_trailing_whitespace = false (as two trailing spaces denote Markdown hard line breaks).

.PARAMETER DryRun
    If specified, previews the files that violate whitespace hygiene or charset rules without modifying them on disk.

.EXAMPLE
    .\Format-SourceHygiene.ps1

Formats all applicable repository files in-place.

.EXAMPLE
    .\Format-SourceHygiene.ps1 -DryRun

Previews files needing formatting or encoding corrections without writing changes to disk.

.NOTES
    File Name : Format-SourceHygiene.ps1
#>

[CmdletBinding()]
param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$baseDir = $PSScriptRoot

# Dot-source Update-FileHelpers.ps1 for shared exclusion definitions if available
$helperPath = Join-Path $PSScriptRoot "Update-FileHelpers.ps1"
if (Test-Path $helperPath) {
    . $helperPath
}

$exclusionRegex = if ($script:BuildArtifactExclusionRegex) {
    $script:BuildArtifactExclusionRegex
} else {
    '[\\/](bin|obj|packages|\.git|\.vs|node_modules|coveragereport|TestResults)[\\/]'
}

if ($DryRun) {
    Write-Host "DRY-RUN: Previewing files violating whitespace hygiene and charset rules..." -ForegroundColor Yellow
} else {
    Write-Host "Scanning and formatting files for whitespace hygiene and charset rules..." -ForegroundColor Cyan
}

$scannedCount = 0
$modifiedCount = 0
$failedCount = 0
$trimmedOnlyCount = 0
$newlineOnlyCount = 0
$bomOnlyCount = 0

# File extensions requiring UTF-8 BOM encoding
$bomRequiredExtensions = @('.ps1', '.psm1', '.psd1', '.xml', '.config')

# Extensions governed by .editorconfig whitespace and charset rules
# NOTE: .resx files are deliberately omitted because trailing spaces inside XML <value> elements
# represent localized string data rather than code formatting.
$textExtensions = @(
    '.cs', '.yml', '.yaml', '.ps1', '.psm1', '.psd1',
    '.csproj', '.props', '.targets', '.xaml',
    '.md', '.iss', '.manifest', '.json', '.sln',
    '.xml', '.config'
)

# Collect repository text files excluding build output, version control, and generated files
$filesToScan = Get-ChildItem -Path $baseDir -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object {
        $relativePath = $_.FullName.Replace($baseDir, '')

        # Exclude build output, version control, and third-party dependency folders
        if ($relativePath -match $exclusionRegex) {
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
    $relativePath = $file.FullName.Replace($baseDir, '')

    try {
        # Inspect encoding strictly; throw exception if file contains undecodable bytes
        $sourceEncoding = Get-FileEncoding $filePath
        $rawText = [System.IO.File]::ReadAllText($filePath, $sourceEncoding)

        # Check Charset & BOM policy using single-pass Get-FileEncoding result (#6625)
        $isUtf8   = $sourceEncoding -is [System.Text.UTF8Encoding]
        $hasBom   = $isUtf8 -and ($sourceEncoding.GetPreamble().Length -eq 3)
        $wantsBom = $file.Extension -in $bomRequiredExtensions
        $bomWrong = (-not $isUtf8) -or ($hasBom -ne $wantsBom)

        # .editorconfig [*.md] sets trim_trailing_whitespace = false: two trailing
        # spaces are a Markdown hard line break, not stray whitespace.
        $trimAllowed = $file.Extension -ne '.md'

        # Check 1: Missing final newline (insert_final_newline = true) (#6612)
        $lacksFinalNewline = ($rawText.Length -gt 0) -and (-not $rawText.EndsWith("`n"))

        # Check 2: Non-CRLF line endings (end_of_line = crlf) (#6612)
        $hasLfEndings = $rawText.Contains("`n") -and ($rawText -replace "`r`n", '').Contains("`n")

        # Check 3: Trailing whitespace per line (derived from in-memory split)
        $hasTrailingWhitespace = $false
        $lines = $rawText -split "`r?`n"
        $trimmedLines = foreach ($line in $lines) {
            if ($trimAllowed) {
                $t = $line.TrimEnd()
                if ($t -ne $line) {
                    $hasTrailingWhitespace = $true
                }
                $t
            } else {
                $line
            }
        }

        if ($hasTrailingWhitespace -or $lacksFinalNewline -or $hasLfEndings -or $bomWrong) {
            # Build precise issue categorization reasons for reporting
            $reasons = @()
            if ($hasTrailingWhitespace) {
                $trimmedOnlyCount++
                $reasons += "trailing whitespace"
            }
            if ($lacksFinalNewline) {
                $newlineOnlyCount++
                $reasons += "missing final newline"
            }
            if ($hasLfEndings) {
                $reasons += "LF line endings"
            }
            if ($bomWrong) {
                $bomOnlyCount++
                if (-not $isUtf8) {
                    $reasons += "not UTF-8 ($($sourceEncoding.EncodingName))"
                } else {
                    $reasons += if ($wantsBom) { "missing UTF-8 BOM" } else { "UTF-8 BOM detected" }
                }
            }
            $reasonStr = $reasons -join " & "

            if ($DryRun) {
                $modifiedCount++
                Write-Host "Would format ($reasonStr): $relativePath" -ForegroundColor Yellow
            } else {
                $targetEncoding = New-Object System.Text.UTF8Encoding($wantsBom)

                # Drop trailing empty element produced by splitting text ending with a newline.
                # Bounded at > 1 (not > 0): a single-element array here is $trimmedLines -eq @(''),
                # and 0..($trimmedLines.Count - 2) would evaluate as the descending range 0..-1.
                if ($trimmedLines.Count -gt 1 -and $trimmedLines[-1] -eq '') {
                    $trimmedLines = $trimmedLines[0..($trimmedLines.Count - 2)]
                }

                $body = $trimmedLines -join "`r`n"

                # A file that is empty, or becomes empty once trailing whitespace is trimmed, needs
                # no final newline (Check 1 exempts it); rewriting must not add one.
                $content = if ($body.Length -eq 0) { '' } else { $body + "`r`n" }

                [System.IO.File]::WriteAllText($filePath, $content, $targetEncoding)
                $modifiedCount++
                Write-Host "Formatted ($reasonStr): $relativePath" -ForegroundColor Gray
            }
        }
    }
    catch [System.Text.DecoderFallbackException] {
        Write-Warning "Skipped ${relativePath}: undecodable bytes for detected encoding ($($sourceEncoding.WebName)): $_"
        $failedCount++
    }
    catch {
        Write-Warning "Failed to process ${relativePath}: $_"
        $failedCount++
    }
}

if ($DryRun) {
    Write-Host "`nDRY-RUN: Scan Complete! (No files modified)" -ForegroundColor Yellow
    Write-Host "Files Scanned        : $scannedCount"
    Write-Host "Files Needing Format : $modifiedCount" -ForegroundColor Yellow
    if ($failedCount -gt 0) {
        Write-Host "Files Skipped/Failed : $failedCount" -ForegroundColor Red
    }
} else {
    Write-Host "`nFormat Complete!" -ForegroundColor Green
    Write-Host "Files Scanned  : $scannedCount"
    Write-Host "Files Modified : $modifiedCount" -ForegroundColor Green
    if ($failedCount -gt 0) {
        Write-Host "Files Skipped/Failed : $failedCount" -ForegroundColor Red
    }
}

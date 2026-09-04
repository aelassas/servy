#Requires -Version 5.1
<#
.SYNOPSIS
    Recursively converts text files in the repository root to UTF-8 (with BOM for PowerShell scripts/manifests, XML, and config files; no BOM for others) with Windows (CRLF) line endings.

.DESCRIPTION
    Traverses the repository root recursively, skipping specified directories (e.g., bin, obj,
    node_modules) and file types/names (e.g., .exe, .7z, .coverage.xml, coverage.cobertura.xml).
    Normalizes line endings to Windows CRLF (`r`n) and re-writes file content using
    [System.IO.File]::WriteAllText only when changes are detected. PowerShell files (.ps1, .psm1, .psd1),
    XML files (.xml), and configuration files (.config) are saved as UTF-8 with BOM for compatibility,
    while all other text files are saved as UTF-8 (no BOM).

.PARAMETER DryRun
    If specified, previews the files that would be converted without performing writes to disk.

.PARAMETER ExcludeDirs
    Array of folder names to exclude from processing. Defaults to 'bin', 'obj', 'packages', '.git', '.vs', 'node_modules', 'coveragereport', 'TestResults'.

.PARAMETER ExcludeExtensions
    Array of file extensions to exclude. Supports compound extensions like '.coverage.xml'. Defaults to '.Designer.cs', '.exe', '.pdb', '.dll', '.7z', '.coverage.xml', '.ico', '.png', '.bmp', '.cur', '.res', '.snk', '.pfx'.

.PARAMETER ExcludeFiles
    Array of specific filenames to exclude. Defaults to 'coverage.cobertura.xml'.

.NOTES
    Target Runtime: PowerShell 5.0+
    Excludes the running script itself from conversion.

.EXAMPLE
    .\Format-SourceFiles.ps1

.EXAMPLE
    .\Format-SourceFiles.ps1 -DryRun
#>

[CmdletBinding()]
param(
    [switch]$DryRun,

    [Parameter(Mandatory = $false)]
    [string[]]$ExcludeDirs = @('bin', 'obj', 'packages', '.git', '.vs', 'node_modules', 'coveragereport', 'TestResults'),

    [Parameter(Mandatory = $false)]
    [string[]]$ExcludeExtensions = @('.Designer.cs', '.exe', '.pdb', '.dll', '.7z', '.coverage.xml', '.ico', '.png', '.bmp', '.cur', '.res', '.snk', '.pfx'),

    [Parameter(Mandatory = $false)]
    [string[]]$ExcludeFiles = @('coverage.cobertura.xml')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:HadFailure = $false

# Anchor execution directly to repository root where the script lives
$baseDir = $PSScriptRoot

if (-not (Test-Path (Join-Path $baseDir 'Servy.sln'))) {
    throw "Run from the Servy repository root (Servy.sln not found in $baseDir)"
}

# Default list of BOM-required extensions if Update-FileHelpers.ps1 is unavailable
$bomRequiredExtensions = @('.ps1', '.psm1', '.psd1', '.xml', '.config')

# Dot-source Update-FileHelpers.ps1 for shared exclusion definitions and BOM policy if available
$helperPath = Join-Path $PSScriptRoot "Update-FileHelpers.ps1"
if (Test-Path $helperPath) {
    . $helperPath
    if ($script:BuildArtifactExclusionDirs -and -not $PSBoundParameters.ContainsKey('ExcludeDirs')) {
        $ExcludeDirs = $script:BuildArtifactExclusionDirs
    }
    if ($script:BomRequiredExtensions) {
        $bomRequiredExtensions = $script:BomRequiredExtensions
    }
}

# Construct UTF-8 encoding objects (With BOM for .ps1/.psm1/.psd1/.xml/.config, No BOM for other files)
$utf8WithBom = New-Object System.Text.UTF8Encoding($true)
$utf8NoBom   = New-Object System.Text.UTF8Encoding($false)

$scriptPath = $MyInvocation.MyCommand.Path

if ($DryRun) {
    Write-Host "DRY-RUN: Previewing UTF-8 & CRLF conversions in: $baseDir" -ForegroundColor Yellow
} else {
    Write-Host "Starting UTF-8 & CRLF conversion in: $baseDir" -ForegroundColor Cyan
}
Write-Host "Excluding directories     : $($ExcludeDirs -join ', ')" -ForegroundColor Yellow
Write-Host "Excluding extensions      : $($ExcludeExtensions -join ', ')" -ForegroundColor Yellow
Write-Host "Excluding specific files  : $($ExcludeFiles -join ', ')" -ForegroundColor Yellow
Write-Host ""

# Helper function to recursively collect files with early folder & file filtering
function Get-FilteredFiles {
    param(
        [string]$Path,
        [string[]]$DirExclusions,
        [string[]]$ExtExclusions,
        [string[]]$FileExclusions
    )

    # 1. Process files in current directory
    $files = Get-ChildItem -Path $Path -File -ErrorAction SilentlyContinue
    foreach ($file in $files) {
        # Skip exact file matches
        if ($FileExclusions -contains $file.Name) {
            continue
        }

        # Skip extension matches (supports multi-dot suffixes like .coverage.xml)
        $isExcludedExt = $false
        foreach ($ext in $ExtExclusions) {
            if ($file.Name.EndsWith($ext, [System.StringComparison]::OrdinalIgnoreCase)) {
                $isExcludedExt = $true
                break
            }
        }
        if ($isExcludedExt) {
            continue
        }

        $file
    }

    # 2. Inspect subdirectories and skip excluded ones prior to entering
    $directories = Get-ChildItem -Path $Path -Directory -ErrorAction SilentlyContinue
    foreach ($dir in $directories) {
        if ($DirExclusions -notcontains $dir.Name) {
            Get-FilteredFiles -Path $dir.FullName `
                              -DirExclusions $DirExclusions `
                              -ExtExclusions $ExtExclusions `
                              -FileExclusions $FileExclusions
        }
    }
}

# Fast byte array equality check for PowerShell 5.1 and Core
function Test-ByteArrayEqual {
    param(
        [byte[]]$Bytes1,
        [byte[]]$Bytes2
    )

    if ($null -eq $Bytes1 -or $null -eq $Bytes2) { return $Bytes1 -eq $Bytes2 }
    if ($Bytes1.Length -ne $Bytes2.Length) { return $false }

    for ($i = 0; $i -lt $Bytes1.Length; $i++) {
        if ($Bytes1[$i] -ne $Bytes2[$i]) {
            return $false
        }
    }
    return $true
}

# Collect target files using early directory pruning
$files = Get-FilteredFiles -Path $baseDir `
                           -DirExclusions $ExcludeDirs `
                           -ExtExclusions $ExcludeExtensions `
                           -FileExclusions $ExcludeFiles

$scannedCount   = 0
$convertedCount = 0
$failedCount    = 0

foreach ($file in $files) {
    try {
        $scannedCount++

        # Inspect and validate existing encoding strictly to prevent silent non-UTF-8 character corruption
        $sourceEncoding = Get-FileEncoding $file.FullName
        $content = [System.IO.File]::ReadAllText($file.FullName, $sourceEncoding)

        # Normalize all line returns (CRLF, LF, CR) to Windows CRLF (`r`n)
        $crlfContent = $content.Replace("`r`n", "`n").Replace("`r", "`n").Replace("`n", "`r`n")

        # Select UTF-8 with BOM for .ps1, .psm1, .psd1, .xml, and .config files; UTF-8 without BOM for all other files
        $requiresBom = $file.Extension -in $bomRequiredExtensions
        $targetEncoding = if ($requiresBom) { $utf8WithBom } else { $utf8NoBom }

        $encodingLabel = if ($requiresBom) { "UTF-8 with BOM" } else { "UTF-8 (no BOM)" }

        # Perform byte-level comparison to detect actual line ending or encoding drift
        $originalBytes = [System.IO.File]::ReadAllBytes($file.FullName)
        $preamble      = $targetEncoding.GetPreamble()
        $contentBytes  = $targetEncoding.GetBytes($crlfContent)

        $targetBytes = New-Object byte[] ($preamble.Length + $contentBytes.Length)
        if ($preamble.Length -gt 0) {
            [System.Buffer]::BlockCopy($preamble, 0, $targetBytes, 0, $preamble.Length)
        }
        [System.Buffer]::BlockCopy($contentBytes, 0, $targetBytes, $preamble.Length, $contentBytes.Length)

        if (-not (Test-ByteArrayEqual -Bytes1 $originalBytes -Bytes2 $targetBytes)) {
            if ($DryRun) {
                Write-Host "Would convert ($encodingLabel): $($file.FullName)" -ForegroundColor Yellow
            } else {
                # Write back file content only when changes are present
                [System.IO.File]::WriteAllText($file.FullName, $crlfContent, $targetEncoding)
                Write-Host "Converted ($encodingLabel): $($file.FullName)" -ForegroundColor Green
            }
            $convertedCount++
        }
    }
    catch {
        Write-Warning "Failed to convert $($file.FullName): $_"
        $failedCount++
        $script:HadFailure = $true
    }
}

if ($DryRun) {
    Write-Host "`nDRY-RUN: Preview Complete! $scannedCount file(s) scanned, $convertedCount would be converted, $failedCount failed." -ForegroundColor Yellow
} else {
    Write-Host "`nCompleted: $scannedCount file(s) scanned, $convertedCount converted, $failedCount failed." -ForegroundColor Cyan
}

if ($failedCount -gt 0 -or $script:HadFailure) {
    exit 1
}

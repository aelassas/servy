#Requires -Version 5.0
<#
.SYNOPSIS
    Recursively converts text files in the current folder and subfolders to UTF-8 without BOM with Windows (CRLF) line endings.

.DESCRIPTION
    Traverses the current directory recursively, skipping specified directories (e.g., bin, obj,
    node_modules) and file types/names (e.g., .exe, .7z, .coverage.xml, coverage.cobertura.xml).
    Normalizes line endings to Windows CRLF (`r`n) and re-writes file content using 
    [System.IO.File]::WriteAllText with UTF-8 (no BOM).

.PARAMETER ExcludeDirs
    Array of folder names to exclude from processing. Defaults to 'bin', 'obj', 'packages', '.git', '.vs', 'node_modules', 'coveragereport'.

.PARAMETER ExcludeExtensions
    Array of file extensions to exclude. Supports compound extensions like '.coverage.xml'. Defaults to '.exe', '.dll', '.7z', '.coverage.xml'.

.PARAMETER ExcludeFiles
    Array of specific filenames to exclude. Defaults to 'coverage.cobertura.xml'.

.NOTES
    Target Runtime: PowerShell 5.0+
    Excludes the running script itself from conversion.

.EXAMPLE
    .\Convert-ToUtf8NoBom.ps1
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string[]]$ExcludeDirs = @('bin', 'obj', 'packages', '.git', '.vs', 'node_modules', 'coveragereport'),

    [Parameter(Mandatory = $false)]
    [string[]]$ExcludeExtensions = @('.Designer.cs', '.exe', '.pdb', '.dll', '.7z', '.coverage.xml', '.ico', '.png', '.bmp', '.cur', '.res', '.snk', '.pfx'),

    [Parameter(Mandatory = $false)]
    [string[]]$ExcludeFiles = @('coverage.cobertura.xml')
)

# Construct UTF-8 encoding object without BOM (Byte Order Mark)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

# Get current execution directory and script path
$currentDir = Get-Location
$scriptPath = $MyInvocation.MyCommand.Path

Write-Host "Starting UTF-8 (no BOM) & CRLF conversion in: $currentDir" -ForegroundColor Cyan
Write-Host "Excluding directories   : $($ExcludeDirs -join ', ')" -ForegroundColor Yellow
Write-Host "Excluding extensions    : $($ExcludeExtensions -join ', ')" -ForegroundColor Yellow
Write-Host "Excluding specific files: $($ExcludeFiles -join ', ')" -ForegroundColor Yellow
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

# Collect target files using early directory pruning
$files = Get-FilteredFiles -Path $currentDir.Path `
                           -DirExclusions $ExcludeDirs `
                           -ExtExclusions $ExcludeExtensions `
                           -FileExclusions $ExcludeFiles

$convertedCount = 0
$failedCount = 0

foreach ($file in $files) {
    # Skip the script itself if executed from within the same directory
    if ($null -ne $scriptPath -and $file.FullName -eq $scriptPath) {
        continue
    }

    try {
        # Read content using .NET to preserve raw text structure
        $content = [System.IO.File]::ReadAllText($file.FullName)

        # Normalize all line returns (CRLF, LF, CR) to Windows CRLF (`r`n)
        $crlfContent = $content -replace "`r?`n|`r", "`r`n"

        # Write back as UTF-8 without BOM
        [System.IO.File]::WriteAllText($file.FullName, $crlfContent, $utf8NoBom)

        Write-Host "Converted: $($file.FullName)" -ForegroundColor Green
        $convertedCount++
    }
    catch {
        Write-Warning "Failed to convert $($file.FullName): $_"
        $failedCount++
    }
}

Write-Host "`nCompleted: $convertedCount file(s) converted successfully, $failedCount failed." -ForegroundColor Cyan

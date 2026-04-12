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
$fullVersion = if ($Version -match "^\d+\.\d+$") { "$Version.0" } else { $Version }
$fileVersion = if ($Version -match "^\d+\.\d+$") { "$Version.0.0" } else { "$Version.0.0" }

Write-Host "Updating Servy version to $Version..."

# Base directory of the script
$baseDir = $PSScriptRoot

# ----------------------------------------------------------------------
# Helper: Safe File Update (Prevents script crash on missing files)
# ----------------------------------------------------------------------
function Update-FileContent {
    param([string]$Path, [string]$Pattern, [string]$Replacement)
    
    if (Test-Path $Path) {
        $content = [System.IO.File]::ReadAllText($Path)
        # Use ExplicitCapture and IgnoreCase for legacy AssemblyInfo files
        $newContent = [regex]::Replace($content, $Pattern, { 
            param($m) "$($m.Groups[1].Value)$Replacement$($m.Groups[2].Value)" 
        }, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        
        [System.IO.File]::WriteAllText($Path, $newContent)
        Write-Host "Updated: $Path" -ForegroundColor Green
    } else {
        Write-Warning "Skipping missing file: $Path"
    }
}

# 1. Update setup\publish.ps1
Update-FileContent `
    -Path (Join-Path $baseDir "setup\publish.ps1") `
    -Pattern '(\$version\s*=\s*")[^"]*(")' `
    -Replacement $Version

# 2. Update src\Servy.Core\Config\AppConfig.cs
Update-FileContent `
    -Path (Join-Path $baseDir "src\Servy.Core\Config\AppConfig.cs") `
    -Pattern '(public static readonly string Version\s*=\s*")[^"]*(";)' `
    -Replacement $Version

# 3. Update all AssemblyInfo.cs files (Recursive)
Get-ChildItem -Path $baseDir -Recurse -Filter AssemblyInfo.cs -ErrorAction SilentlyContinue | ForEach-Object {
    $path = $_.FullName
    $content = [System.IO.File]::ReadAllText($path)

    # Chain AssemblyVersion and AssemblyFileVersion updates
    $content = [regex]::Replace($content, '(\[assembly:\s*AssemblyVersion\(")[^"]*("\)\])', { param($m) "$($m.Groups[1].Value)$fileVersion$($m.Groups[2].Value)" }, "IgnoreCase")
    $content = [regex]::Replace($content, '(\[assembly:\s*AssemblyFileVersion\(")[^"]*("\)\])', { param($m) "$($m.Groups[1].Value)$fileVersion$($m.Groups[2].Value)" }, "IgnoreCase")

    [System.IO.File]::WriteAllText($path, $content)
    Write-Host "Updated AssemblyInfo: $path" -ForegroundColor Gray
}

# 4. Update src\Servy.CLI\Servy.psd1
Update-FileContent `
    -Path (Join-Path $baseDir "src\Servy.CLI\Servy.psd1") `
    -Pattern "(ModuleVersion\s*=\s*')[^']*(')" `
    -Replacement $fullVersion

Write-Host "`nAll legacy and metadata version updates complete." -ForegroundColor Green
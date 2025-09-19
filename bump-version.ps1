param(
    [Parameter(Mandatory = $true, Position = 0)]
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
$content = [System.IO.File]::ReadAllText($PublishPath)
$content = [regex]::Replace(
    $content,
    '(\$version\s*=\s*")[^"]*(")',
    { param($m) "$($m.Groups[1].Value)$Version$($m.Groups[2].Value)" }
)
[System.IO.File]::WriteAllText($PublishPath, $content)
Write-Host "Updated $PublishPath"

# 2. Update src\Servy.Core\Config\AppConfig.cs
$AppConfigPath = Join-Path $BaseDir "src\Servy.Core\Config\AppConfig.cs"
if (-Not (Test-Path $AppConfigPath)) { Write-Error "File not found: $AppConfigPath"; exit 1 }
$content = [System.IO.File]::ReadAllText($AppConfigPath)
$content = [regex]::Replace(
    $content,
    '(public static readonly string Version\s*=\s*")[^"]*(";)',
    { param($m) "$($m.Groups[1].Value)$Version$($m.Groups[2].Value)" }
)
[System.IO.File]::WriteAllText($AppConfigPath, $content)
Write-Host "Updated $AppConfigPath"

# 4. Update all AssemblyInfo.cs files recursively
Get-ChildItem -Path $BaseDir -Recurse -Filter AssemblyInfo.cs | ForEach-Object {
    $assemblyInfo = $_.FullName
    $content = [System.IO.File]::ReadAllText($assemblyInfo)

    # Update [assembly: AssemblyVersion("1.0.0.0")]
    $content = [regex]::Replace(
        $content,
        '(\[assembly:\s*AssemblyVersion\(")[^"]*("\)\])',
        { param($m) "$($m.Groups[1].Value)$FileVersion$($m.Groups[2].Value)" }
    )

    # Update [assembly: AssemblyFileVersion("1.0.0.0")]
    $content = [regex]::Replace(
        $content,
        '(\[assembly:\s*AssemblyFileVersion\(")[^"]*("\)\])',
        { param($m) "$($m.Groups[1].Value)$FileVersion$($m.Groups[2].Value)" }
    )

    [System.IO.File]::WriteAllText($assemblyInfo, $content)
    Write-Host "Updated $assemblyInfo"
}

Write-Host "All version updates complete."

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
    '(\[string\]\$version\s*=\s*")[^"]*(")',
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

# 3. Update all *.csproj files recursively
Get-ChildItem -Path $BaseDir -Recurse -Filter *.csproj | ForEach-Object {
    $csproj = $_.FullName
    $content = [System.IO.File]::ReadAllText($csproj)

    # Update <Version>
    $content = [regex]::Replace(
        $content,
        '(<Version>)[^<]*(</Version>)',
        { param($m) "$($m.Groups[1].Value)$FullVersion$($m.Groups[2].Value)" }
    )

    # Update <FileVersion>
    $content = [regex]::Replace(
        $content,
        '(<FileVersion>)[^<]*(</FileVersion>)',
        { param($m) "$($m.Groups[1].Value)$FileVersion$($m.Groups[2].Value)" }
    )

    # Update <AssemblyVersion>
    $content = [regex]::Replace(
        $content,
        '(<AssemblyVersion>)[^<]*(</AssemblyVersion>)',
        { param($m) "$($m.Groups[1].Value)$FileVersion$($m.Groups[2].Value)" }
    )
    [System.IO.File]::WriteAllText($csproj, $content)
    Write-Host "Updated $csproj"
}

Write-Host "All version updates complete."

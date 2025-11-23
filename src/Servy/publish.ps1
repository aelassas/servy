<#
.SYNOPSIS
Builds the Servy WPF application in Release or Debug mode and signs the output.

.DESCRIPTION
This script prepares the Servy WPF application for publishing by:
1. Running either publish-res-release.ps1 or publish-res-debug.ps1 depending
   on the selected build configuration.
2. Building the Servy WPF project using MSBuild.
3. Signing the produced Servy.exe binary using the SignPath signing script.

.PARAMETER BuildConfiguration
Specifies the build configuration. Default is "Release".
Accepted values are typically "Debug" or "Release".

.PARAMETER Pause
Optional switch that pauses execution at the end of the script.

.NOTES
- Requires msbuild to be available in PATH.
- Script must be executed using PowerShell (x64 recommended).
- Adjust paths if project structure changes.
- Servy.exe is signed only after a successful build.
- The signing script path is resolved relative to this script's directory.

.EXAMPLE
.\publish.ps1
Builds the project in Release mode and signs the output.

.EXAMPLE
.\publish.ps1 -BuildConfiguration Debug
Builds using Debug mode and runs publish-res-debug.ps1 beforehand.
#>


param(
	[string]$BuildConfiguration = "Release",
    [switch]$Pause
)

$ErrorActionPreference = "Stop"

# Get the directory of the current script
$ScriptDir            = Split-Path -Parent $MyInvocation.MyCommand.Path

# Configuration
$Platform             = "x64"
$SignPath             = Join-Path $ScriptDir "..\..\setup\signpath.ps1" | Resolve-Path
$PublishFolder        = Join-Path $ScriptDir "bin\$Platform\$BuildConfiguration"

# Paths
$ProjectPath = Join-Path $ScriptDir "Servy.csproj"

# Step 1: Run publish-res-release.ps1
$PublishResScriptName = if ($BuildConfiguration -eq "Debug") { "publish-res-debug.ps1" } else { "publish-res-release.ps1" }
$PublishResScript = Join-Path $ScriptDir $PublishResScriptName

if (-not (Test-Path $PublishResScript)) {
    Write-Error "Required script not found: $PublishResScript"
    exit 1
}

Write-Host "=== Running $PublishResScriptName ==="
& $PublishResScript -tfm $tfm
if ($LASTEXITCODE -ne 0) {
    Write-Error "$PublishResScriptName failed."
    exit $LASTEXITCODE
}
Write-Host "=== Completed $PublishResScriptName ===`n"

# Step 2: Build project with MSBuild
Write-Host "Building Servy project in $BuildConfiguration mode..."
& msbuild $ProjectPath /t:Clean,Rebuild /p:Configuration=$BuildConfiguration /p:AllowUnsafeBlocks=true /p:Platform=$Platform
Write-Host "Build completed."

# Step 3: Sign the published executable if signing is enabled
if ($BuildConfiguration -eq "Release") {
    $ExePath = Join-Path $PublishFolder "Servy.exe" | Resolve-Path
    & $SignPath $ExePath

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Signing Servy.exe failed."
        exit $LASTEXITCODE
    }
}

if ($Pause) { 
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
<#
.SYNOPSIS
Builds the Servy CLI application in Release or Debug mode and signs the output.

.DESCRIPTION
This script prepares the Servy CLI application for publishing by:
1. Running publish-res-release.ps1 or publish-res-debug.ps1 depending on the
   specified build configuration, to generate required resource binaries.
2. Cleaning and rebuilding the Servy.CLI.csproj project using MSBuild.
3. Signing the resulting Servy.CLI.exe executable using the SignPath script.

.PARAMETER BuildConfiguration
Specifies the build configuration. Default is "Release".
Typical values: "Release" or "Debug".

.PARAMETER Pause
Optional switch that pauses execution before the script exits.

.REQUIREMENTS
- MSBuild must be available in PATH.
- publish-res-release.ps1 and publish-res-debug.ps1 must exist in the same folder.
- Script should be executed from PowerShell (x64).

.NOTES
- The signing script (signpath.ps1) must exist under setup\.
- The output executable is signed only after a successful build.
- Adjust paths if project structure changes.

.EXAMPLE
.\publish-cli.ps1
Builds the Servy CLI in Release mode and signs the output.

.EXAMPLE
.\publish-cli.ps1 -BuildConfiguration Debug
Builds the Servy CLI using Debug mode and runs publish-res-debug.ps1.
#>

param(
	[string]$BuildConfiguration = "Release",
    [switch]$Pause
)

$ErrorActionPreference = "Stop"

# Determine script directory
$ScriptDir             = Split-Path -Parent $MyInvocation.MyCommand.Path

# Configuration
$Platform              = "x64"
$SignPath              = Join-Path $ScriptDir "..\..\setup\signpath.ps1" | Resolve-Path
$PublishFolder         = Join-Path $ScriptDir "bin\$Platform\$BuildConfiguration"

# Project path
$ProjectPath = Join-Path $ScriptDir "Servy.CLI.csproj"

# Step 0: Run publish-res-release.ps1
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

# Step 1: Clean and build the CLI project
Write-Host "Building Servy.CLI project in $BuildConfiguration mode..."
& msbuild $ProjectPath /t:Clean,Rebuild /p:Configuration=$BuildConfiguration /p:AllowUnsafeBlocks=true /p:Platform=$Platform
Write-Host "Build completed."

# Step 2: Sign the published executable if signing is enabled
if ($BuildConfiguration -eq "Release") {
    $ExePath = Join-Path $PublishFolder "Servy.CLI.exe" | Resolve-Path
    & $SignPath $ExePath

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Signing Servy.CLI.exe failed."
        exit $LASTEXITCODE
    }
}

if ($Pause) { 
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
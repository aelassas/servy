<#
.SYNOPSIS
Builds and publishes the Servy WPF application and optionally signs the output executable.

.DESCRIPTION
This script performs the following steps:
1. Runs the resource publishing script (publish-res-release.ps1 or publish-res-debug.ps1 depending on configuration).
2. Builds the Servy.Manager project using MSBuild in the specified configuration and platform.
3. Signs the resulting executable using SignPath if signing is enabled.

.PARAMETER BuildConfiguration
Specifies the build configuration for the project. Default is "Release".
Common values: "Release" or "Debug".

.PARAMETER Pause
Optional switch to pause the script at the end of execution.

.REQUIREMENTS
- MSBuild must be installed and available in PATH.
- publish-res-release.ps1 and publish-res-debug.ps1 must exist in the same directory as this script.
- SignPath.ps1 must exist under ..\..\setup\.
- Script should be executed from PowerShell (x64).

.NOTES
- The output executable is located under bin\x64\<BuildConfiguration>\Servy.Manager.exe.
- Adjust file paths if the project structure changes.
- Author: Akram El Assas

.EXAMPLE
.\publish.ps1
Builds Servy.Manager in Release mode, runs the resource publishing script, and signs the output.

.EXAMPLE
.\publish.ps1 -BuildConfiguration Debug -Pause
Builds Servy.Manager in Debug mode, publishes resources, signs the output, and pauses before exiting.
#>

param(
	[string]$BuildConfiguration = "Release",
    [switch]$Pause
)

$ErrorActionPreference = "Stop"

# Get the directory of the current script
$ScriptDir             = Split-Path -Parent $MyInvocation.MyCommand.Path

# Configuration
$Platform              = "x64"
$SignPath              = Join-Path $ScriptDir "..\..\setup\signpath.ps1" | Resolve-Path
$PublishFolder         = Join-Path $ScriptDir "bin\$Platform\$BuildConfiguration"

# Paths
$ProjectPath = Join-Path $ScriptDir "Servy.Manager.csproj"

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
Write-Host "Building Servy.Manager project in $BuildConfiguration mode..."
& msbuild $ProjectPath /t:Clean,Rebuild /p:Configuration=$BuildConfiguration /p:AllowUnsafeBlocks=true /p:Platform=$Platform
Write-Host "Build completed."

# Step 3: Sign the published executable if signing is enabled
if ($BuildConfiguration -eq "Release") {
    $ExePath = Join-Path $PublishFolder "Servy.Manager.exe" | Resolve-Path
    & $SignPath $ExePath

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Signing Servy.Manager.exe failed."
        exit $LASTEXITCODE
    }
}

if ($Pause) { 
    Write-Host "`nPress any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
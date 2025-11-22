<#
.SYNOPSIS
    Publishes the Servy WPF application as a self-contained executable and signs it.

.DESCRIPTION
    This script performs the following steps:
      1. Runs the resource publishing script (`publish-res-release.ps1`)
      2. Builds and publishes `Servy.csproj` as a self-contained win-x64 executable
      3. Signs the published executable using SignPath (if enabled)

.PARAMETER Tfm
    Target Framework Moniker (default: "net10.0-windows").

.PARAMETER BuildConfiguration
    Build configuration to use (default: "Release").

.PARAMETER Runtime
    Target runtime identifier (RID) for publishing (default: "win-x64").

.NOTES
    Requirements:
      - .NET SDK must be installed
      - The SignPath script (signpath.ps1) must exist in ../../setup/

.EXAMPLE
    ./publish.ps1
    Publishes using default parameters.

.EXAMPLE
    ./publish.ps1 -Tfm "net10.0-windows" -BuildConfiguration "Debug" -Runtime "win-x64"
#>

param(
    # Target framework (default: net10.0-windows)
    [string]$Tfm                = "net10.0-windows",
    [string]$BuildConfiguration = "Release",
    [string]$Runtime            = "win-x64"
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Script directory (so we can run from anywhere)
# ---------------------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# SignPath script path
# ---------------------------------------------------------------------------------
$SignPath = Join-Path $ScriptDir "..\..\setup\signpath.ps1" | Resolve-Path

# ---------------------------------------------------------------------------------
# Step 0: Publish resources
# ---------------------------------------------------------------------------------
$PublishResScriptName = if ($BuildConfiguration -eq "Debug") { "publish-res-debug.ps1" } else { "publish-res-release.ps1" }
$PublishResScript = Join-Path $ScriptDir $PublishResScriptName

if (-not (Test-Path $PublishResScript)) {
    Write-Error "Required script not found: $PublishResScript"
    exit 1
}

Write-Host "=== Running $PublishResScriptName ==="
& $PublishResScript -Tfm $Tfm
if ($LASTEXITCODE -ne 0) {
    Write-Error "$PublishResScriptName failed."
    exit $LASTEXITCODE
}
Write-Host "=== Completed $PublishResScriptName ===`n"

# ---------------------------------------------------------------------------------
# Step 1: Build and publish Servy.csproj (Self-contained, win-x64)
# ---------------------------------------------------------------------------------
$ProjectPath = Join-Path $ScriptDir "Servy.csproj" | Resolve-Path

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

Write-Host "=== Publishing Servy.csproj ==="
Write-Host "Target Framework: $Tfm"
Write-Host "Configuration: $BuildConfiguration"
Write-Host "Runtime: $Runtime"
Write-Host "Self-contained: true"

& dotnet clean $ProjectPath -c $BuildConfiguration

& dotnet publish $ProjectPath `
    -c $BuildConfiguration `
    -r $Runtime `
    --self-contained true `
    --force `
    /p:DeleteExistingFiles=true `
    /p:PublishSingleFile=true `
    /p:IncludeAllContentForSelfExtract=true `
    /p:PublishTrimmed=false `

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit $LASTEXITCODE
}

# ---------------------------------------------------------------------------------
# Step 2: Sign the published executable if signing is enabled
# ---------------------------------------------------------------------------------
if ($BuildConfiguration -eq "Release") {
    $BasePath      = Join-Path $ScriptDir "..\Servy\bin\$BuildConfiguration\$Tfm\$Runtime"
    $PublishFolder = Join-Path $BasePath "publish"
    $ExePath       = Join-Path $PublishFolder "Servy.exe" | Resolve-Path
    & $SignPath $ExePath

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Signing Servy.exe failed."
        exit $LASTEXITCODE
    }
}

Write-Host "=== Servy.csproj published successfully ==="

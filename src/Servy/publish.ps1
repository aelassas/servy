param(
    # Target framework (default: net10.0-windows)
    [string]$tfm = "net10.0-windows"
)

$ErrorActionPreference = "Stop"

$buildConfiguration = "Release"
$runtime            = "win-x64"

# ---------------------------------------------------------------------------------
# Script directory (so we can run from anywhere)
# ---------------------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# SignPath script path
# ---------------------------------------------------------------------------------
$SignPath = Join-Path $ScriptDir "..\..\setup\signpath.ps1" | Resolve-Path

# ---------------------------------------------------------------------------------
# Step 0: Run publish-res-release.ps1 (Resource publishing step)
# ---------------------------------------------------------------------------------
$PublishResScript = Join-Path $ScriptDir "publish-res-release.ps1"

if (-not (Test-Path $PublishResScript)) {
    Write-Error "Required script not found: $PublishResScript"
    exit 1
}

Write-Host "=== Running publish-res-release.ps1 ==="
& $PublishResScript -tfm $tfm
if ($LASTEXITCODE -ne 0) {
    Write-Error "publish-res-release.ps1 failed."
    exit $LASTEXITCODE
}
Write-Host "=== Completed publish-res-release.ps1 ===`n"

# ---------------------------------------------------------------------------------
# Step 1: Build and publish Servy.csproj (Self-contained, win-x64)
# ---------------------------------------------------------------------------------
$ProjectPath = Join-Path $ScriptDir "Servy.csproj"

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

Write-Host "=== Publishing Servy.csproj ==="
Write-Host "Target Framework: $tfm"
Write-Host "Configuration: $buildConfiguration"
Write-Host "Runtime: $runtime"
Write-Host "Self-contained: true"

& dotnet clean $ProjectPath -c $buildConfiguration

& dotnet publish $ProjectPath `
    -c $buildConfiguration `
    -r $runtime `
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
$basePath      = Join-Path $ScriptDir "..\Servy\bin\$buildConfiguration\$tfm\$runtime"
$publishFolder = Join-Path $basePath "publish"
$exePath       = Join-Path $publishFolder "Servy.exe"
& $signPath $exePath

if ($LASTEXITCODE -ne 0) {
    Write-Error "Signing Servy.exe failed."
    exit $LASTEXITCODE
}

Write-Host "=== Servy.csproj published successfully ==="

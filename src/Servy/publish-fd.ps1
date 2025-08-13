param(
    # Target framework (default: net8.0-windows)
    [string]$tfm = "net8.0-windows"
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Script directory (so the script can be run from anywhere)
# ---------------------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# Step 0: Run publish-res-release.ps1 (resource publishing step)
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
# Step 1: Clean and publish Servy.csproj (Framework-dependent, win-x64)
# ---------------------------------------------------------------------------------
$ProjectPath   = Join-Path $ScriptDir "Servy.csproj"
$PublishFolder = Join-Path $ScriptDir "bin\Release\$tfm\win-x64\publish"

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

# Remove old publish output if it exists
if (Test-Path $PublishFolder) {
    Write-Host "Removing old publish folder: $PublishFolder"
    Remove-Item -Recurse -Force $PublishFolder
}

Write-Host "=== Publishing Servy.csproj ==="
Write-Host "Target Framework : $tfm"
Write-Host "Configuration    : Release"
Write-Host "Runtime          : win-x64"
Write-Host "Self-contained   : false"
Write-Host "Single File      : false"

& dotnet publish $ProjectPath `
    -c Release `
    -r win-x64 `
    --self-contained false `
    /p:PublishSingleFile=false `
    /p:IncludeAllContentForSelfExtract=true `
    /p:PublishTrimmed=false `
    --no-restore `
    --nologo `
    --verbosity minimal `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:ErrorOnDuplicatePublishOutputFiles=true `
    -p:GeneratePackageOnBuild=false `
    -p:UseAppHost=true `
    -p:Clean=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit $LASTEXITCODE
}

Write-Host "=== Servy.csproj published successfully ==="


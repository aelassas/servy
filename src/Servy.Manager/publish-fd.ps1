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
# Step 0: Clean and publish Servy.Manager.csproj (Framework-dependent, win-x64)
# ---------------------------------------------------------------------------------
$ProjectPath   = Join-Path $ScriptDir "Servy.Manager.csproj"
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

Write-Host "=== Publishing Servy.Manager.csproj ==="
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

Write-Host "=== Servy.Manager.csproj published successfully ==="


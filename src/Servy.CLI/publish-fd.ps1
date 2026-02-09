<#
.SYNOPSIS
    Publishes the Servy.CLI project (framework-dependent) for Windows.

.DESCRIPTION
    This script performs a framework-dependent publish of the Servy.CLI application
    targeting the win-x64 runtime. It first runs the resource publishing script
    (publish-res-release.ps1), cleans any previous publish output, and then publishes
    the project with the specified target framework.

PARAMETER tfm
    The target framework for publishing. Default is "net10.0-windows".

NOTES
    - Requires .NET SDK installed and 'dotnet' available in PATH.
    - Can be run from any location; paths are resolved relative to the script location.
    - Produces output in 'bin\Release\<tfm>\win-x64\publish'.
    - Self-contained and single-file options are disabled for framework-dependent builds.

.EXAMPLE
    PS> .\publish-fd.ps1
    Publishes Servy.CLI using the default target framework (net10.0-windows).

.EXAMPLE
    PS> .\publish-fd.ps1 -Tfm net10-windows
    Publishes Servy.CLI targeting .NET 10.
#>

param(
    # Target framework (default: net10.0-windows)
    [string]$Tfm                = "net10.0-windows",
    [string]$BuildConfiguration = "Release"
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Script directory (so the script works regardless of where it's run from)
# ---------------------------------------------------------------------------------
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# Step 0: Run publish-res-release.ps1 (publish resources first)
# ---------------------------------------------------------------------------------
$publishResScriptName = if ($BuildConfiguration -eq "Debug") { "publish-res-debug.ps1" } else { "publish-res-release.ps1" }
$publishResScript = Join-Path $scriptDir $publishResScriptName

if (-not (Test-Path $publishResScript)) {
    Write-Error "Required script not found: $publishResScript"
    exit 1
}

Write-Host "=== Running $publishResScriptName ==="
& $publishResScript -Tfm $Tfm
if ($LASTEXITCODE -ne 0) {
    Write-Error "$publishResScriptName failed."
    exit $LASTEXITCODE
}
Write-Host "=== Completed $publishResScriptName ===`n"

# ---------------------------------------------------------------------------------
# Step 1: Clean and publish Servy.CLI.csproj (Framework-dependent, win-x64)
# ---------------------------------------------------------------------------------
$projectPath   = Join-Path $scriptDir "Servy.CLI.csproj" | Resolve-Path
$publishFolder = Join-Path $scriptDir "bin\Release\$Tfm\win-x64\publish"

if (-not (Test-Path $projectPath)) {
    Write-Error "Project file not found: $projectPath"
    exit 1
}

# Remove old publish output if it exists
if (Test-Path $publishFolder) {
    Write-Host "Removing old publish folder: $publishFolder"
    Remove-Item -Recurse -Force $publishFolder
}

Write-Host "=== Publishing Servy.CLI.csproj ==="
Write-Host "Target Framework : $Tfm"
Write-Host "Configuration    : Release"
Write-Host "Runtime          : win-x64"
Write-Host "Self-contained   : false"
Write-Host "Single File      : false"

& dotnet publish $projectPath `
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
    -p:Clean=true `
    /p:DeleteExistingFiles=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit $LASTEXITCODE
}

Write-Host "=== Servy.CLI.csproj published successfully ==="

<#
.SYNOPSIS
    Publishes Servy.Service and copies its build artifacts into the Servy.CLI Resources folder for debug builds.

.DESCRIPTION
    This script builds the Servy.Service project targeting the specified framework
    in Debug configuration. It produces a self-contained single-file executable
    and copies the resulting executable and PDB files into the Servy.CLI Resources
    folder, renaming them as needed. Optional core and infrastructure PDBs can also
    be copied if required.

.PARAMETER Tfm
    The target framework to build against. Default is "net10.0-windows".

.NOTES
    - Requires .NET SDK installed and 'dotnet' available in PATH.
    - Can be run from any working directory; paths are resolved relative to the script location.
    - Produces output in 'Servy.Service\bin\Debug\<tfm>\win-x64\publish'.

.EXAMPLE
    PS> .\publish-res-debug.ps1
    Publishes Servy.Service in Debug mode using the default target framework and copies artifacts to the CLI resources folder.

.EXAMPLE
    PS> .\publish-res-debug.ps1 -Tfm net10.0-windows
    Publishes Servy.Service targeting .NET 10 and copies the artifacts.
#>

param(
    # Target framework for build (default: net10.0-windows)
    [string]$Tfm = "net10.0-windows"
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------------
# Script directory (ensures relative paths work)
# ---------------------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------
# Paths & build configuration
# ---------------------------------------------------------------------------------
$ServiceDir         = Join-Path $ScriptDir "..\Servy.Service" | Resolve-Path
$ServiceProject     = Join-Path $ServiceDir "Servy.Service.csproj" | Resolve-Path
$ResourcesFolder    = Join-Path $ScriptDir "..\Servy.CLI\Resources" | Resolve-Path
$BuildConfiguration = "Debug"
$Runtime            = "win-x64"
$SelfContained      = $true

# ---------------------------------------------------------------------------------
# Step 1: Publish Servy.Service project
# ---------------------------------------------------------------------------------
$PublishServiceScript = Join-Path $ServiceDir "publish.ps1"

if (-not (Test-Path $PublishServiceScript)) {
    Write-Error "Required script not found: $PublishServiceScript"
    exit 1
}

Write-Host "=== [service] Running publish.ps1 ==="
& $PublishServiceScript -Tfm $Tfm -Configuration $BuildConfiguration
if ($LASTEXITCODE -ne 0) {
    Write-Error "[service] publish.ps1 failed."
    exit $LASTEXITCODE
}
Write-Host "=== [service] Completed publish.ps1 ===`n"

# ---------------------------------------------------------------------------------
# Step 2: Locate publish and build output folders
# ---------------------------------------------------------------------------------
$BasePath      = Join-Path $ScriptDir "..\Servy.Service\bin\$BuildConfiguration\$Tfm\$Runtime"
$PublishFolder = Join-Path $BasePath "publish"
$BuildFolder   = $BasePath

# ---------------------------------------------------------------------------------
# Step 3: Ensure resources folder exists
# ---------------------------------------------------------------------------------
if (-not (Test-Path $ResourcesFolder)) {
    New-Item -ItemType Directory -Path $ResourcesFolder | Out-Null
}

# ---------------------------------------------------------------------------------
# Step 4: Copy artifacts (renaming as needed)
# ---------------------------------------------------------------------------------
Copy-Item -Path (Join-Path $PublishFolder "Servy.Service.exe") `
          -Destination (Join-Path $ResourcesFolder "Servy.Service.CLI.exe") -Force

Copy-Item -Path (Join-Path $BuildFolder "Servy.Service.pdb") `
          -Destination (Join-Path $ResourcesFolder "Servy.Service.CLI.pdb") -Force
<#
Copy-Item -Path (Join-Path $BuildFolder "Servy.Core.pdb") `
          -Destination (Join-Path $ResourcesFolder "Servy.Core.pdb") -Force
#>
# ----------------------------------------------------------------------
# Step 5 - CopyServy.Infrastructure.pdb
# ----------------------------------------------------------------------
<#
$InfraServiceProject = Join-Path $ScriptDir "..\Servy.Infrastructure\Servy.Infrastructure.csproj"
$InfraSourcePath     = Join-Path $ScriptDir "..\Servy.Infrastructure\bin\$BuildConfiguration\$Tfm\$Runtime\Servy.Infrastructure.pdb"
$InfraDestPath       = Join-Path $ResourcesFolder "Servy.Infrastructure.pdb"

dotnet publish $InfraServiceProject `
    -c $BuildConfiguration `
    -r $Runtime `
    --self-contained false `
    /p:TargetFramework=$Tfm `
    /p:PublishSingleFile=false `
    /p:IncludeAllContentForSelfExtract=false `
    /p:PublishTrimmed=false

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed."
    exit $LASTEXITCODE
}

Copy-Item -Path $InfraSourcePath  -Destination $InfraDestPath -Force
Write-Host "Copied Servy.Infrastructure.pdb"
#>

# ---------------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------------
Write-Host "=== $BuildConfiguration build ($Tfm) published successfully to Resources ==="

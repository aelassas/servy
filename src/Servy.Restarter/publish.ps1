# publish-restarter-sc.ps1
# Self-contained publish script for Servy.Restarter
# Requirements: .NET SDK installed and accessible from PATH

param(
    [string]$version = "1.0.0",
    [string]$tfm     = "net8.0-windows",
    [string]$runtime = "win-x64",
    [string]$configuration = "Release",
    [switch]$pause
)

$ErrorActionPreference = "Stop"

# Project and directories
$projectName = "Servy.Restarter"
$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptDir "$projectName.csproj"

# Publish output folder
$publishDir = Join-Path $ScriptDir "bin\$configuration\$tfm\$runtime\publish"

# Step 0: Clean previous publish folder
if (Test-Path $publishDir) {
    Write-Host "Cleaning previous publish folder: $publishDir"
    Remove-Item $publishDir -Recurse -Force
}

# Step 1: Publish project
Write-Host "Publishing $projectName..."
dotnet publish $projectPath `
    -c $configuration `
    -r $runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeAllContentForSelfExtract=true `
    /p:PublishTrimmed=false `
    /p:Version=$version `
    -f $tfm `
    -o $publishDir `
    /p:DeleteExistingFiles=true

Write-Host "Publish completed for $projectName."

if ($pause) { Pause }

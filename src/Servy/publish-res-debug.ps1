param(
    [string]$tfm =  "net8.0-windows"
)


# Get folder of the current script
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Paths
$serviceProject = Join-Path $ScriptDir "..\Servy.Service\Servy.Service.csproj"
$resourcesFolder = Join-Path $ScriptDir "..\Servy\Resources"
$buildConfiguration = "Debug"
$runtime = "win-x64"
$selfContained = $true

# Publish Servy.Service
Write-Host "Publishing Servy.Service (.NET 8.0 Windows) in $buildConfiguration mode..."
dotnet publish $serviceProject `
    -c $buildConfiguration `
    -r $runtime `
    --self-contained $selfContained `
    /p:TargetFramework=$tfm `
    /p:PublishSingleFile=true `
    /p:IncludeAllContentForSelfExtract=true `
    /p:PublishTrimmed=false

# Publish folder contains the single-file EXE
$basePath = Join-Path $ScriptDir "..\Servy.Service\bin\$buildConfiguration\$tfm\$runtime"
$publishFolder = Join-Path $basePath "publish"
$buildFolder = $basePath

# Copy single-file EXE
Copy-Item -Path (Join-Path $publishFolder "Servy.Service.exe") `
          -Destination (Join-Path $resourcesFolder "Servy.Service.exe") -Force

# Copy PDBs from build folder
Copy-Item -Path (Join-Path $buildFolder "Servy.Service.pdb") `
          -Destination (Join-Path $resourcesFolder "Servy.Service.pdb") -Force

Copy-Item -Path (Join-Path $buildFolder "Servy.Core.pdb") `
          -Destination (Join-Path $resourcesFolder "Servy.Core.pdb") -Force

Write-Host "$buildConfiguration build (.NET 8.0 Windows) published successfully to Resources."

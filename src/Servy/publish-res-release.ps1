# publish-release.ps1
$serviceProject = "..\Servy.Service\Servy.Service.csproj"
$resourcesFolder = "..\Servy\Resources"
$buildConfiguration = "Release"
$runtime = "win-x64"
$selfContained = $true
$tfm = "net8.0-windows"

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
$basePath = "..\Servy.Service\bin\$buildConfiguration\$tfm\$runtime"
$publishFolder = Join-Path $basePath "publish"
$buildFolder = $basePath

# 1. Copy single-file EXE
Copy-Item -Path (Join-Path $publishFolder "Servy.Service.exe") `
          -Destination (Join-Path $resourcesFolder "Servy.Service.exe") -Force

# 2. Copy PDBs from build folder
Copy-Item -Path (Join-Path $buildFolder "Servy.Service.pdb") `
          -Destination (Join-Path $resourcesFolder "Servy.Service.pdb") -Force

Copy-Item -Path (Join-Path $buildFolder "Servy.Core.pdb") `
          -Destination (Join-Path $resourcesFolder "Servy.Core.pdb") -Force

Write-Host "$buildConfiguration build (.NET 8.0 Windows) published successfully to Resources."

# publish-release.ps1
$serviceProject = "..\Servy.Service\Servy.Service.csproj"
$resourcesFolder = "..\Servy.CLI\Resources"
$buildConfiguration = "Release"
$buildOutput = "..\Servy.Service\bin\$buildConfiguration"

# 1. Build the project in Release mode
Write-Host "Building Servy.Service in Release mode..."
msbuild $serviceProject /p:Configuration=$buildConfiguration

# 2. Files to copy
$filesToCopy = @(
    @{ Source = "Servy.Service.exe"; Destination = "Servy.Service.Net48.CLI.exe" },
    @{ Source = "Servy.Service.pdb"; Destination = "Servy.Service.Net48.CLI.pdb" },
    @{ Source = "Servy.Core.pdb"; Destination = "Servy.Core.pdb" }
)

# 3. Copy files to Resources folder
foreach ($file in $filesToCopy) {
    $sourcePath = Join-Path $buildOutput $file.Source
    $destPath = Join-Path $resourcesFolder $file.Destination
    Copy-Item -Path $sourcePath -Destination $destPath -Force
    Write-Host "Copied $($file.Source) -> $($file.Destination)"
}

Write-Host "Release build published successfully to Resources."

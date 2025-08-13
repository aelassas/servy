# Step 0: Run publish-res-release.ps1 and wait until it finishes
Write-Host "Running publish-res-release.ps1..."
& ".\publish-res-release.ps1"
Write-Host "Finished publish-res-release.ps1, continuing with main dotnet publish..."

# Step 1: Build and publish main project in Release mode
Write-Host "Publishing main project (.NET 8.0 Release, win-x64, self-contained)..."
dotnet publish -c Release -r win-x64 --self-contained false `
    /p:PublishSingleFile=false `
    /p:IncludeAllContentForSelfExtract=true `
    /p:PublishTrimmed=false
Write-Host "Main project published successfully."

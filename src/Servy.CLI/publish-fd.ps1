param(
    [string]$tfm     = "net8.0-windows"
)

# Get the directory of the current script
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Step 0: Run publish-res-release.ps1 and wait until it finishes
$PublishResScript = Join-Path $ScriptDir "publish-res-release.ps1"
Write-Host "Running publish-res-release.ps1..."
& $PublishResScript -tfm $tfm 
Write-Host "Finished publish-res-release.ps1, continuing with main dotnet publish..."

# Step 1: Build and publish Servy.csproj in Release mode
$ProjectPath = Join-Path $ScriptDir "Servy.CLI.csproj"

Write-Host "Publishing Servy.csproj (.NET 8.0 Release, win-x64, self-contained)..."
& dotnet publish $ProjectPath -c Release -r win-x64 --self-contained false `
    /p:PublishSingleFile=false `
    /p:IncludeAllContentForSelfExtract=true `
    /p:PublishTrimmed=false

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed." -ForegroundColor Red
    exit $LASTEXITCODE
}



Write-Host "Servy.csproj published successfully."

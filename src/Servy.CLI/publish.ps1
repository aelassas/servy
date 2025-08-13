$buildConfiguration = "Release"

# Get the directory of the current script
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$ProjectPath = Join-Path $ScriptDir "Servy.CLI.csproj"

# Step 0: Run publish-res-release.ps1 and wait until it finishes
$PublishResScript = Join-Path $ScriptDir "publish-res-release.ps1"
Write-Host "Running publish-res-release.ps1..."
& $PublishResScript -tfm $tfm
Write-Host "Finished publish-res-release.ps1, continuing with main dotnet publish..."


msbuild $ProjectPath /t:Clean,Build /p:Configuration=$buildConfiguration
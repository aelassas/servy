#Requires -Version 5.0

$setupScript  = Join-Path $PSScriptRoot "..\..\setup\publish-res.ps1"
$targetFolder = Join-Path $PSScriptRoot "..\Servy\Resources"

# Servy.Service.Net48.exe: the service wrapper extracted to %ProgramData%\Servy
& $setupScript -ProjectName "Servy.Service" -TargetResourcesFolder $targetFolder -Configuration "Debug" -IncludeDlls
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Servy.Restarter.Net48.exe: the restart helper extracted next to the service wrapper
& $setupScript -ProjectName "Servy.Restarter" -TargetResourcesFolder $targetFolder -Configuration "Debug"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

#Requires -Version 5.0

param(
    [string]$Tfm     = "",
    [string]$Runtime = "win-x64"
)

$setupScript = Join-Path $PSScriptRoot "..\..\setup\publish-res.ps1"
$targetFolder = Join-Path $PSScriptRoot "Resources"

. (Join-Path $PSScriptRoot "..\..\setup\common-helpers.ps1")

# Servy.Service.exe: the service wrapper extracted to %ProgramData%\Servy
& $setupScript -ProjectName "Servy.Service" -TargetResourcesFolder $targetFolder -Configuration "Release" -Tfm $Tfm -Runtime $Runtime
Assert-LastExitCode "publish-res.ps1 failed for Servy.Service"

# Servy.Restarter.exe: the restart helper extracted next to the service wrapper
& $setupScript -ProjectName "Servy.Restarter" -TargetResourcesFolder $targetFolder -Configuration "Release" -Tfm $Tfm -Runtime $Runtime
Assert-LastExitCode "publish-res.ps1 failed for Servy.Restarter"

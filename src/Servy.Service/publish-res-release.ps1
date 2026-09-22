#Requires -Version 5.0

param(
    [string]$Tfm     = "",
    [string]$Runtime = "win-x64"
)

$setupScript = Join-Path $PSScriptRoot "..\..\setup\publish-res.ps1"
$targetFolder = Join-Path $PSScriptRoot "Resources"

& $setupScript -ProjectName "Servy.Restarter" -TargetResourcesFolder $targetFolder -Configuration "Release" -Tfm $Tfm -Runtime $Runtime

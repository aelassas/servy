#requires -Version 5.0
param(
    [string]$Tfm     = "net10.0-windows",
    [string]$Runtime = "win-x64"
)

$setupScript = Join-Path $PSScriptRoot "..\..\setup\publish-res.ps1"
$targetFolder = Join-Path $PSScriptRoot "..\Servy\Resources"

& $setupScript -ProjectName "Servy.Service" -TargetResourcesFolder $targetFolder -Configuration "Debug" -Tfm $Tfm -Runtime $Runtime
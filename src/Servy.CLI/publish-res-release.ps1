#requires -Version 5.0
param(
    [string]$Tfm = "net10.0-windows"
)

$setupScript = Join-Path $PSScriptRoot "..\..\setup\publish-res.ps1"
$targetFolder = Join-Path $PSScriptRoot "..\Servy.CLI\Resources"

& $setupScript -ProjectName "Servy.Service" -TargetResourcesFolder $targetFolder -Configuration "Release" -OutputSuffix "CLI" -Tfm $Tfm
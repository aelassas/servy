#requires -Version 5.0

$setupScript  = Join-Path $PSScriptRoot "..\..\setup\publish-res.ps1"
$targetFolder = Join-Path $PSScriptRoot "..\Servy.Service\Resources"

& $setupScript -ProjectName "Servy.Restarter" -TargetResourcesFolder $targetFolder -Configuration "Debug"
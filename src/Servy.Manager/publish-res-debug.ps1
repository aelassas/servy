#requires -Version 5.0

$setupScript  = Join-Path $PSScriptRoot "..\..\setup\publish-res.ps1"
$targetFolder = Join-Path $PSScriptRoot "..\Servy.Manager\Resources"

& $setupScript -ProjectName "Servy.Service" -TargetResourcesFolder $targetFolder -Configuration "Debug" -IncludeDlls
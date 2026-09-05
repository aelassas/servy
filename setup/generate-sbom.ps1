<#
.SYNOPSIS
    Generates a unified CycloneDX SBOM for the five Servy executables.

.DESCRIPTION
    Maps solution assembly structures, runs dotnet-CycloneDX dependency tracking per project,
    merges the individual component SBOM XML files with the cyclonedx CLI, and cleans up
    intermediate files.

.PARAMETER BaseVersion
    Product version in Major.Minor form (e.g. "10.0"); ".0" is appended to meet the CycloneDX schema version requirements.

.PARAMETER OutputFile
    Destination file path where the merged, unified CycloneDX SBOM XML will be written.

.NOTES
    Requires dotnet-CycloneDX and the cyclonedx CLI to be available on PATH.

.EXAMPLE
    .\generate-sbom.ps1 -BaseVersion 10.0 -OutputFile servy-10.0-sbom.xml
#>

param (
    [Parameter(Mandatory=$true)]
    [ValidatePattern('^\d+\.\d+$')]
    [string]$BaseVersion,

    [Parameter(Mandatory=$true)]
    [string]$OutputFile
)

# Append the .0 patch component the CycloneDX schema requires
$FullSbomVersion = "$($BaseVersion).0"

$projects = @(
    @{ Path = 'src\Servy\Servy.csproj';                      File = 'sbom-Servy.xml' }
    @{ Path = 'src\Servy.CLI\Servy.CLI.csproj';              File = 'sbom-Servy.CLI.xml' }
    @{ Path = 'src\Servy.Manager\Servy.Manager.csproj';      File = 'sbom-Servy.Manager.xml' }
    @{ Path = 'src\Servy.Restarter\Servy.Restarter.csproj';  File = 'sbom-Servy.Restarter.xml' }
    @{ Path = 'src\Servy.Service\Servy.Service.csproj';      File = 'sbom-Servy.Service.xml' }
)

$inputFiles = $projects | ForEach-Object { $_.File }

try {
    # Explicitly check for native command failures to prevent partial SBOMs
    foreach ($p in $projects) {
        dotnet-CycloneDX $p.Path --recursive --set-version "$FullSbomVersion" --output . --filename $p.File
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet-CycloneDX failed for $($p.Path) (exit $LASTEXITCODE)"
        }
    }

    # Merge all component project files into the single specified target output file
    cyclonedx merge --input-files $inputFiles --output-file "$OutputFile"
    if ($LASTEXITCODE -ne 0) {
        throw "cyclonedx merge failed (exit $LASTEXITCODE)"
    }
}
finally {
    # Clean up intermediate component SBOM files after generation pass
    foreach ($file in $inputFiles) {
        if (Test-Path $file) {
            Remove-Item -Path $file -Force -ErrorAction SilentlyContinue
        }
    }
}

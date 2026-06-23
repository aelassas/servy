# ==============================================================================
# Shared Utility Script - Generate Solution SBOM
# ------------------------------------------------------------------------------
# Purpose:
#   Maps solution assembly structures, runs CycloneDX dependency tracking,
#   and combines outputs into a finalized, unified CycloneDX SBOM XML file.
# ==============================================================================

param (
    [Parameter(Mandatory=$true)]
    [string]$BaseVersion,
    
    [Parameter(Mandatory=$true)]
    [string]$OutputFile
)

# Use subexpression operator to safely append the .0 suffix required for CycloneDX schemas
$FullSbomVersion = "$($BaseVersion).0"

$projects = @(
    @{ Path = 'src\Servy\Servy.csproj';                      File = 'sbom-Servy.xml' }
    @{ Path = 'src\Servy.CLI\Servy.CLI.csproj';              File = 'sbom-Servy.CLI.xml' }
    @{ Path = 'src\Servy.Manager\Servy.Manager.csproj';      File = 'sbom-Servy.Manager.xml' }
    @{ Path = 'src\Servy.Restarter\Servy.Restarter.csproj';  File = 'sbom-Servy.Restarter.xml' }
    @{ Path = 'src\Servy.Service\Servy.Service.csproj';      File = 'sbom-Servy.Service.xml' }
)

# Explicitly check for native command failures to prevent partial SBOMs
foreach ($p in $projects) {
    dotnet-CycloneDX $p.Path --recursive --set-version "$FullSbomVersion" --output . --filename $p.File
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet-CycloneDX failed for $($p.Path) (exit $LASTEXITCODE)"
    }
}

# Merge all component project files into the single specified target output file
$inputFiles = $projects | ForEach-Object { $_.File }
cyclonedx merge --input-files $inputFiles --output-file "$OutputFile"
if ($LASTEXITCODE -ne 0) {
    throw "cyclonedx merge failed (exit $LASTEXITCODE)"
}

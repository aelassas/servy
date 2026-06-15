
#Requires -Version 5.1
<#
.SYNOPSIS
    Runs unit tests and integration tests, and generates code coverage reports.

.DESCRIPTION
    This script performs the following tasks:
    1. Cleans up previous test results and coverage reports.
    2. Runs unit tests for dynamically discovered test projects using 'dotnet test'.
    3. Collects code coverage in Cobertura format.
    4. Generates an aggregated HTML coverage report using ReportGenerator.

.EXAMPLE
    ./test.ps1
    Runs all unit tests and generates the coverage report.

.NOTES
    Author: Akram El Assas

    Requirements:
        - .NET SDK installed and accessible in PATH.
        - ReportGenerator tool installed and available in PATH.
#>

$ErrorActionPreference = "Stop"

# Directories
$ScriptDir = $PSScriptRoot
$TestResultsDir = Join-Path -Path $ScriptDir -ChildPath "TestResults"
$CoverageReportDir = Join-Path -Path $ScriptDir -ChildPath "coveragereport"

# Cleanup previous results
if (Test-Path $TestResultsDir) {
    Write-Host "Cleaning up previous test results..."
    Remove-Item -Path $TestResultsDir -Recurse -Force
}

if (Test-Path $CoverageReportDir) {
    Write-Host "Cleaning up previous coverage report..."
    Remove-Item -Path $CoverageReportDir -Recurse -Force
}

# The native filesystem globbing filter (*Tests.csproj) already naturally excludes 'Servy.Testing.csproj'.
$RawTestProjects = Get-ChildItem -Path $ScriptDir -Recurse -Filter '*Tests.csproj'

# Run tests and collect coverage for each project
foreach ($ProjFile in $RawTestProjects) {
    $Proj = $ProjFile.FullName
    $ProjName = $ProjFile.BaseName

    Write-Host "Running tests for $($Proj)..." -ForegroundColor Cyan
    
    # Build the 'dotnet test' arguments for this project
    $dotnetArgs = @(
        'test', $Proj,
        '--configuration', 'Debug',
        '--collect', 'XPlat Code Coverage',
        '--results-directory', (Join-Path $TestResultsDir $ProjName)
    )

    $dotnetArgs += @(
        '--',
        'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura',
        'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude=[*.UnitTests]*,[*.IntegrationTests]*,[Servy.Testing]*,**/*.xaml,**/*.xaml.cs,**/*.g.cs,**/*.Designer.cs,**/obj/**/*',
        'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.IncludeProperties="True"'
    )

    & dotnet @dotnetArgs
    if ($LASTEXITCODE -ne 0) { Write-Error "dotnet test failed for $Proj"; exit $LASTEXITCODE }
}

# Generate a global coverage report
$CoverageFiles = Join-Path -Path $TestResultsDir -ChildPath "**/*.cobertura.xml"
Write-Host "Generating global coverage report..."
reportgenerator `
    -reports:$CoverageFiles `
    -targetdir:$CoverageReportDir `
    -reporttypes:Html `
    -assemblyfilters:"-*.UnitTests;-*.IntegrationTests;-Servy.Testing" `
    -filefilters:"-**/*.xaml;-**/*.xaml.cs;-**/*.g.cs;-**/*.Designer.cs;-**/obj/**/*"
if ($LASTEXITCODE -ne 0) { Write-Error "reportgenerator failed"; exit $LASTEXITCODE }

Write-Host "Coverage report generated at $CoverageReportDir"
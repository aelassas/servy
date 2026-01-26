<#
.SYNOPSIS
    Runs unit tests for Servy projects and generates code coverage reports.

.DESCRIPTION
    This script performs the following tasks:
    1. Cleans up previous test results and coverage reports.
    2. Runs unit tests for specified test projects using 'dotnet test'.
    3. Collects code coverage in Cobertura format.
    4. Generates an aggregated HTML coverage report using ReportGenerator.

.PARAMETER None
    No parameters are required; the script is self-contained.

.REQUIREMENTS
    - .NET SDK installed and accessible in PATH.
    - ReportGenerator tool installed and available in PATH.

.EXAMPLE
    ./test.ps1
    Runs all unit tests and generates the coverage report.

.NOTES
    Author: Akram El Assas
#>

$ErrorActionPreference = "Stop"

# Directories
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
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

# Explicit test projects
$TestProjects = @(
    Join-Path $ScriptDir "Servy.Core.UnitTests\Servy.Core.UnitTests.csproj"
    Join-Path $ScriptDir "Servy.Infrastructure.UnitTests\Servy.Infrastructure.UnitTests.csproj"
)

# Run tests and collect coverage for each project
foreach ($Proj in $TestProjects) {
    if (-not (Test-Path $Proj)) {
        Write-Error "Test project not found: $Proj"
        exit 1
    }

    Write-Host "Running tests for $($Proj)..."
    dotnet test $Proj `
        --configuration Debug `
        --collect:"XPlat Code Coverage" `
        --results-directory $TestResultsDir `
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
}

# Generate a global coverage report
$CoverageFiles = Join-Path -Path $TestResultsDir -ChildPath "**/*.cobertura.xml"
Write-Host "Generating global coverage report..."
reportgenerator `
    -reports:$CoverageFiles `
    -targetdir:$CoverageReportDir `
    -reporttypes:Html

Write-Host "Coverage report generated at $CoverageReportDir"

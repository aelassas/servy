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
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$testResultsDir = Join-Path -Path $scriptDir -ChildPath "TestResults"
$coverageReportDir = Join-Path -Path $scriptDir -ChildPath "coveragereport"

# Cleanup previous results
if (Test-Path $testResultsDir) {
    Write-Host "Cleaning up previous test results..."
    Remove-Item -Path $testResultsDir -Recurse -Force
}

if (Test-Path $coverageReportDir) {
    Write-Host "Cleaning up previous coverage report..."
    Remove-Item -Path $coverageReportDir -Recurse -Force
}

# Explicit test projects
$testProjects = @(
    Join-Path $scriptDir "Servy.Core.UnitTests\Servy.Core.UnitTests.csproj"
    Join-Path $scriptDir "Servy.Infrastructure.UnitTests\Servy.Infrastructure.UnitTests.csproj"
)

# Run tests and collect coverage for each project
foreach ($proj in $testProjects) {
    if (-not (Test-Path $proj)) {
        Write-Error "Test project not found: $proj"
        exit 1
    }

    Write-Host "Running tests for $($proj)..."
    dotnet test $proj `
        --collect:"XPlat Code Coverage" `
        --results-directory $testResultsDir `
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
}

# Generate a global coverage report
$coverageFiles = Join-Path -Path $testResultsDir -ChildPath "**/*.cobertura.xml"
Write-Host "Generating global coverage report..."
reportgenerator `
    -reports:$coverageFiles `
    -targetdir:$coverageReportDir `
    -reporttypes:Html

Write-Host "Coverage report generated at $coverageReportDir"

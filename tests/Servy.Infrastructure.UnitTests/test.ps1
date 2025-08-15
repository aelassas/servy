# tests/test.ps1
$ErrorActionPreference = "Stop"

# Directories
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$testResultsDir = Join-Path -Path $scriptDir -ChildPath "TestResults"
$coverageReportDir = Join-Path -Path $scriptDir -ChildPath "CoverageReport"

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

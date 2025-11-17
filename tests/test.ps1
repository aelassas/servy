# tests/test.ps1
$ErrorActionPreference = "Stop"

# Path to MSBuild.exe for Visual Studio 2022 Community Edition
$msbuildPath = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"

# Directories
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$testResultsDir = Join-Path -Path $scriptDir -ChildPath "TestResults"
$coverageReportDir = Join-Path -Path $scriptDir -ChildPath "coveragereport"

# Cleanup previous results
if (Test-Path $testResultsDir) {
    Write-Host "Cleaning up previous test results..."
    Remove-Item -Path $testResultsDir -Recurse -Force
}
New-Item -ItemType Directory -Path $testResultsDir | Out-Null

if (Test-Path $coverageReportDir) {
    Write-Host "Cleaning up previous coverage report..."
    Remove-Item -Path $coverageReportDir -Recurse -Force
}
New-Item -ItemType Directory -Path $coverageReportDir | Out-Null

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

    # Build the test project in Debug mode
    Write-Host "Building $($proj)..."
    & $msbuildPath $proj /p:Configuration=Debug /verbosity:minimal

    # Get project name without extension
    $projName = [System.IO.Path]::GetFileNameWithoutExtension($proj)
    $projDir = Split-Path $proj -Parent
    $dllPath = Join-Path $projDir "bin\Debug\$projName.dll"

    if (-not (Test-Path $dllPath)) {
        Write-Error "Could not find built DLL: $dllPath"
        exit 1
    }

    Write-Host "Running tests for $($projName)..."

    coverlet "$dllPath" `
      --target "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\Extensions\TestPlatform\vstest.console.exe" `
      --targetargs "$dllPath --ResultsDirectory:$testResultsDir" `
      --output (Join-Path $testResultsDir "$projName.coverage.xml") `
      --format "cobertura"
}

# Generate a global coverage report
$coverageFiles = Join-Path $testResultsDir "*.coverage.xml"
Write-Host "Generating global coverage report..."
reportgenerator `
    -reports:$coverageFiles `
    -targetdir:$coverageReportDir `
    -reporttypes:Html

Write-Host "Coverage report generated at $coverageReportDir"

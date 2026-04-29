<#
.SYNOPSIS
Runs Servy.Core and Servy.Infrastructure unit tests, collects coverage with Coverlet, and generates an HTML coverage report.

.DESCRIPTION
This script:
1. Cleans previous test and coverage output.
2. Builds the Servy.Core.UnitTests and Servy.Infrastructure.UnitTests projects in Debug mode.
3. Executes their tests using vstest.console.exe through Coverlet.
4. Produces Cobertura-format coverage reports for each project.
5. Generates a combined HTML coverage report using ReportGenerator.

.PARAMETER MsbuildPath
The path to MSBuild.exe. Defaults to the Visual Studio 2022 Community Edition location.

.NOTES
- Requires Coverlet and ReportGenerator to be installed and available in PATH.
- Must be run in PowerShell (x64).
- Paths to MSBuild and vstest.console.exe may need to be adjusted for your environment.

.EXAMPLE
PS> .\test.ps1
Runs tests for Servy.Core and Servy.Infrastructure, collects coverage, and generates an HTML report.

#>

# tests/test.ps1
$ErrorActionPreference = "Stop"

# Path to MSBuild.exe for Visual Studio 2022 Community Edition
$MsbuildPath = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"

# Directories
$ScriptDir = $PSScriptRoot
$TestResultsDir = Join-Path -Path $ScriptDir -ChildPath "TestResults"
$CoverageReportDir = Join-Path -Path $ScriptDir -ChildPath "coveragereport"

# Cleanup previous results
if (Test-Path $TestResultsDir) {
    Write-Host "Cleaning up previous test results..."
    Remove-Item -Path $TestResultsDir -Recurse -Force
}
New-Item -ItemType Directory -Path $TestResultsDir | Out-Null

if (Test-Path $CoverageReportDir) {
    Write-Host "Cleaning up previous coverage report..."
    Remove-Item -Path $CoverageReportDir -Recurse -Force
}
New-Item -ItemType Directory -Path $CoverageReportDir | Out-Null

# Explicit test projects
$TestProjects = @(
    Join-Path $ScriptDir "Servy.Core.UnitTests\Servy.Core.UnitTests.csproj"
    Join-Path $ScriptDir "Servy.Core.IntegrationTests\Servy.Core.IntegrationTests.csproj"
    Join-Path $ScriptDir "Servy.Infrastructure.UnitTests\Servy.Infrastructure.UnitTests.csproj"
    Join-Path $ScriptDir "Servy.Restarter.UnitTests\Servy.Restarter.UnitTests.csproj"
    Join-Path $ScriptDir "Servy.Service.UnitTests\Servy.Service.UnitTests.csproj"
    Join-Path $ScriptDir "Servy.UI.UnitTests\Servy.UI.UnitTests.csproj"
    Join-Path $ScriptDir "Servy.UnitTests\Servy.UnitTests.csproj"
    Join-Path $ScriptDir "Servy.Manager.UnitTests\Servy.Manager.UnitTests.csproj"
    Join-Path $ScriptDir "Servy.CLI.UnitTests\Servy.CLI.UnitTests.csproj"
)

# Run tests and collect coverage for each project
foreach ($Proj in $TestProjects) {
    if (-not (Test-Path $Proj)) {
        Write-Error "Test project not found: $Proj"
        exit 1
    }

    # Build the test project in Debug mode
    Write-Host "Building $($Proj)..."
    $Platform = "x64"
    & $MsbuildPath $Proj /p:Configuration=Debug /p:Platform=$Platform /p:DebugType=portable /p:DebugSymbols=true /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { Write-Error "Test failed for $Proj"; exit $LASTEXITCODE }

    # Get project name without extension
    $ProjName = [System.IO.Path]::GetFileNameWithoutExtension($Proj)
    $ProjDir = Split-Path $Proj -Parent
    $DllPath = Join-Path $ProjDir "bin\$Platform\Debug\$ProjName.dll"

    if (-not (Test-Path $DllPath)) {
        Write-Error "Could not find built DLL: $DllPath"
        exit 1
    }

    Write-Host "Running tests for $($ProjName)..."

    if ($Proj -like "*Servy.Infrastructure*") {
        coverlet "$DllPath" `
            --target "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\Extensions\TestPlatform\vstest.console.exe" `
            --targetargs "`"$DllPath`" --ResultsDirectory:`"$TestResultsDir`"" `
            --output (Join-Path $TestResultsDir "$ProjName.coverage.xml") `
            --format "cobertura" `
            --include-directory "$ProjDir" `
            --exclude "[Servy.Core]*"
        if ($LASTEXITCODE -ne 0) { Write-Error "coverlet failed for $ProjName"; exit $LASTEXITCODE }
    } else {
        coverlet "$DllPath" `
            --target "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\Extensions\TestPlatform\vstest.console.exe" `
            --targetargs "`"$DllPath`" --ResultsDirectory:`"$TestResultsDir`"" `
            --output (Join-Path $TestResultsDir "$ProjName.coverage.xml") `
            --format "cobertura" `
            --include-directory "$ProjDir"
        if ($LASTEXITCODE -ne 0) { Write-Error "coverlet failed for $ProjName"; exit $LASTEXITCODE }
    }

}

# Generate a global coverage report
$coverageFiles = Join-Path $TestResultsDir "*.coverage.xml"
Write-Host "Generating global coverage report..."
reportgenerator `
    -reports:$coverageFiles `
    -targetdir:$CoverageReportDir `
    -reporttypes:Html
if ($LASTEXITCODE -ne 0) { Write-Error "reportgenerator failed"; exit $LASTEXITCODE }

Write-Host "Coverage report generated at $CoverageReportDir"

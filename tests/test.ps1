<#
.SYNOPSIS
    Runs unit tests and integration tests, collects coverage with Coverlet, and generates an HTML coverage report.

.DESCRIPTION
    This script:
    1. Cleans previous test and coverage output.
    2. Builds all dynamically discovered test projects in Debug mode via MSBuild.
    3. Executes their tests using vstest.console.exe through Coverlet.
    4. Produces Cobertura-format coverage reports for each project.
    5. Generates a combined HTML coverage report using ReportGenerator.

.NOTES
    - Requires Coverlet and ReportGenerator to be installed and available in PATH.
    - Must be run in PowerShell (x64).
    - Paths to MSBuild and vstest.console.exe may need to be adjusted for the environment.

.EXAMPLE
    PS> .\test.ps1
    Runs tests for discovered suites, collects coverage, and generates an HTML report.
#>

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

# Leverage the dynamic pipeline scanner to discover test configurations natively.
# The native filesystem globbing filter (*Tests.csproj) already naturally excludes 'Servy.Testing.csproj'.
$RawTestProjects = Get-ChildItem -Path $ScriptDir -Recurse -Filter '*Tests.csproj'

# Run tests and collect coverage for each project
foreach ($ProjFile in $RawTestProjects) {
    $Proj = $ProjFile.FullName
    $ProjName = $ProjFile.BaseName
    $ProjDir = $ProjFile.DirectoryName

    # Build the test project in Debug mode
    Write-Host "Building $($Proj)..." -ForegroundColor Cyan
    $Platform = "x64"
    & $MsbuildPath $Proj /p:Configuration=Debug /p:Platform=$Platform /p:DebugType=portable /p:DebugSymbols=true /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { Write-Host "Build failed for $Proj"; exit $LASTEXITCODE }

    $DllPath = Join-Path $ProjDir "bin\${Platform}\Debug\${ProjName}.dll"
    if (-not (Test-Path $DllPath)) {
        Write-Host "Could not find built DLL: $DllPath"
        exit 1
    }

    Write-Host "Running tests for ${ProjName}..." -ForegroundColor Green

    # Define distinct filter categories based on Coverlet's native parameter targets
    $AssemblyExclusions = @("*.UnitTests", "*.IntegrationTests", "Servy.Testing")
    $FileExclusions     = @("**/*.xaml", "**/*.xaml.cs", "**/*.g.cs",  "**/*.Designer.cs", "**/obj/**/*")

    # Dynamically evaluate cross-project dependency assembly exclusions to eliminate hardcoded forks
    if ($ProjName -like "*Infrastructure*") {
        $AssemblyExclusions += "Servy.Core"
    }
    elseif ($ProjName -like "*Core*") {
        $AssemblyExclusions += "Servy.Infrastructure"
    }

    # Join array strings with a comma wrapper for Coverlet's expected input parser
    $excludeAssemblies = ($AssemblyExclusions | ForEach-Object { "[$_]*" }) -join ","
    $excludeFiles      = $FileExclusions -join ","

    # Build dynamic execution parameters
    $excludeArgs = "--exclude `"${excludeAssemblies}`" --exclude-by-file `"${excludeFiles}`""

    # Execute test run session through Coverlet coverage runner natively
    coverlet "$DllPath" `
        --target "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\Extensions\TestPlatform\vstest.console.exe" `
        --targetargs "`"${DllPath}`" --ResultsDirectory:`"${TestResultsDir}`"" `
        --output (Join-Path $TestResultsDir "${ProjName}.coverage.xml") `
        --format "cobertura" `
        --include-directory "$ProjDir" `
        $excludeArgs

    if ($LASTEXITCODE -ne 0) { Write-Host "coverlet failed for $ProjName"; exit $LASTEXITCODE }
}

# Generate a global coverage report
$coverageFiles = Join-Path $TestResultsDir "*.coverage.xml"
Write-Host "Generating global coverage report..."
reportgenerator `
    -reports:$coverageFiles `
    -targetdir:$CoverageReportDir `
    -reporttypes:Html `
    -assemblyfilters:"-*.UnitTests;-*.IntegrationTests;-Servy.Testing;-Servy.Restarter.Net48" `
    -filefilters:"-**/*.xaml;-**/*.xaml.cs;-**/*.g.cs;-**/*.Designer.cs;-**/obj/**/*"

if ($LASTEXITCODE -ne 0) { Write-Host "reportgenerator failed"; exit $LASTEXITCODE }

Write-Host "Coverage report generated at $CoverageReportDir"
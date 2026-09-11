#Requires -Version 5.1

<#
.SYNOPSIS
    Runs unit tests and integration tests on .NET Framework 4.8, collects code coverage via dotnet-coverage, and generates an HTML coverage report.

.DESCRIPTION
    This script:
    1. Cleans previous test and coverage output.
    2. Builds all dynamically discovered test projects targeting net48 in Debug mode via MSBuild with auto binding redirects.
    3. Executes tests using dotnet-coverage wrapping vstest.console.exe via discrete argument splatting.
    4. Generates a combined HTML coverage report using ReportGenerator.

.PARAMETER IncludeStress
    When specified, includes tests tagged with Category=Stress. Defaults to excluding them.

.EXAMPLE
    PS> .\test.ps1
    Runs tests for discovered suites (excluding Stress tests) and generates an HTML coverage report.

.EXAMPLE
    PS> .\test.ps1 -IncludeStress
    Runs tests for discovered suites (including Stress tests) and generates an HTML coverage report.
#>
param(
    [switch]$IncludeStress
)

$ErrorActionPreference = "Stop"

# Verify dotnet-coverage global tool is installed
if (-not (Get-Command "dotnet-coverage" -ErrorAction SilentlyContinue)) {
    Write-Host "Installing dotnet-coverage global tool..." -ForegroundColor Cyan
    dotnet tool install -g dotnet-coverage
}

# Dynamically locate MSBuild via vswhere or fallback
$vsInstallPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath
if ($vsInstallPath) {
    $MsbuildPath = Join-Path $vsInstallPath "MSBuild\Current\Bin\MSBuild.exe"
} else {
    $MsbuildPath = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
}

if (-not (Test-Path $MsbuildPath)) {
    Write-Error "MSBuild.exe could not be found at: $MsbuildPath"
    exit 1
}

# Dynamically locate vstest.console.exe
$vsTest = Join-Path $vsInstallPath "Common7\IDE\Extensions\TestPlatform\vstest.console.exe"
if (-not (Test-Path $vsTest)) {
    $vsTest = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\Extensions\TestPlatform\vstest.console.exe"
}

if (-not (Test-Path $vsTest)) {
    Write-Error "vstest.console.exe could not be found at: $vsTest"
    exit 1
}

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

# Discover test project configurations
$RawTestProjects = Get-ChildItem -Path $ScriptDir -Recurse -Filter '*Tests.csproj'

if (-not $RawTestProjects) {
    Write-Host "No '*Tests.csproj' projects found under $ScriptDir - nothing to test." -ForegroundColor Red
    exit 1
}

Write-Host "Discovered $($RawTestProjects.Count) test project(s)." -ForegroundColor Cyan

# Run tests and collect coverage for each project via dotnet-coverage + vstest.console.exe
foreach ($ProjFile in $RawTestProjects) {
    $Proj = $ProjFile.FullName
    $ProjName = $ProjFile.BaseName
    $ProjDir = $ProjFile.DirectoryName

    # Build the test project targeting net48 with automatic binding redirects
    Write-Host "Building $($Proj)..." -ForegroundColor Cyan
    $Platform = "x64"
    & $MsbuildPath $Proj `
        /p:Configuration=Debug `
        /p:Platform=$Platform `
        /p:TargetFrameworkVersion=v4.8 `
        /p:AutoGenerateBindingRedirects=true `
        /p:GenerateBindingRedirectsOutputType=true `
        /p:DebugType=full `
        /p:DebugSymbols=true `
        /verbosity:minimal

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed for $Proj" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    $DllPath = Join-Path $ProjDir "bin\${Platform}\Debug\${ProjName}.dll"
    if (-not (Test-Path $DllPath)) {
        $DllPath = Join-Path $ProjDir "bin\Debug\${ProjName}.dll"
    }

    if (-not (Test-Path $DllPath)) {
        Write-Host "Could not find built DLL: $DllPath" -ForegroundColor Red
        exit 1
    }

    Write-Host "Running tests for ${ProjName} via dotnet-coverage and vstest.console.exe..." -ForegroundColor Green

    $coverageOutputFile = Join-Path $TestResultsDir "${ProjName}.cobertura.xml"

    # Construct discrete dotnet-coverage collect CLI argument list
    $coverageCollectArgs = @(
        "collect",
        "--output", $coverageOutputFile,
        "--output-format", "cobertura",
        "--",
        $vsTest,
        $DllPath,
        "/Framework:.NETFramework,Version=v4.8",
        "/ResultsDirectory:$TestResultsDir",
        "/Logger:trx",
        "/Blame"
    )

    if (-not $IncludeStress) {
        $coverageCollectArgs += "/TestCaseFilter:Category!=Stress"
    }

    # Execute dotnet-coverage cleanly using array splatting
    & dotnet-coverage @coverageCollectArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "dotnet-coverage/vstest failed for $ProjName" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# Consolidate coverage XML files for ReportGenerator
$xmlCoverageFiles = Get-ChildItem -Path $TestResultsDir -Filter "*.cobertura.xml" -ErrorAction SilentlyContinue

if ($xmlCoverageFiles) {
    Write-Host "Generating global HTML coverage report..."
    
    $joinedReports = ($xmlCoverageFiles.FullName) -join ";"

    reportgenerator `
        "-reports:$joinedReports" `
        "-targetdir:$CoverageReportDir" `
        "-reporttypes:Html" `
        "-assemblyfilters:-*.UnitTests;-*.IntegrationTests;-Servy.Testing;-Servy.Restarter.Net48;-Dapper;-Moq;-xunit*;-Castle*;-CommandLine;-System*;-Microsoft*" `
        "-filefilters:-**/*.xaml;-**/*.xaml.cs;-**/*.g.cs;-**/*.Designer.cs;-**/obj/**/*"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "reportgenerator failed" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "Coverage report successfully generated at $CoverageReportDir"
} else {
    Write-Host "Test run completed successfully, but no coverage XML files were produced." -ForegroundColor Yellow
}

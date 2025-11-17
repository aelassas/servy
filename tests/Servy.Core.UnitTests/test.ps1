# Ensure you installed coverlet.console globally:
# dotnet tool install --global coverlet.console
# Also install ReportGenerator:
# dotnet tool install --global dotnet-reportgenerator-globaltool

# Path to MSBuild.exe for Visual Studio 2026 Community Edition
$msbuildPath = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"

# Build the Servy.Core.UnitTests.csproj in Debug mode using MSBuild
& $msbuildPath "tests\Servy.Core.UnitTests\Servy.Core.UnitTests.csproj" /p:Configuration=Debug /verbosity:minimal

# Cleanup previous coverage files if they exist
if (Test-Path cobertura.xml) {
    Remove-Item cobertura.xml -Force
}
if (Test-Path coverage.xml) {
    Remove-Item coverage.xml -Force
}
if (Test-Path coveragereport) {
    Remove-Item coveragereport -Recurse -Force
}

# Run tests with coverage collection using coverlet
coverlet.exe "bin\Debug\Servy.Core.UnitTests.dll" `
  --target "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\Extensions\TestPlatform\vstest.console.exe" `
  --targetargs "bin\Debug\Servy.Core.UnitTests.dll" `
  --output "coverage.xml" `
  --format "cobertura"

# Generate coverage report (HTML and others)
reportgenerator `
  -reports:"coverage.xml" `
  -targetdir:"coveragereport" `
  -reporttypes:Html

Write-Host "Coverage report generated in 'coveragereport' folder."

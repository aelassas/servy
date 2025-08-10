
# Cleanup
Remove-Item -Recurse -Force ./TestResults

# Run tests and collect coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

# Build coverage reprt
reportgenerator -reports:"TestResults/**/*.cobertura.xml" -targetdir:coveragereport
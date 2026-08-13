using Servy.Core.Config;
using Servy.Testing;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Servy.Core.IntegrationTests.Config
{
    /// <summary>
    /// Tests to ensure that magic strings and configurations are synchronized
    /// between the C# codebase and the external PowerShell CLI modules.
    /// </summary>
    public class PowerShellConfigSyncIntegrationTests
    {
        // Statically tracks the compiled mappings to cross-examine against dynamic file parses and reflection scans
        private static readonly Dictionary<string, string> ExpectedMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ServyPasswordEnvVar", AppConfig.PasswordEnvVarName },
            { "ServyProcessParametersEnvVar", AppConfig.ProcessParametersEnvVarName },
            { "ServyEnvironmentVariablesEnvVar", AppConfig.EnvironmentVariablesEnvVarName },
            { "ServyFailureProgramParametersEnvVar", AppConfig.FailureProgramParametersEnvVarName },
            { "ServyPreLaunchParametersEnvVar", AppConfig.PreLaunchParametersEnvVarName },
            { "ServyPreLaunchEnvironmentVariablesEnvVar", AppConfig.PreLaunchEnvironmentVariablesEnvVarName },
            { "ServyPostLaunchParametersEnvVar", AppConfig.PostLaunchParametersEnvVarName },
            { "ServyPreStopParametersEnvVar", AppConfig.PreStopParametersEnvVarName },
            { "ServyPostStopParametersEnvVar", AppConfig.PostStopParametersEnvVarName }
        };

        public static TheoryData<string, string> EnvVarMappings()
        {
            var data = new TheoryData<string, string>();
            foreach (var kvp in ExpectedMappings)
            {
                data.Add(kvp.Key, kvp.Value);
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(EnvVarMappings))]
        public void ServyPsm1_SensitiveEnvVars_MatchAppConfigConstants(string scriptVarName, string expectedEnvVarName)
        {
            // Arrange
            var psm1Path = Helper.GetServyPsm1Path();
            Assert.True(File.Exists(psm1Path), $"Servy.psm1 file not found at path: {psm1Path}");

            // Act
            string scriptContent = File.ReadAllText(psm1Path);

            // Regex to locate the specific script-scoped variable mapping assignment dynamically
            // e.g., $script:ServyPasswordEnvVar = 'SERVY_PASSWORD'
            string pattern = @"\$script:" + Regex.Escape(scriptVarName) + @"\s*=\s*['""]([^'""]+)['""]";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            var match = regex.Match(scriptContent);

            // Assert
            Assert.True(match.Success, $"Could not find the assignment for '$script:{scriptVarName}' in Servy.psm1. Has the script variable structure deviated?");

            string actualEnvVarName = match.Groups[1].Value;

            Assert.Equal(expectedEnvVarName, actualEnvVarName);
        }

        [Fact]
        public void ServyPsm1_VerifyAllScriptEnvVarsAreGuarded()
        {
            // Arrange
            var psm1Path = Helper.GetServyPsm1Path();
            Assert.True(File.Exists(psm1Path), $"Servy.psm1 file not found at path: {psm1Path}");
            string scriptContent = File.ReadAllText(psm1Path);

            // Parse out EVERY string assignment matching the script environment variable nomenclature pattern
            var strictRegex = new Regex(@"\$script:(Servy\w*EnvVar)\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
            var strictMatches = strictRegex.Matches(scriptContent);

            Assert.NotEmpty(strictMatches);

            // Looser check to detect any environment variable assignment that does not use string literals (e.g. dynamic/computed expressions)
            var looseRegex = new Regex(@"\$script:(Servy\w*EnvVar)\s*=", RegexOptions.IgnoreCase);
            var looseMatches = looseRegex.Matches(scriptContent);

            Assert.Equal(strictMatches.Count, looseMatches.Count);

            // Act & Assert
            foreach (Match match in strictMatches)
            {
                string scriptVarName = match.Groups[1].Value;
                string literalValue = match.Groups[2].Value;

                // Guard 1: Verify the variable isn't an unmapped rogue entry drifting from the sync list
                Assert.True(ExpectedMappings.ContainsKey(scriptVarName),
                    $"PowerShell script variable '$script:{scriptVarName}' was added to Servy.psm1 but is missing from the verification guard mappings array.");

                // Guard 2: Verify the literal values track identically across files
                Assert.Equal(ExpectedMappings[scriptVarName], literalValue);
            }
        }

        [Fact]
        public void AppConfig_VerifyAllEnvVarConstantsAreGuarded()
        {
            // Arrange
            // Reflectively crawl AppConfig to discover all target public constants ending in 'EnvVarName'
            var targetFields = typeof(AppConfig)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => (f.IsLiteral || (f.IsStatic && f.IsInitOnly)) && f.Name.EndsWith("EnvVarName", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.NotEmpty(targetFields);

            // Act & Assert
            foreach (var field in targetFields)
            {
                string? constantValue = field.GetValue(null) as string;
                Assert.NotNull(constantValue);

                // Guard: Verify that the reflected C# environment variable string literal value is covered by our sync guard map
                Assert.True(ExpectedMappings.ContainsValue(constantValue),
                    $"AppConfig constant field '{field.Name}' ('{constantValue}') is missing from the validation guard mappings list. Ensure it is fully synchronized.");
            }
        }
    }
}

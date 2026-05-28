using Servy.Core.Config;
using System.Text.RegularExpressions;

namespace Servy.Core.UnitTests.Config
{
    /// <summary>
    /// Tests to ensure that magic strings and configurations are synchronized 
    /// between the C# codebase and the external PowerShell CLI modules.
    /// </summary>
    public class PowerShellConfigSyncTests
    {
        [Theory]
        [InlineData("ServyPasswordEnvVar", AppConfig.PasswordEnvVarName)]
        [InlineData("ServyProcessParametersEnvVar", AppConfig.ProcessParametersEnvVarName)]
        [InlineData("ServyEnvironmentVariablesEnvVar", AppConfig.EnvironmentVariablesEnvVarName)]
        [InlineData("ServyFailureProgramParametersEnvVar", AppConfig.FailureProgramParametersEnvVarName)]
        [InlineData("ServyPreLaunchParametersEnvVar", AppConfig.PreLaunchParametersEnvVarName)]
        [InlineData("ServyPreLaunchEnvironmentVariablesEnvVar", AppConfig.PreLaunchEnvironmentVariablesEnvVarName)]
        [InlineData("ServyPostLaunchParametersEnvVar", AppConfig.PostLaunchParametersEnvVarName)]
        [InlineData("ServyPreStopParametersEnvVar", AppConfig.PreStopParametersEnvVarName)]
        [InlineData("ServyPostStopParametersEnvVar", AppConfig.PostStopParametersEnvVarName)]
        public void ServyPsm1_SensitiveEnvVars_MatchAppConfigConstants(string scriptVarName, string expectedEnvVarName)
        {
            // Arrange
            string startDir = AppDomain.CurrentDomain.BaseDirectory;
            string repoRoot = AppConfig.FindRepoRoot(startDir);

            Assert.False(string.IsNullOrEmpty(repoRoot), "Could not find repository root.");

            // Construct the path to the PowerShell module
            string psm1Path = Path.Combine(repoRoot, "src", "Servy.CLI", "Servy.psm1");
            Assert.True(File.Exists(psm1Path), $"PowerShell module not found at expected path: {psm1Path}");

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
    }
}
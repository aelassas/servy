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
        [Fact]
        public void ServyPsm1_PasswordEnvVar_MatchesAppConfigConstant()
        {
            // Arrange
            string startDir = AppDomain.CurrentDomain.BaseDirectory;
            string repoRoot = AppConfig.FindRepoRoot(startDir);

            Assert.False(string.IsNullOrEmpty(repoRoot), "Could not find repository root.");

            // Construct the path to the PowerShell module
            string psm1Path = Path.Combine(repoRoot, "src", "Servy.CLI", "Servy.psm1");
            Assert.True(File.Exists(psm1Path), $"PowerShell module not found at expected path: {psm1Path}");

            string expectedEnvVarName = AppConfig.PasswordEnvVarName;

            // Act
            string scriptContent = File.ReadAllText(psm1Path);

            // Regex to find: $script:ServyPasswordEnvVar = 'SERVY_PASSWORD' (handles single/double quotes and whitespace)
            var regex = new Regex(@"\$script:ServyPasswordEnvVar\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
            var match = regex.Match(scriptContent);

            // Assert
            Assert.True(match.Success, "Could not find the assignment for '$script:ServyPasswordEnvVar' in Servy.psm1. Did the variable name change?");

            string actualEnvVarName = match.Groups[1].Value;

            Assert.Equal(expectedEnvVarName, actualEnvVarName);
        }
    }
}
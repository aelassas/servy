using Servy.Core.Config;
using Servy.Core.Logging;
using System.Text.RegularExpressions;

namespace Servy.Core.UnitTests.Logging
{
    public class PowerShellEventIdsTests
    {
        private readonly string _repoRoot;
        private const string TaskSchdPath = "setup/taskschd";

        public PowerShellEventIdsTests()
        {
            // Use the utility to locate the repository root from the current execution directory
            _repoRoot = AppConfig.FindRepoRoot(AppDomain.CurrentDomain.BaseDirectory);
        }

        [Fact]
        public void ServyWatermark_ErrorId_Matches_EventIds_Constant()
        {
            // Arrange
            string filePath = Path.Combine(_repoRoot, TaskSchdPath, "Servy-Watermark.psm1");
            int expectedId = EventIds.ScheduledTaskScriptError;

            // Act
            int actualId = ExtractEventIdFromPowerShell(filePath, "$EVENT_ID_ERROR");

            // Assert
            Assert.Equal(expectedId, actualId);
        }

        [Fact]
        public void ServyFailureNotification_DependencyErrorId_Matches_EventIds_Constant()
        {
            // Arrange
            string filePath = Path.Combine(_repoRoot, TaskSchdPath, "ServyFailureNotification.ps1");
            int expectedId = EventIds.ScheduledTaskScriptDependencyError;

            // Act
            int actualId = ExtractEventIdFromPowerShell(filePath, "$EVENT_ID_DEPENDENCY_ERROR");

            // Assert
            Assert.Equal(expectedId, actualId);
        }

        [Fact]
        public void ServyFailureEmail_DependencyErrorId_Matches_EventIds_Constant()
        {
            // Arrange
            string filePath = Path.Combine(_repoRoot, TaskSchdPath, "ServyFailureEmail.ps1");
            int expectedId = EventIds.ScheduledTaskScriptDependencyError;

            // Act
            int actualId = ExtractEventIdFromPowerShell(filePath, "$EVENT_ID_DEPENDENCY_ERROR");

            // Assert
            Assert.Equal(expectedId, actualId);
        }

        /// <summary>
        /// Parses a PowerShell file to find a variable assignment and extract its integer value.
        /// </summary>
        private static int ExtractEventIdFromPowerShell(string filePath, string variableName)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"PowerShell script not found at expected path: {filePath}");
            }

            string content = File.ReadAllText(filePath);

            // Regex handles optional spaces around the equals sign and extracts the digit
            // Escaping the variable name because it contains a '$'
            string pattern = $@"{Regex.Escape(variableName)}\s*=\s*(\d+)";
            var match = Regex.Match(content, pattern);

            if (!match.Success)
            {
                throw new InvalidOperationException($"Could not find variable {variableName} in {filePath}");
            }

            return int.Parse(match.Groups[1].Value);
        }
    }
}
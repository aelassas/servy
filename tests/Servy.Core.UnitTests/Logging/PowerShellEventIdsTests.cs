using Servy.Core.Config;
using Servy.Core.Logging;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

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

        /// <summary>
        /// Verifies that the event ID variables defined inside the automated setup PowerShell scripts
        /// strictly match the compile-time constants managed by the core logging infrastructure.
        /// </summary>
        [Theory]
        [InlineData("Servy-Watermark.psm1", "$EVENT_ID_ERROR", EventIds.ScheduledTaskScriptError)]
        [InlineData("ServyFailureNotification.ps1", "$EVENT_ID_DEPENDENCY_ERROR", EventIds.ScheduledTaskScriptDependencyError)]
        [InlineData("ServyFailureEmail.ps1", "$EVENT_ID_DEPENDENCY_ERROR", EventIds.ScheduledTaskScriptDependencyError)]
        public void PowerShellScript_EventId_Matches_EventIds_Constant(string scriptName, string variableName, int expectedId)
        {
            // Arrange
            string filePath = Path.Combine(_repoRoot, TaskSchdPath, scriptName);

            // Act
            int actualId = ExtractEventIdFromPowerShell(filePath, variableName);

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

            // Multiline anchoring regex rules (`^\s*`) to explicitly defend against 
            // parsing commented-out descriptors or secondary block strings higher up in the script layout.
            string pattern = $@"^\s*{Regex.Escape(variableName)}\s*=\s*(\d+)";
            var match = Regex.Match(content, pattern, RegexOptions.Multiline);

            if (!match.Success)
            {
                throw new InvalidOperationException($"Could not find variable {variableName} in {filePath}");
            }

            return int.Parse(match.Groups[1].Value);
        }
    }
}
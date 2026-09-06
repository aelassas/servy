using Servy.Core.Config;
using Servy.Core.Logging;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Servy.Core.IntegrationTests.Logging
{
    public class PowerShellEventIdsIntegrationTests
    {
        private readonly string _repoRoot;
        private const string TaskSchdPath = "setup/taskschd";

        // The script-error range documented in Get-ServyLastErrors.ps1's taxonomy comment and in EventIds.cs.
        private const int ScriptErrorIdRangeStart = 3100;
        private const int ScriptErrorIdRangeEnd = 3199;

        /// <summary>
        /// Matches a PowerShell assignment of an integer literal at the start of a line, allowing the
        /// type or attribute prefixes a parameter declaration carries (<c>[int]$EventLogErrorId = 3103</c>).
        /// Group 1 is the variable, group 2 the value. Only horizontal whitespace is allowed before the
        /// variable, so a commented-out or wrapped descriptor higher up cannot be picked up instead.
        /// </summary>
        private const string AssignmentPatternFormat = @"^[ \t]*(?:\[[^\]]+\][ \t]*)*({0})[ \t]*=[ \t]*(\d+)";

        /// <summary>
        /// Every event ID mirrored by hand from <see cref="EventIds"/> into a taskschd script. Both the
        /// parity theory and the completeness test read this list, so a new mirror is declared once.
        /// </summary>
        private static readonly (string ScriptName, string VariableName, int ExpectedId)[] MirroredEventIds =
        {
            ("Servy-Watermark.psm1", "$EVENT_ID_ERROR", EventIds.ScheduledTaskScriptError),
            ("ServyFailureNotification.ps1", "$EVENT_ID_DEPENDENCY_ERROR", EventIds.ScheduledTaskScriptDependencyError),
            ("ServyFailureEmail.ps1", "$EVENT_ID_DEPENDENCY_ERROR", EventIds.ScheduledTaskScriptDependencyError),
            ("Get-ServyLastErrors.ps1", "$EventLogErrorId", EventIds.ScheduledTaskScriptError),
        };

        public static IEnumerable<object[]> MirroredEventIdCases() =>
            MirroredEventIds.Select(m => new object[] { m.ScriptName, m.VariableName, m.ExpectedId });

        public PowerShellEventIdsIntegrationTests()
        {
            // Use the utility to locate the repository root from the current execution directory
            _repoRoot = AppConfig.FindRepoRoot(AppDomain.CurrentDomain.BaseDirectory);
        }

        /// <summary>
        /// Verifies that the event ID variables defined inside the automated setup PowerShell scripts
        /// strictly match the compile-time constants managed by the core logging infrastructure.
        /// </summary>
        [Theory]
        [MemberData(nameof(MirroredEventIdCases))]
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
        /// Fails when a taskschd script declares a script-error event ID the parity theory does not cover.
        /// The theory can only check names someone remembered to list; this closes that gap.
        /// </summary>
        [Fact]
        public void EveryScriptErrorEventIdInTaskSchdScripts_IsCoveredByTheParityTheory()
        {
            // Arrange
            string taskSchdDirectory = Path.Combine(_repoRoot, TaskSchdPath);
            string[] scripts = Directory.GetFiles(taskSchdDirectory, "*.ps1")
                .Concat(Directory.GetFiles(taskSchdDirectory, "*.psm1"))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            // A scan that quietly reads nothing would pass forever, so pin that it read something.
            Assert.NotEmpty(scripts);

            string pattern = string.Format(CultureInfo.InvariantCulture, AssignmentPatternFormat, @"\$\w+");

            // Act
            var declared = new List<string>();
            foreach (string script in scripts)
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(script), pattern, RegexOptions.Multiline))
                {
                    int value = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                    if (value < ScriptErrorIdRangeStart || value > ScriptErrorIdRangeEnd)
                    {
                        continue;
                    }

                    declared.Add($"{Path.GetFileName(script)} {match.Groups[1].Value}");
                }
            }

            // Assert
            // Same reason as above: an empty result here means the pattern stopped matching, not that
            // the scripts stopped mirroring event IDs.
            Assert.NotEmpty(declared);

            var covered = new HashSet<string>(
                MirroredEventIds.Select(m => $"{m.ScriptName} {m.VariableName}"),
                StringComparer.Ordinal);

            string[] uncovered = declared.Where(d => !covered.Contains(d)).Distinct(StringComparer.Ordinal).ToArray();

            Assert.True(
                uncovered.Length == 0,
                $"Event IDs in the {ScriptErrorIdRangeStart}-{ScriptErrorIdRangeEnd} range are mirrored in taskschd " +
                $"scripts without a row in {nameof(MirroredEventIds)}: {string.Join(", ", uncovered)}");
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

            // Multiline anchoring to explicitly defend against parsing commented-out descriptors or
            // secondary block strings higher up in the script layout.
            string pattern = string.Format(CultureInfo.InvariantCulture, AssignmentPatternFormat, Regex.Escape(variableName));
            var match = Regex.Match(content, pattern, RegexOptions.Multiline);

            if (!match.Success)
            {
                throw new InvalidOperationException($"Could not find variable {variableName} in {filePath}");
            }

            return int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        }
    }
}

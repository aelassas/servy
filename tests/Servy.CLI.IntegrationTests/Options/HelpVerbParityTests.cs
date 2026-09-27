using CommandLine;
using Servy.Testing;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Servy.CLI.IntegrationTests.Options
{
    /// <summary>
    /// Guards the parity between the <c>-Command</c> <c>[ValidateSet(...)]</c> of <c>Get-ServyHelp</c>
    /// in <c>Servy.psm1</c> and the verbs the CLI declares, so a verb added to or removed from
    /// <c>Servy.CLI</c> cannot leave the module rejecting it at parameter binding.
    /// </summary>
    public class HelpVerbParityTests
    {
        /// <summary>
        /// Asserts that the command names <c>Get-ServyHelp</c> accepts are exactly the verbs declared by
        /// the CLI option types, excluding <c>help</c>, which <c>Get-ServyHelp</c> already invokes when
        /// no <c>-Command</c> is given.
        /// </summary>
        [Fact]
        public void GetServyHelp_CommandValidateSet_MatchesEveryCliVerb()
        {
            // Arrange
            var psm1Path = Helper.GetServyPsm1Path();
            Assert.True(File.Exists(psm1Path), $"Servy.psm1 file not found at path: {psm1Path}");
            var psm1Content = File.ReadAllText(psm1Path);

            // Extract the elements inside Get-ServyHelp's [ValidateSet(...)] using Regex. The lazy
            // [\s\S]*? keeps the match inside that function, so another function's ValidateSet cannot
            // be picked up instead.
            var validateSetRegex = new Regex(@"function\s+Get-ServyHelp\s*\{[\s\S]*?\[ValidateSet\(([^)]*)\)\]");

            // Act
            var match = validateSetRegex.Match(psm1Content);
            Assert.True(match.Success, "Could not locate Get-ServyHelp's -Command ValidateSet in Servy.psm1.");

            var listed = new HashSet<string>(
                Regex.Matches(match.Groups[1].Value, "[\"']([^\"']+)[\"']").Cast<Match>().Select(m => m.Groups[1].Value),
                StringComparer.Ordinal);

            // Reflection rather than a text grep of the options files: the status verb spells its
            // [Verb(...)] across several lines, and a new verb type must not be able to escape by
            // being formatted differently.
            var verbs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var type in CliOptionTypes.All)
            {
                var verbAttribute = type.GetCustomAttribute<VerbAttribute>();
                if (verbAttribute == null || string.IsNullOrEmpty(verbAttribute.Name) || verbAttribute.Name == "help")
                {
                    continue;
                }

                verbs.Add(verbAttribute.Name);
            }

            // Assert
            Assert.NotEmpty(verbs);
            Assert.True(listed.SetEquals(verbs),
                $"Get-ServyHelp -Command and the CLI verbs disagree. Only in Servy.psm1: {string.Join(", ", listed.Except(verbs))}. Only in C#: {string.Join(", ", verbs.Except(listed))}");
        }
    }
}

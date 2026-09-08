using Servy.Service.Helpers;
using System.Text.RegularExpressions;

namespace Servy.Service.UnitTests.Documentation
{
    /// <summary>
    /// Verifies that the set of protected environment variables in code aligns
    /// perfectly with the documented Wiki table.
    /// </summary>
    /// <remarks>
    /// Reads the wiki page from disk rather than fetching it over HTTP, so the result is
    /// reproducible for a given commit and does not depend on what the wiki currently says.
    /// Runs only when <c>SERVY_WIKI_PATH</c> is set, which the wiki workflow's
    /// <c>validate-protected-variables</c> job does after checking out the wiki repository
    /// alongside the code; a wiki edit is what invalidates this assertion, so that is the
    /// trigger that should run it, not an unrelated push to the code repository.
    /// </remarks>
    public class ProtectedVariablesDocumentationTests
    {
        private const string WikiPageFileName = "Environment-Variables.md";

        private static readonly string? WikiPath = Environment.GetEnvironmentVariable("SERVY_WIKI_PATH");

        [Fact]
        public void ProtectedVariables_MustMatchDocumentedWikiTable()
        {
            Assert.SkipUnless(
                !string.IsNullOrWhiteSpace(WikiPath),
                "SERVY_WIKI_PATH not set; this parity check runs from the wiki workflow's validate-protected-variables job.");

            string markdownContent = File.ReadAllText(Path.Combine(WikiPath!, WikiPageFileName));

            // 1. Strictly bound the scope to the 'Protected Variables' section up to the next heading
            var sectionMatch = Regex.Match(
                markdownContent,
                @"###\s+Protected Variables(?<section_content>.*?)(?=\n#{1,3}\s+|\Z)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            Assert.True(sectionMatch.Success, "Failed to locate the '### Protected Variables' section in the Wiki markdown.");

            string sectionArea = sectionMatch.Groups["section_content"].Value;

            // 2. Extract variable names strictly from the 2nd column of each markdown table row
            var documentedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var documentedList = new List<string>(); // every backticked name, duplicates included
            var backtickRegex = new Regex(@"`([^`]+)`", RegexOptions.Compiled);

            using (var reader = new StringReader(sectionArea))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    // Process markdown table rows (ignoring header dividers like '| --- |')
                    if (line.StartsWith("|") && !line.Contains("---"))
                    {
                        var columns = line.Split('|');

                        // The 2nd column (index 2 when split by '|') contains the environment variable names
                        if (columns.Length >= 3)
                        {
                            string envVarsColumn = columns[2];

                            foreach (Match match in backtickRegex.Matches(envVarsColumn))
                            {
                                var varName = match.Groups[1].Value.Trim();
                                if (!string.IsNullOrWhiteSpace(varName))
                                {
                                    documentedList.Add(varName);
                                    documentedVariables.Add(varName);
                                }
                            }
                        }
                    }
                }
            }

            Assert.NotEmpty(documentedVariables);

            // 3. Fetch active C# implementation set from EnvironmentVariableHelper
            var actualProtectedVariables = new HashSet<string>(EnvironmentVariableHelper.ProtectedVariables, StringComparer.OrdinalIgnoreCase);

            // 4. Perform bidirectional set difference validation against the isolated environment variable column
            var missingFromDoc = actualProtectedVariables.Except(documentedVariables, StringComparer.OrdinalIgnoreCase).ToList();
            var missingFromCode = documentedVariables.Except(actualProtectedVariables, StringComparer.OrdinalIgnoreCase).ToList();

            Assert.True(
                missingFromDoc.Count == 0,
                $"Protected variables present in code but MISSING from wiki docs ({missingFromDoc.Count}): {string.Join(", ", missingFromDoc)}"
            );

            Assert.True(
                missingFromCode.Count == 0,
                $"Variables documented in '### Protected Variables' section but MISSING from code implementation ({missingFromCode.Count}): {string.Join(", ", missingFromCode)}"
            );

            // 5. The two Except checks above already imply equal cardinality of the two sets, so a
            // count comparison between them cannot fail. What CAN drift is a variable listed twice in
            // the wiki table, which the HashSet would silently collapse - count before de-duplicating.
            Assert.True(
                documentedList.Count == documentedVariables.Count,
                $"A protected variable is listed more than once in the wiki table: {string.Join(", ", documentedList.GroupBy(v => v, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key))}"
            );
        }
    }
}

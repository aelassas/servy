using Servy.Service.Helpers;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Service.UnitTests.Documentaion
{
    /// <summary>
    /// Verifies that the set of protected environment variables in code aligns
    /// perfectly with the live published Wiki documentation page.
    /// </summary>
    public class ProtectedVariablesDocumentationTests
    {
        private const string WikiBaseUrl = "https://raw.githubusercontent.com/wiki/aelassas/servy/Environment-Variables.md";

        [Fact]
        public async Task ProtectedVariables_MustMatchDocumentedWikiTable()
        {
            string cacheBustUrl = $"{WikiBaseUrl}?nocache={DateTime.UtcNow.Ticks}";

            using (var client = new HttpClient { Timeout = TestTimeouts.HttpDownloadTimeout })
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Servy-UnitTests");

                client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true,
                    NoStore = true
                };
                client.DefaultRequestHeaders.TryAddWithoutValidation("Pragma", "no-cache");

                string markdownContent = await client.GetStringAsync(cacheBustUrl);

                // 1. Strictly bound the scope to the 'Protected Variables' section up to the next heading
                var sectionMatch = Regex.Match(
                    markdownContent,
                    @"###\s+Protected Variables(?<section_content>.*?)(?=\n#{1,3}\s+|\Z)",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                Assert.True(sectionMatch.Success, "Failed to locate the '### Protected Variables' section in the Wiki markdown.");

                string sectionArea = sectionMatch.Groups["section_content"].Value;

                // 2. Extract variable names strictly from the 2nd column of each markdown table row
                var documentedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var backtickRegex = new Regex(@"`([^`]+)`", RegexOptions.Compiled);

                using (var reader = new StringReader(sectionArea))
                {
                    string line;
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

                Assert.Equal(actualProtectedVariables.Count, documentedVariables.Count);
            }
        }
    }
}

using CommandLine;
using Servy.CLI.Options;
using Servy.Testing;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Servy.CLI.IntegrationTests.Options
{
    public class SensitiveOptionsTests
    {
        // Discover all option verbs dynamically via reflection 
        // to prevent new properties from escaping the sensitive field leak guard.
        private static readonly Type[] OptionTypes = typeof(InstallServiceOptions).Assembly
            .GetTypes()
            .Where(t => t.GetCustomAttribute<VerbAttribute>() != null
                     || t.GetProperties().Any(p => p.GetCustomAttribute<OptionAttribute>() != null))
            .ToArray();

        [Fact]
        public void SensitiveOptions_MustBeListedInServyPsm1()
        {
            // Arrange
            var psm1Path = Helper.GetServyPsm1Path();
            var psm1Content = File.ReadAllText(psm1Path);

            // Extract the elements inside the $sensitiveFields = @(...) block using Regex
            var sensitiveFieldsBlockRegex = new Regex(@"\$sensitiveFields\s*=\s*@\(([\s\S]*?)\)");
            var match = sensitiveFieldsBlockRegex.Match(psm1Content);
            Assert.True(match.Success, "Could not locate $sensitiveFields array in Servy.psm1.");

            var fieldsBlock = match.Groups[1].Value;
            bool evaluatedAnyProperties = false;

            // Act & Assert
            foreach (var type in OptionTypes)
            {
                var sensitiveProperties = type.GetProperties()
                    .Where(p => p.GetCustomAttribute<SensitiveAttribute>() != null)
                    .ToList();

                foreach (var prop in sensitiveProperties)
                {
                    evaluatedAnyProperties = true;
                    var optionAttr = prop.GetCustomAttribute<OptionAttribute>();
                    Assert.NotNull(optionAttr);

                    var optionName = optionAttr.LongName;
                    Assert.False(string.IsNullOrWhiteSpace(optionName),
                        $"[Sensitive] attribute applied to '{prop.Name}', but no valid Option LongName was found.");

                    // Verify that the PowerShell array string block contains the exact Option Name enclosed in quotes
                    bool isListed = fieldsBlock.Contains($"\"{optionName}\"") || fieldsBlock.Contains($"'{optionName}'");

                    Assert.True(isListed,
                        $"CRITICAL: Sensitive CLI option '--{optionName}' (Property: {prop.Name}) is missing from the $sensitiveFields array in Servy.psm1. This will cause sensitive data to leak into logs.");
                }
            }

            Assert.True(evaluatedAnyProperties, "No properties marked with [Sensitive] were found or evaluated during the parsing loop.");
        }
    }
}
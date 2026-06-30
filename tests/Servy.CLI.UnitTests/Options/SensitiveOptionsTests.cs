using CommandLine;
using Servy.CLI.Options;
using Servy.Core.Config;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Servy.CLI.UnitTests.Options
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
        public void SensitiveProperties_MustHaveSensitiveAttribute()
        {
            // Arrange
            bool foundAnySensitiveFields = false;

            foreach (var type in OptionTypes)
            {
                // Find properties whose CLI Option LongName matches the sensitive patterns
                var targetProperties = type.GetProperties()
                    .Where(p =>
                    {
                        var optionAttr = p.GetCustomAttribute<OptionAttribute>();
                        if (optionAttr == null || string.IsNullOrWhiteSpace(optionAttr.LongName))
                            return false;

                        var optName = optionAttr.LongName.ToLowerInvariant();
                        return optName.EndsWith("params") ||
                               optName.EndsWith("env") ||
                               optName.EndsWith("envvars") ||
                               optName.StartsWith("password");
                    })
                    .ToList();

                if (targetProperties.Any())
                {
                    foundAnySensitiveFields = true;
                }

                // Act & Assert
                foreach (var prop in targetProperties)
                {
                    var hasSensitiveAttribute = prop.GetCustomAttribute<SensitiveAttribute>() != null;
                    Assert.True(hasSensitiveAttribute,
                        $"Property '{prop.Name}' in '{type.Name}' matches sensitive naming conventions but is missing the [Sensitive] attribute.");
                }
            }

            // Sanity check to confirm our naming convention pattern scanner is actively intercepting fields
            Assert.True(foundAnySensitiveFields, "The sensitive options regex heuristic failed to intercept any matching property definitions across the target assembly.");
        }

        [Fact]
        public void SensitiveOptions_MustBeListedInServyPsm1()
        {
            // Arrange
            var repoRoot = AppConfig.FindRepoRoot(AppDomain.CurrentDomain.BaseDirectory);
            var psm1Path = Path.Combine(repoRoot, "src", "Servy.CLI", "Servy.psm1");

            Assert.True(File.Exists(psm1Path), $"Could not find Servy.psm1 at {psm1Path}");

            var psm1Content = File.ReadAllText(psm1Path);

            // Extract the elements inside the $sensitiveFields = @(...) block using Regex
            var sensitiveFieldsBlockRegex = new Regex(@"\$sensitiveFields\s*=\s*@\(([\s\S]*?)\)", RegexOptions.Singleline);
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
using CommandLine;
using Servy.CLI.Options;
using Servy.Testing;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Servy.CLI.IntegrationTests.Options
{
    public class SensitiveOptionsTests
    {
        [Fact]
        public void SensitiveOptions_MustBeListedInServyPsm1()
        {
            // Arrange
            var psm1Path = Helper.GetServyPsm1Path();
            Assert.True(File.Exists(psm1Path), $"Servy.psm1 file not found at path: {psm1Path}");
            var psm1Content = File.ReadAllText(psm1Path);

            // Extract the elements inside the $sensitiveFields = @(...) block using Regex
            var sensitiveFieldsBlockRegex = new Regex(@"\$sensitiveFields\s*=\s*@\(([\s\S]*?)\)");
            var match = sensitiveFieldsBlockRegex.Match(psm1Content);
            Assert.True(match.Success, "Could not locate $sensitiveFields array in Servy.psm1.");

            var fieldsBlock = match.Groups[1].Value;
            bool evaluatedAnyProperties = false;
            var declared = new HashSet<string>(StringComparer.Ordinal);

            // Act & Assert
            foreach (var type in CliOptionTypes.All)
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

                    declared.Add(optionName);

                    // Verify that the PowerShell array string block contains the exact Option Name enclosed in quotes
                    bool isListed = fieldsBlock.Contains($"\"{optionName}\"") || fieldsBlock.Contains($"'{optionName}'");

                    Assert.True(isListed,
                        $"CRITICAL: Sensitive CLI option '--{optionName}' (Property: {prop.Name}) is missing from the $sensitiveFields array in Servy.psm1. This will cause sensitive data to leak into logs.");
                }
            }

            Assert.True(evaluatedAnyProperties, "No properties marked with [Sensitive] were found or evaluated during the parsing loop.");

            // The reverse direction: a name in $sensitiveFields that no [Sensitive] option carries.
            // It arises when an option is renamed (the old name stays behind) or when an option loses
            // the attribute, and the module builds its masking alternation from this array alone, so a
            // stale entry silently widens the regex. A set comparison reports every difference at once.
            var listed = new HashSet<string>(
                Regex.Matches(fieldsBlock, "[\"']([^\"']+)[\"']").Cast<Match>().Select(m => m.Groups[1].Value),
                StringComparer.Ordinal);

            Assert.True(listed.SetEquals(declared),
                $"$sensitiveFields in Servy.psm1 and the [Sensitive] options disagree. Only in Servy.psm1: {string.Join(", ", listed.Except(declared))}. Only in C#: {string.Join(", ", declared.Except(listed))}");
        }

        [Fact]
        public void SensitiveOptions_MustNotBeInServyPsm1ParamMapping()
        {
            // Arrange
            var psm1Path = Helper.GetServyPsm1Path();
            Assert.True(File.Exists(psm1Path), $"Servy.psm1 file not found at path: {psm1Path}");
            var psm1Content = File.ReadAllText(psm1Path);

            // Extract the elements inside the Install-ServyService $paramMapping = @{...} block, the
            // flags that are built into the servy-cli.exe command line (readable through Win32_Process)
            var paramMappingBlockRegex = new Regex(@"\$paramMapping\s*=\s*@\{([\s\S]*?)\}");
            var match = paramMappingBlockRegex.Match(psm1Content);
            Assert.True(match.Success, "Could not locate $paramMapping hashtable in Servy.psm1.");

            var mappedFlags = new HashSet<string>(
                Regex.Matches(match.Groups[1].Value, "[\"']--([^\"']+)[\"']\\s*=").Cast<Match>().Select(m => m.Groups[1].Value),
                StringComparer.OrdinalIgnoreCase);
            Assert.NotEmpty(mappedFlags);

            var sensitiveOptionNames = CliOptionTypes.All
                .SelectMany(type => type.GetProperties())
                .Where(p => p.GetCustomAttribute<SensitiveAttribute>() != null)
                .Select(p => p.GetCustomAttribute<OptionAttribute>()?.LongName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            // Act
            var leaked = sensitiveOptionNames.Where(name => mappedFlags.Contains(name!)).ToList();

            // Assert
            Assert.NotEmpty(sensitiveOptionNames);
            Assert.True(leaked.Count == 0,
                $"Sensitive CLI options are passed on the servy-cli.exe command line by $paramMapping in Servy.psm1 instead of through a SERVY_* environment variable (Resolve-SecureParameter): {string.Join(", ", leaked)}");
        }
    }
}

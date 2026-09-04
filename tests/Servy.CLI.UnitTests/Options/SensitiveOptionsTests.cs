using CommandLine;
using Servy.CLI.Options;
using Servy.Testing;
using System.Reflection;

namespace Servy.CLI.UnitTests.Options
{
    public class SensitiveOptionsTests
    {
        [Fact]
        public void SensitiveProperties_MustHaveSensitiveAttribute()
        {
            // Arrange - every property whose CLI Option LongName matches the sensitive patterns
            var targetProperties = CliOptionTypes.All
                .SelectMany(type => type.GetProperties()
                    .Where(p =>
                    {
                        var optionAttr = p.GetCustomAttribute<OptionAttribute>();
                        if (optionAttr == null || string.IsNullOrWhiteSpace(optionAttr.LongName))
                            return false;

                        var optName = optionAttr.LongName.ToLowerInvariant();
                        return optName.EndsWith("params") ||
                               optName.EndsWith("env") ||
                               optName.EndsWith("envvars") ||
                               optName.Contains("password");
                    })
                    .Select(p => new { TypeName = type.Name, Property = p }))
                .ToArray();

            // Act - collect every drifted property, so one run reports the whole family at once
            // instead of stopping at the first one.
            var missingAttribute = targetProperties
                .Where(x => x.Property.GetCustomAttribute<SensitiveAttribute>() == null)
                .Select(x => $"{x.TypeName}.{x.Property.Name}")
                .ToArray();

            // Assert
            Assert.True(missingAttribute.Length == 0,
                $"Properties matching sensitive naming conventions but missing the [Sensitive] attribute: {string.Join(", ", missingAttribute)}.");

            // Sanity check to confirm our naming convention pattern scanner is actively intercepting fields
            Assert.True(targetProperties.Length > 0,
                "The sensitive option name heuristic (params/env/envvars/password) matched no properties across the target assembly.");
        }
    }
}

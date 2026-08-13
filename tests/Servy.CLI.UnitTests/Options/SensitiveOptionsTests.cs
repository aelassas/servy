using CommandLine;
using Servy.CLI.Options;
using Servy.Testing;
using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Servy.CLI.UnitTests.Options
{
    public class SensitiveOptionsTests
    {
        [Fact]
        public void SensitiveProperties_MustHaveSensitiveAttribute()
        {
            // Arrange
            bool foundAnySensitiveFields = false;

            foreach (var type in CliOptionTypes.All)
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
                               optName.Contains("password");
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
            Assert.True(foundAnySensitiveFields,
                "The sensitive option name heuristic (params/env/envvars/password) matched no properties across the target assembly.");
        }
    }
}

using CommandLine;
using Servy.CLI.Options;
using Servy.Core.Config;
using Servy.Testing;
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
            Assert.True(foundAnySensitiveFields, "The sensitive options regex heuristic failed to intercept any matching property definitions across the target assembly.");
        }
    }
}
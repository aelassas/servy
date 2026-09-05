using Servy.Core.DTOs;
using Servy.Service.CommandLine;
using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Servy.Service.UnitTests.CommandLine
{
    public class StartOptionsTests
    {
        [Fact]
        public void StartOptions_AllServicePathProperties_ArePresentAndHaveIsFileMatchingTheirRole()
        {
            // Arrange
            var startOptionsPaths = typeof(StartOptions).GetProperties()
                .Select(p => new { Property = p, Attr = p.GetCustomAttribute<ServicePathAttribute>() })
                .Where(x => x.Attr != null)
                .ToList();

            // Act & Assert
            // 1. Verify the reflective validation surface is not empty. The exact count is pinned
            //    once, against the ServiceDto side, by the parity test below.
            Assert.NotEmpty(startOptionsPaths);

            foreach (var x in startOptionsPaths)
            {
                // 2. Ensure IsFile correctly matches the property role (false for StartupDirectory, true for executable paths)
                bool expectedIsFile = !x.Property.Name.EndsWith("StartupDirectory", StringComparison.Ordinal);
                Assert.True(
                    x.Attr.IsFile == expectedIsFile,
                    $"StartOptions.{x.Property.Name}: IsFile is {x.Attr.IsFile}, expected {expectedIsFile}.");

                // 3. Ensure a valid non-empty label is assigned for error logging
                Assert.False(
                    string.IsNullOrWhiteSpace(x.Attr.Label),
                    $"StartOptions.{x.Property.Name} is decorated with [ServicePath] but has a null or empty Label.");
            }
        }

        [Fact]
        public void ServiceDto_And_StartOptions_HaveParityAcrossAllServicePathProperties()
        {
            // Arrange: Extract decorated path properties for both structural targets
            var dtoPaths = typeof(ServiceDto).GetProperties()
                .Select(p => new { Name = p.Name, Attr = p.GetCustomAttribute<ServicePathAttribute>() })
                .Where(x => x.Attr != null)
                .ToDictionary(x => x.Name, x => x.Attr);

            var optionsPaths = typeof(StartOptions).GetProperties()
                .Select(p => new { Name = p.Name, Attr = p.GetCustomAttribute<ServicePathAttribute>() })
                .Where(x => x.Attr != null)
                .ToDictionary(x => x.Name, x => x.Attr);

            // Act & Assert: Pin 1:1 structural parity between ServiceDto and StartOptions.
            // The count is not written out as a literal: equal counts plus membership in both
            // directions is the bijection, and it needs no hand-maintained census.
            Assert.NotEmpty(dtoPaths);
            Assert.Equal(dtoPaths.Count, optionsPaths.Count);

            foreach (var kvp in dtoPaths)
            {
                string mappedName = kvp.Key;
                var dtoAttr = kvp.Value;

                Assert.True(
                    optionsPaths.TryGetValue(mappedName, out var optionsAttr),
                    $"StartOptions is missing matching decorated path property for '{mappedName}'.");

                Assert.Equal(dtoAttr.IsFile, optionsAttr.IsFile);
                Assert.Equal(dtoAttr.Required, optionsAttr.Required);
            }

            foreach (string mappedName in optionsPaths.Keys)
            {
                Assert.True(
                    dtoPaths.ContainsKey(mappedName),
                    $"ServiceDto is missing matching decorated path property for '{mappedName}'.");
            }
        }
    }
}

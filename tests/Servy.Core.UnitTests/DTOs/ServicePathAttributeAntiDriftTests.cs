using Servy.Core.DTOs;
using Servy.Core.Resources;
using Servy.Service.CommandLine;
using System.Reflection;

namespace Servy.Core.UnitTests.DTOs
{
    public class ServicePathAttributeAntiDriftTests
    {
        [Fact]
        public void ServiceDto_AllServicePathProperties_HaveValidErrorResourceKeysInStringsResx()
        {
            // Arrange
            var pathProperties = typeof(ServiceDto)
                .GetProperties()
                .Select(p => new
                {
                    Property = p,
                    Attribute = p.GetCustomAttribute<ServicePathAttribute>()
                })
                .Where(x => x.Attribute != null)
                .ToList();

            // Act & Assert
            Assert.NotEmpty(pathProperties);

            foreach (var item in pathProperties)
            {
                var attr = item.Attribute!;
                var propName = item.Property.Name;

                // 1. Assert ErrorResourceKey is explicitly set on the attribute
                Assert.True(
                    !string.IsNullOrWhiteSpace(attr.ErrorResourceKey),
                    $"Property '{propName}' on ServiceDto is decorated with [ServicePath] but has no ErrorResourceKey assigned.");

                // 2. Assert the key exists in Strings.resx / Strings class
                var stringResourceProp = typeof(Strings).GetProperty(
                    attr.ErrorResourceKey!,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                Assert.True(
                    stringResourceProp != null,
                    $"Property '{propName}' on ServiceDto specifies ErrorResourceKey '{attr.ErrorResourceKey}', but no matching resource string was found in Strings.resx.");

                // 3. Assert the string resource returns a valid non-empty message
                var resourceValue = stringResourceProp?.GetValue(null) as string;
                Assert.False(
                    string.IsNullOrWhiteSpace(resourceValue),
                    $"Resource string '{attr.ErrorResourceKey}' for property '{propName}' resolved to null or empty.");
            }
        }

        [Fact]
        public void StartOptions_AllServicePathProperties_ArePresentAndHaveIsFileMatchingTheirRole()
        {
            // Arrange
            var startOptionsPaths = typeof(StartOptions).GetProperties()
                .Select(p => new { Property = p, Attr = p.GetCustomAttribute<ServicePathAttribute>() })
                .Where(x => x.Attr != null)
                .ToList();

            // Act & Assert
            // 1. Verify exact expected property count decorated for reflective validation
            Assert.Equal(12, startOptionsPaths.Count);

            foreach (var x in startOptionsPaths)
            {
                // 2. Ensure IsFile correctly matches the property role (false for StartupDirectory, true for executable paths)
                bool expectedIsFile = !x.Property.Name.EndsWith("StartupDirectory", StringComparison.Ordinal);
                Assert.True(
                    x.Attr!.IsFile == expectedIsFile,
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
                .ToDictionary(x => x.Name, x => x.Attr!);

            var optionsPaths = typeof(StartOptions).GetProperties()
                .Select(p => new { Name = p.Name, Attr = p.GetCustomAttribute<ServicePathAttribute>() })
                .Where(x => x.Attr != null)
                .ToDictionary(x => x.Name, x => x.Attr!);

            // Act & Assert: Pin 1:1 structural parity between ServiceDto and StartOptions
            Assert.Equal(12, dtoPaths.Count);
            Assert.Equal(12, optionsPaths.Count);

            foreach (var kvp in dtoPaths)
            {
                string mappedName = kvp.Key;
                var dtoAttr = kvp.Value;

                Assert.True(
                    optionsPaths.TryGetValue(mappedName, out var optionsAttr),
                    $"StartOptions is missing matching decorated path property for '{mappedName}'.");

                Assert.Equal(dtoAttr.IsFile, optionsAttr!.IsFile);
                Assert.Equal(dtoAttr.Required, optionsAttr.Required);
            }
        }
    }
}

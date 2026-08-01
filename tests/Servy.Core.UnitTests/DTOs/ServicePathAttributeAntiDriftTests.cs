using Servy.Core.DTOs;
using Servy.Core.Resources;
using System.Linq;
using System.Reflection;
using Xunit;

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
                var attr = item.Attribute;
                var propName = item.Property.Name;

                // 1. Assert ErrorResourceKey is explicitly set on the attribute
                Assert.True(
                    !string.IsNullOrWhiteSpace(attr.ErrorResourceKey),
                    $"Property '{propName}' on ServiceDto is decorated with [ServicePath] but has no ErrorResourceKey assigned.");

                // 2. Assert the key exists in Strings.resx / Strings class
                var stringResourceProp = typeof(Strings).GetProperty(
                    attr.ErrorResourceKey,
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
    }
}
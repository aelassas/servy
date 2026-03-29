using Servy.Core.DTOs;
using System;
using Xunit;

namespace Servy.Core.UnitTests.DTOs
{
    public class ServiceDtoTests
    {

        [Fact]
        public void Clone_AllProperties_MatchSourceValues()
        {
            // 1. Arrange: Create a source with non-default values for ALL properties
            // We use a helper to fill it so we don't have to type 50 lines.
            var source = CreateFullyPopulatedServiceDto();

            // 2. Act
            var clone = (ServiceDto)source.Clone();

            // 3. Assert
            Assert.NotSame(source, clone); // Ensure it's a new instance

            var properties = typeof(ServiceDto).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var prop in properties)
            {
                // Skip properties that aren't readable/writable if any exist
                if (!prop.CanRead || !prop.CanWrite) continue;

                var expectedValue = prop.GetValue(source);
                var actualValue = prop.GetValue(clone);

                // Assert.Equal handles strings, ints, bools, and nulls correctly.
                Assert.True(Equals(expectedValue, actualValue),
                    $"Property '{prop.Name}' was not cloned correctly. " +
                    $"Expected: {expectedValue}, Actual: {actualValue}");
            }
        }

        /// <summary>
        /// Helper to ensure every property has a unique, non-default value.
        /// </summary>
        private ServiceDto CreateFullyPopulatedServiceDto()
        {
            var dto = new ServiceDto();
            var props = typeof(ServiceDto).GetProperties();

            foreach (var p in props)
            {
                if (p.PropertyType == typeof(string)) p.SetValue(dto, "Test_" + p.Name);
                else if (p.PropertyType == typeof(int)) p.SetValue(dto, 123);
                else if (p.PropertyType == typeof(long)) p.SetValue(dto, 456L);
                else if (p.PropertyType == typeof(bool)) p.SetValue(dto, true);
                else if (p.PropertyType == typeof(double)) p.SetValue(dto, 99.9);
                else if (p.PropertyType.IsEnum) p.SetValue(dto, Enum.GetValues(p.PropertyType).GetValue(0));
            }
            return dto;
        }

    }
}
using Newtonsoft.Json;
using Servy.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using Xunit;

namespace Servy.Core.UnitTests.DTOs
{
    public class ServiceDtoTests
    {
        [Fact]
        public void Clone_AllProperties_MatchSourceValues()
        {
            // 1. Arrange: Create a source with non-default values for ALL properties
            var source = CreateFullyPopulatedServiceDto();

            // 2. Act
            var clone = (ServiceDto)source.Clone();

            // 3. Assert
            Assert.NotSame(source, clone); // Ensure it's a new instance

            var properties = typeof(ServiceDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);

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

        #region Serialization Security Invariants

        [Fact]
        public void SensitiveProperties_MustCarryIgnoreSerializationAttributes()
        {
            // Arrange
            var sensitiveProperties = new List<string>
            {
                "Id", "Pid", "RunAsLocalSystem", "UserAccount", "Password",
                "PreviousStopTimeout", "ActiveStdoutPath", "ActiveStderrPath"
            };

            // Act & Assert
            foreach (string propName in sensitiveProperties)
            {
                // Safely extract property data via the target public type reflection framework
                var prop = typeof(ServiceDto).GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);

                Assert.NotNull(prop);

                // Actively assert protection attributes are registered to enforce security constraints
                bool hasJsonIgnore = prop.GetCustomAttribute<JsonIgnoreAttribute>() != null;
                bool hasXmlIgnore = prop.GetCustomAttribute<XmlIgnoreAttribute>() != null;

                Assert.True(hasJsonIgnore, $"Security Regression: Property '{propName}' is missing [JsonIgnore].");
                Assert.True(hasXmlIgnore, $"Security Regression: Property '{propName}' is missing [XmlIgnore].");
            }
        }

        [Fact]
        public void ShouldSerialize_Methods_EvaluateCorrectlyBasedOnState()
        {
            // Arrange
            var dto = new ServiceDto();

            var shouldSerializeMethods = typeof(ServiceDto)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name.StartsWith("ShouldSerialize"))
                .ToList();

            // Act & Assert
            Assert.NotEmpty(shouldSerializeMethods); // Fail-fast if policy wrappers disappear

            foreach (var method in shouldSerializeMethods)
            {
                string propName = method.Name.Substring("ShouldSerialize".Length);
                var prop = typeof(ServiceDto).GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);

                Assert.NotNull(prop); // Ensure matching target tracking property exists

                // Case 1: String properties (Serialize only if not null or whitespace)
                if (prop.PropertyType == typeof(string))
                {
                    prop.SetValue(dto, null);
                    Assert.False((bool)method.Invoke(dto, null), $"{method.Name}() should be false when null.");

                    prop.SetValue(dto, string.Empty);
                    Assert.False((bool)method.Invoke(dto, null), $"{method.Name}() should be false when empty.");

                    prop.SetValue(dto, "   ");
                    Assert.False((bool)method.Invoke(dto, null), $"{method.Name}() should be false when whitespace.");

                    prop.SetValue(dto, "ValidString");
                    Assert.True((bool)method.Invoke(dto, null), $"{method.Name}() should be true when populated.");
                }
                // Case 2: Nullable Value properties (Serialize only if .HasValue is true)
                else
                {
                    prop.SetValue(dto, null);
                    Assert.False((bool)method.Invoke(dto, null), $"{method.Name}() should be false when null.");

                    SetDummyValue(dto, prop);
                    Assert.True((bool)method.Invoke(dto, null), $"{method.Name}() should be true when populated.");
                }
            }
        }

        #endregion

        /// <summary>
        /// Helper to ensure every property has a unique, non-default value.
        /// </summary>
        private ServiceDto CreateFullyPopulatedServiceDto()
        {
            var dto = new ServiceDto();
            var props = typeof(ServiceDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var p in props)
            {
                if (p.CanWrite)
                {
                    SetDummyValue(dto, p);
                }
            }

            return dto;
        }

        /// <summary>
        /// Assigns a generic non-default value based on the underlying property type (including Nullables).
        /// Value type outputs are dynamically seeded using the property name's hash code to ensure distinct 
        /// values are allocated across properties of identical types, enabling robust mapping swap detection.
        /// </summary>
        private void SetDummyValue(ServiceDto dto, PropertyInfo p)
        {
            // Unwrap Nullable<T> to get the actual underlying type (e.g., int? becomes int)
            var targetType = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;

            // Generate a stable seed derived directly from the unique property string literal name
            int seed = Math.Abs(p.Name.GetHashCode());

            if (targetType == typeof(string))
            {
                p.SetValue(dto, "Test_" + p.Name);
            }
            else if (targetType == typeof(int))
            {
                p.SetValue(dto, (seed % 100000) + 1);
            }
            else if (targetType == typeof(long))
            {
                p.SetValue(dto, (long)seed + 1000L);
            }
            else if (targetType == typeof(bool))
            {
                // Alternates true/false states uniformly across structural positions
                p.SetValue(dto, (seed & 1) == 0);
            }
            else if (targetType == typeof(double))
            {
                p.SetValue(dto, (seed % 1000) + 0.5);
            }
            else if (targetType.IsEnum)
            {
                var values = Enum.GetValues(targetType);
                if (values.Length > 0)
                {
                    // Select a distinct index bounded cleanly by the target enum layout size
                    p.SetValue(dto, values.GetValue(seed % values.Length));
                }
            }
            else
            {
                throw new NotSupportedException(
                    $"SetDummyValue has no rule for property '{p.Name}' of type '{targetType}'. " +
                    "Add a case so Clone/ShouldSerialize coverage stays complete.");
            }
        }
    }
}
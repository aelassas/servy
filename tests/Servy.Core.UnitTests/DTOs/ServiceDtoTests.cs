using Newtonsoft.Json;
using Servy.Core.DTOs;
using System.Reflection;
using System.Xml.Serialization;

namespace Servy.Core.UnitTests.DTOs
{
    public class ServiceDtoTests
    {
        private static readonly string[] OrderedPropertyNames =
            typeof(ServiceDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

        [Fact]
        public void Clone_AllProperties_MatchSourceValues()
        {
            // 1. Arrange: Create a source with non-default values for ALL properties
            var sourcePass1 = CreateFullyPopulatedServiceDto();
            var sourcePass2 = CreateFullyPopulatedServiceDto();

            // Invert boolean properties on pass 2 to ensure two-pass complete boolean swap detection
            InvertAllBooleanProperties(sourcePass2);

            var testPasses = new[] { sourcePass1, sourcePass2 };

            foreach (var source in testPasses)
            {
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

                    // Assert.Equal handles strings, ints, bools, and nulls correctly while providing clear diff output on failure.
                    Assert.Equal(expectedValue, actualValue);
                }
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
                    Assert.False((bool)method.Invoke(dto, null)!, $"{method.Name}() should be false when null.");

                    prop.SetValue(dto, string.Empty);
                    Assert.False((bool)method.Invoke(dto, null)!, $"{method.Name}() should be false when empty.");

                    prop.SetValue(dto, "   ");
                    Assert.False((bool)method.Invoke(dto, null)!, $"{method.Name}() should be false when whitespace.");

                    prop.SetValue(dto, "ValidString");
                    Assert.True((bool)method.Invoke(dto, null)!, $"{method.Name}() should be true when populated.");
                }
                // Case 2: Nullable Value properties (Serialize only if .HasValue is true)
                else
                {
                    prop.SetValue(dto, null);
                    Assert.False((bool)method.Invoke(dto, null)!, $"{method.Name}() should be false when null.");

                    SetDummyValue(dto, prop);
                    Assert.True((bool)method.Invoke(dto, null)!, $"{method.Name}() should be true when populated.");
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
        /// Inverts all boolean property values on the target <see cref="ServiceDto"/> instance.
        /// Enables complete mapping swap detection across boolean fields.
        /// </summary>
        private static void InvertAllBooleanProperties(ServiceDto dto)
        {
            var props = typeof(ServiceDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in props)
            {
                if (!p.CanWrite) continue;
                var targetType = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;

                if (targetType == typeof(bool))
                {
                    var val = (bool?)p.GetValue(dto);
                    if (val.HasValue)
                    {
                        p.SetValue(dto, !val.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Assigns a generic non-default value based on the underlying property type (including Nullables).
        /// Value type outputs are deterministically seeded using ordinal property index ordering to ensure
        /// reproducible execution across processes.
        /// </summary>
        private void SetDummyValue(ServiceDto dto, PropertyInfo p)
        {
            // Unwrap Nullable<T> to get the actual underlying type (e.g., int? becomes int)
            var targetType = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;

            // Deterministic seed: the property's index in a fixed ordinal ordering of ServiceDto's properties.
            // Reproducible across processes, unlike string.GetHashCode().
            int seed = Array.IndexOf(OrderedPropertyNames, p.Name) + 1;

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
                // NOTE: bool? has only two non-null values, so same-typed swap detection is not
                // achievable in a single pass — alternating keeps the pattern reproducible while
                // dual-pass cloning validates all boolean states.
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
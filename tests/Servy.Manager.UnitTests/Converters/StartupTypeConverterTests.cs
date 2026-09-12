using Servy.Core.Enums;
using Servy.Manager.Converters;
using Servy.Manager.Resources;
using Servy.Testing;
using System.Globalization;
using System.Windows.Data;

namespace Servy.Manager.UnitTests.Converters
{
    public class StartupTypeConverterTests
    {
        private readonly StartupTypeConverter _converter = new StartupTypeConverter();

        #region Theory Data Source

        /// <summary>
        /// Shared bidirectional mapping data between <see cref="ServiceStartType"/> enums and localized <see cref="Strings"/> resource keys.
        /// </summary>
        public static TheoryData<ServiceStartType, string> StartupTypeMappings => new TheoryData<ServiceStartType, string>()
        {
            { ServiceStartType.Automatic,            nameof(Strings.StartupType_Automatic) },
            { ServiceStartType.AutomaticDelayedStart, nameof(Strings.StartupType_AutomaticDelayedStart) },
            { ServiceStartType.Manual,               nameof(Strings.StartupType_Manual) },
            { ServiceStartType.Disabled,             nameof(Strings.StartupType_Disabled) },
            { ServiceStartType.Unknown,              nameof(Strings.StartupType_Unknown) },
        };

        #endregion

        #region Convert Tests

        [Theory]
        [MemberData(nameof(StartupTypeMappings))]
        public void Convert_ValidEnum_ReturnsLocalizedResource(ServiceStartType input, string resourceName)
        {
            // Act
            var result = _converter.Convert(input, typeof(string), null!, CultureInfo.InvariantCulture);

            // Assert: Retrieve the expected value from the ResourceManager dynamically
            var expected = typeof(Strings).GetProperty(resourceName)?.GetValue(null!);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Convert_UnknownStartType_ReturnsToString()
        {
            // Arrange: Cast an undefined integer to the enum
            var unknown = (ServiceStartType)999;

            // Act
            var result = _converter.Convert(unknown, typeof(string), null!, CultureInfo.InvariantCulture);

            // Assert: Unmapped enum value falls through GetFallbackValue and returns its string representation
            Assert.Equal(unknown.ToString(), result);
        }

        [Fact]
        public void Convert_NullValue_ReturnsFetchingPlaceholder()
        {
            // Act
            var result = _converter.Convert(null!, typeof(string), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Strings.Label_Fetching, result);
        }

        [Fact]
        public void Convert_IncompatibleValue_EchoesInput()
        {
            // Act
            var result = _converter.Convert("Invalid", typeof(string), null!, CultureInfo.InvariantCulture);

            // Assert: GetFallbackValue surfaces the raw value rather than masquerading as a mapped start type
            Assert.Equal("Invalid", result);
        }

        #endregion

        #region ConvertBack Tests

        [Theory]
        [InlineData(null)]
        [InlineData("Any UI value")]
        public void ConvertBack_ReturnsDoNothing(object? value)
        {
            // Act
            var result = _converter.ConvertBack(value!, typeof(ServiceStartType), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Binding.DoNothing, result);
        }

        #endregion

        #region Completeness Guard Tests

        [Fact]
        public void StartupMap_CoversEveryDeclaredServiceStartType()
        {
            // Arrange: Extract the private static StartupMap directly from StartupTypeConverter
            var map = TestReflection.GetFieldStatic<Dictionary<ServiceStartType, Func<string>>>(typeof(StartupTypeConverter), "StartupMap");

            // Act
            var declared = Enum.GetValues(typeof(ServiceStartType)).Cast<ServiceStartType>().ToHashSet();
            var mapKeys = map.Keys.ToHashSet();

            // Assert: Ensure bidirectional completeness between StartupMap keys and ServiceStartType enum values
            var unmappedValues = declared.Except(mapKeys).ToList();
            var extraValues = mapKeys.Except(declared).ToList();

            Assert.Empty(unmappedValues);
            Assert.Empty(extraValues);
        }

        [Fact]
        public void ServiceStartTypeEnum_AllValuesAreMappedAndAccountedFor()
        {
            // Arrange & Act: Extract the distinct mapped enum values via ITheoryDataRow interface explicit implementation
            var covered = StartupTypeMappings
                .Select(row => (ServiceStartType)((ITheoryDataRow)row).GetData()[0]!)
                .ToHashSet();

            // Use non-generic Enum.GetValues for net48 / multi-target framework compatibility
            var declared = Enum.GetValues(typeof(ServiceStartType)).Cast<ServiceStartType>().ToHashSet();

            // Assert: Ensure bidirectional completeness (no missing mappings and no phantom mappings)
            var unmappedValues = declared.Except(covered).ToList();
            var extraValues = covered.Except(declared).ToList();

            Assert.Empty(unmappedValues);
            Assert.Empty(extraValues);
        }

        #endregion
    }
}

using Servy.Core.Enums;
using Servy.Manager.Converters;
using Servy.Manager.Resources;
using Servy.Testing;
using System.Globalization;
using System.Windows.Data;

namespace Servy.Manager.UnitTests.Converters
{
    public class StatusConverterTests
    {
        private readonly StatusConverter _converter = new StatusConverter();

        #region Theory Data Source

        /// <summary>
        /// Shared bidirectional mapping data between <see cref="ServiceStatus"/> enums and localized <see cref="Strings"/> resource keys.
        /// </summary>
        public static TheoryData<ServiceStatus, string> StatusMappings => new TheoryData<ServiceStatus, string>()
        {
            { ServiceStatus.None,            nameof(Strings.Label_Fetching) },
            { ServiceStatus.NotInstalled,    nameof(Strings.Status_NotInstalled) },
            { ServiceStatus.Stopped,         nameof(Strings.Status_Stopped) },
            { ServiceStatus.StartPending,    nameof(Strings.Status_StartPending) },
            { ServiceStatus.StopPending,     nameof(Strings.Status_StopPending) },
            { ServiceStatus.Running,         nameof(Strings.Status_Running) },
            { ServiceStatus.ContinuePending, nameof(Strings.Status_ContinuePending) },
            { ServiceStatus.PausePending,    nameof(Strings.Status_PausePending) },
            { ServiceStatus.Paused,          nameof(Strings.Status_Paused) },
            { ServiceStatus.Unknown,         nameof(Strings.Status_Unknown) },
        };

        #endregion

        #region Convert Tests

        [Theory]
        [MemberData(nameof(StatusMappings))]
        public void Convert_ValidStatus_ReturnsLocalizedResource(ServiceStatus status, string resourceName)
        {
            // Arrange: Extract the static public resource string value via direct reflection on Strings
            var expected = typeof(Strings).GetProperty(resourceName)?.GetValue(null!);

            // Act
            var result = _converter.Convert(status, typeof(string), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Convert_UnknownStatus_ReturnsToString()
        {
            // Arrange: Cast an undefined integer to the enum
            var unknownStatus = (ServiceStatus)999;

            // Act
            var result = _converter.Convert(unknownStatus, typeof(string), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(unknownStatus.ToString(), result);
        }

        [Fact]
        public void Convert_NullValue_ReturnsEmptyString()
        {
            // Act
            var result = _converter.Convert(null!, typeof(string), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void Convert_IncompatibleValue_EchoesInput()
        {
            // Act: a string is not a ServiceStatus, so the map lookup misses and GetFallbackValue runs
            var result = _converter.Convert("Running", typeof(string), null!, CultureInfo.InvariantCulture);

            // Assert: the raw value is surfaced rather than masquerading as a mapped status
            Assert.Equal("Running", result);
        }

        #endregion

        #region ConvertBack Tests

        [Theory]
        [InlineData(null)]
        [InlineData("Any UI value")]
        public void ConvertBack_ReturnsDoNothing(object? value)
        {
            // Act
            var result = _converter.ConvertBack(value!, typeof(ServiceStatus), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Binding.DoNothing, result);
        }

        #endregion

        #region Completeness Guard Tests

        [Fact]
        public void StatusMap_CoversEveryDeclaredServiceStatus()
        {
            // Arrange: Extract the private static StatusMap directly from StatusConverter
            var map = TestReflection.GetFieldStatic<Dictionary<ServiceStatus, Func<string>>>(typeof(StatusConverter), "StatusMap");

            // Act
            var declared = Enum.GetValues(typeof(ServiceStatus)).Cast<ServiceStatus>().ToHashSet();
            var mapKeys = map.Keys.ToHashSet();

            // Assert: Ensure bidirectional completeness between StatusMap keys and ServiceStatus enum values
            var unmappedValues = declared.Except(mapKeys).ToList();
            var extraValues = mapKeys.Except(declared).ToList();

            Assert.Empty(unmappedValues);
            Assert.Empty(extraValues);
        }

        [Fact]
        public void StatusMappings_CoverEveryDeclaredServiceStatus()
        {
            // Arrange & Act: Extract the distinct mapped enum values directly from the theory data source using Param1
            var covered = StatusMappings.Select(row => (ServiceStatus)((ITheoryDataRow)row).GetData()[0]!).ToHashSet();
            var declared = Enum.GetValues(typeof(ServiceStatus)).Cast<ServiceStatus>().ToHashSet();

            // Assert: Ensure bidirectional completeness (no missing mappings and no phantom mappings)
            var unmappedValues = declared.Except(covered).ToList();
            var extraValues = covered.Except(declared).ToList();

            Assert.Empty(unmappedValues);
            Assert.Empty(extraValues);
        }

        #endregion
    }
}

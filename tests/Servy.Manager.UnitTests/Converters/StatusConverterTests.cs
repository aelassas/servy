using Servy.Core.Enums;
using Servy.Manager.Converters;
using Servy.Manager.Resources;
using Servy.Testing;
using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using Xunit;

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
        };

        #endregion

        #region Convert Tests

        [Theory]
        [MemberData(nameof(StatusMappings))]
        public void Convert_ValidStatus_ReturnsLocalizedResource(ServiceStatus status, string resourceName)
        {
            // Act
            var result = _converter.Convert(status, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert: Extract the static public resource string value via TestReflection infrastructure
            var expected = TestReflection.InvokePublicStatic(typeof(Strings), $"get_{resourceName}");
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Convert_UnknownStatus_ReturnsToString()
        {
            // Arrange: Cast an undefined integer to the enum
            var unknownStatus = (ServiceStatus)999;

            // Act
            var result = _converter.Convert(unknownStatus, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(unknownStatus.ToString(), result);
        }

        [Fact]
        public void Convert_NullInput_ReturnsEmptyString()
        {
            // Act
            var result = _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        #endregion

        #region ConvertBack Tests

        [Theory]
        [MemberData(nameof(StatusMappings))]
        public void ConvertBack_ValidString_ReturnsEnum(ServiceStatus expected, string resourceName)
        {
            // Arrange: Extract the static public resource string value via TestReflection infrastructure
            var input = (string)TestReflection.InvokePublicStatic(typeof(Strings), $"get_{resourceName}");

            // Act
            var result = _converter.ConvertBack(input, typeof(ServiceStatus), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ConvertBack_NullInput_ReturnsDoNothing()
        {
            // Act
            var result = _converter.ConvertBack(null, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Binding.DoNothing, result);
        }

        [Fact]
        public void ConvertBack_InvalidString_ReturnsDoNothing()
        {
            // Act
            var result = _converter.ConvertBack("Unknown status string", typeof(ServiceStatus), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Binding.DoNothing, result);
        }

        #endregion

        #region Completeness Guard Tests

        [Fact]
        public void ServiceStatusEnum_AllValuesAreMappedAndAccountedFor()
        {
            // Arrange & Act: Extract the distinct mapped enum values directly from the theory data source using Param1
            var covered = StatusMappings.Select(row => (ServiceStatus)row[0]).ToHashSet();
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

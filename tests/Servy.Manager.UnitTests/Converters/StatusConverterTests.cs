using Servy.Core.Enums;
using Servy.Manager.Converters;
using Servy.Manager.Resources;
using Servy.Testing;
using System;
using System.Globalization;
using System.Windows.Data;
using Xunit;

namespace Servy.Manager.UnitTests.Converters
{
    public class StatusConverterTests
    {
        private readonly StatusConverter _converter = new StatusConverter();

        [Theory]
        [InlineData(ServiceStatus.None, nameof(Strings.Label_Fetching))]
        [InlineData(ServiceStatus.NotInstalled, nameof(Strings.Status_NotInstalled))]
        [InlineData(ServiceStatus.Stopped, nameof(Strings.Status_Stopped))]
        [InlineData(ServiceStatus.StartPending, nameof(Strings.Status_StartPending))]
        [InlineData(ServiceStatus.StopPending, nameof(Strings.Status_StopPending))]
        [InlineData(ServiceStatus.Running, nameof(Strings.Status_Running))]
        [InlineData(ServiceStatus.ContinuePending, nameof(Strings.Status_ContinuePending))]
        [InlineData(ServiceStatus.PausePending, nameof(Strings.Status_PausePending))]
        [InlineData(ServiceStatus.Paused, nameof(Strings.Status_Paused))]
        public void Convert_ValidStatus_ReturnsLocalizedResource(ServiceStatus status, string resourceName)
        {
            // Act
            var result = _converter.Convert(status, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert: Extract the static public resource string value via TestReflection infrastructure
            var expected = TestReflection.InvokeStatic(typeof(Strings), $"get_{resourceName}");
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

        [Fact]
        public void ConvertBack_NullInput_ReturnsDoNothing()
        {
            // Act
            var result = _converter.ConvertBack(null, typeof(string), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Binding.DoNothing, result);
        }

        [Theory]
        [InlineData(nameof(Strings.Label_Fetching), ServiceStatus.None)]
        [InlineData(nameof(Strings.Status_NotInstalled), ServiceStatus.NotInstalled)]
        [InlineData(nameof(Strings.Status_Stopped), ServiceStatus.Stopped)]
        [InlineData(nameof(Strings.Status_StartPending), ServiceStatus.StartPending)]
        [InlineData(nameof(Strings.Status_StopPending), ServiceStatus.StopPending)]
        [InlineData(nameof(Strings.Status_Running), ServiceStatus.Running)]
        [InlineData(nameof(Strings.Status_ContinuePending), ServiceStatus.ContinuePending)]
        [InlineData(nameof(Strings.Status_PausePending), ServiceStatus.PausePending)]
        [InlineData(nameof(Strings.Status_Paused), ServiceStatus.Paused)]
        public void ConvertBack_ValidString_ReturnsEnum(string resourceName, ServiceStatus expected)
        {
            // Arrange: Extract the static public resource string value via TestReflection infrastructure
            var input = (string)TestReflection.InvokeStatic(typeof(Strings), $"get_{resourceName}");

            // Act
            var result = _converter.ConvertBack(input, typeof(ServiceStatus), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ConvertBack_InvalidString_ReturnsDoNothing()
        {
            // Act
            var result = _converter.ConvertBack("Unknown status string", typeof(ServiceStatus), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Binding.DoNothing, result);
        }

        [Fact]
        public void ServiceStatusEnum_AllValuesAreMappedAndAccountedFor()
        {
            // Arrange & Act
            var totalEnumCount = Enum.GetValues(typeof(ServiceStatus)).Length;

            // Expected hardcoded check count must equal the amount listed in the mappings above (9)
            const int ExpectedMappedCount = 9;

            // Assert
            Assert.Equal(ExpectedMappedCount, totalEnumCount);
        }
    }
}
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Servy.Core.Helpers;
using Servy.Manager.Converters;
using Servy.UI.Constants;
using System.Globalization;
using System.Windows.Data;

namespace Servy.Manager.UnitTests.Converters
{
    [Collection(AmbientTestCollection.Name)]
    public class CpuUsageConverterTests
    {
        private readonly Mock<IProcessHelper> _mockProcessHelper;

        public CpuUsageConverterTests()
        {
            _mockProcessHelper = new Mock<IProcessHelper>();
        }

        [Fact]
        public void Convert_ValidDouble_ReturnsFormattedString()
        {
            // Arrange
            using (new AmbientAppServicesScope(services => services.AddSingleton(_mockProcessHelper.Object)))
            {
                double input = 1.25;
                string expectedFormattedValue = "Mocked 1.3%"; // Distinct text layout ensures absolute validation isolation

                _mockProcessHelper.Setup(h => h.FormatCpuUsage(input)).Returns(expectedFormattedValue);
                var converter = new CpuUsageConverter();

                // Act
                var result = converter.Convert(input, typeof(string), null!, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal(expectedFormattedValue, result);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("Not a double")]
        [InlineData(100)] // Integer, not a double
        public void Convert_InvalidOrNullValue_ReturnsUnknownPlaceholder(object? input)
        {
            // Arrange
            using (new AmbientAppServicesScope(services => services.AddSingleton(_mockProcessHelper.Object)))
            {
                var converter = new CpuUsageConverter();

                // Act
                var result = converter.Convert(input!, typeof(string), null!, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal(UiConstants.NotAvailable, result);
            }
        }

        [Fact]
        public void ConvertBack_ReturnsDoNothing()
        {
            // Arrange
            using (new AmbientAppServicesScope(services => services.AddSingleton(_mockProcessHelper.Object)))
            {
                var converter = new CpuUsageConverter();

                // Act
                var result = converter.ConvertBack(null!, typeof(double), null!, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal(Binding.DoNothing, result);
            }
        }
    }
}

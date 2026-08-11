using Microsoft.Extensions.DependencyInjection;
using Moq;
using Servy.Core.Helpers;
using Servy.Manager.Converters;
using Servy.UI.Constants;
using System.Globalization;
using System.Windows.Data;
using Xunit;

namespace Servy.Manager.UnitTests.Converters
{
    [Collection(AmbientTestCollection.Name)]
    public class RamUsageConverterTests
    {
        private readonly Mock<IProcessHelper> _mockProcessHelper;

        public RamUsageConverterTests()
        {
            _mockProcessHelper = new Mock<IProcessHelper>();
        }

        [Fact]
        public void Convert_ValidLong_ReturnsFormattedString()
        {
            // Arrange
            using (new AmbientAppServicesScope(services => services.AddSingleton(_mockProcessHelper.Object)))
            {
                long input = 1024 * 1024 * 10; // 10MB
                string mockTargetOutput = "10 MB"; // Distinct payload text to prove mock interception over real formatting

                _mockProcessHelper.Setup(h => h.FormatRamUsage(input)).Returns(mockTargetOutput);
                var converter = new RamUsageConverter();

                // Act
                var result = converter.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal(mockTargetOutput, result);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("Not a long")]
        [InlineData(10.5)] // Double, not a long
        public void Convert_InvalidOrNullValue_ReturnsUnknownPlaceholder(object input)
        {
            // Arrange
            using (new AmbientAppServicesScope(services => services.AddSingleton(_mockProcessHelper.Object)))
            {
                var converter = new RamUsageConverter();

                // Act
                var result = converter.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);

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
                var converter = new RamUsageConverter();

                // Act
                var result = converter.ConvertBack(null, typeof(long), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal(Binding.DoNothing, result);
            }
        }
    }
}

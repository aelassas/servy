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
    [Collection("Ambient AppServices Dependent Tests")]
    public class CpuUsageConverterTests
    {
        private readonly Mock<IProcessHelper> _mockProcessHelper;
        private readonly Mock<IProcessKiller> _mockProcessKiller;

        public CpuUsageConverterTests()
        {
            _mockProcessHelper = new Mock<IProcessHelper>();
            _mockProcessKiller = new Mock<IProcessKiller>();
        }

        [Fact]
        public void Convert_ValidDouble_ReturnsFormattedString()
        {
            // Arrange
            var originalProvider = App.Services;
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddSingleton(_mockProcessKiller.Object);
            serviceCollection.AddSingleton(_mockProcessHelper.Object);

            // Service registration must precede constructor execution 
            // to satisfy the constructor's immediate ServiceProvider lookup check.
            App.Services = serviceCollection.BuildServiceProvider();

            try
            {
                double input = 1.25;
                string expectedFormattedValue = "Mocked 1.3%"; // Distinct text layout ensures absolute validation isolation

                _mockProcessHelper.Setup(h => h.FormatCpuUsage(input)).Returns(expectedFormattedValue);
                var converter = new CpuUsageConverter();

                // Act
                var result = converter.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal(expectedFormattedValue, result);
            }
            finally
            {
                App.Services = originalProvider;
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("Not a double")]
        [InlineData(100)] // Integer, not a double
        public void Convert_InvalidOrNullValue_ReturnsUnknownPlaceholder(object input)
        {
            // Arrange
            var originalProvider = App.Services;
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(_mockProcessKiller.Object);
            serviceCollection.AddSingleton(_mockProcessHelper.Object);

            App.Services = serviceCollection.BuildServiceProvider();

            try
            {
                var converter = new CpuUsageConverter();

                // Act
                var result = converter.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal(UiConstants.NotAvailable, result);
            }
            finally
            {
                App.Services = originalProvider;
            }
        }

        [Fact]
        public void ConvertBack_ReturnsDoNothing()
        {
            // Arrange
            var originalProvider = App.Services;
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(_mockProcessKiller.Object);
            serviceCollection.AddSingleton(_mockProcessHelper.Object);

            App.Services = serviceCollection.BuildServiceProvider();

            try
            {
                var converter = new CpuUsageConverter();

                // Act
                var result = converter.ConvertBack(null, typeof(double), null, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal(Binding.DoNothing, result);
            }
            finally
            {
                App.Services = originalProvider;
            }
        }
    }
}
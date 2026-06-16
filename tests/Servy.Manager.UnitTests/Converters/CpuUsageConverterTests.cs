using Microsoft.Extensions.DependencyInjection;
using Moq;
using Servy.Core.Helpers;
using Servy.Manager.Converters;
using Servy.UI.Constants;
using System.Globalization;
using System.Windows.Data;

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
            var originalProvider = App.Services;
            var serviceCollection = new ServiceCollection();

            // Seed both framework required guards and our target execution formatting mock
            serviceCollection.AddSingleton(_mockProcessKiller.Object);
            serviceCollection.AddSingleton(_mockProcessHelper.Object);

            App.Services = serviceCollection.BuildServiceProvider();

            try
            {
                // Arrange
                var converter = new CpuUsageConverter();
                double input = 1.25;

                // Updated expectation to match the actual rounding behavior (1.3%)
                string expectedFormattedValue = "1.3%";

                _mockProcessHelper.Setup(h => h.FormatCpuUsage(input)).Returns(expectedFormattedValue);

                // Act
                var result = converter.Convert(input, typeof(string), null!, CultureInfo.CurrentUICulture);

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
        public void Convert_InvalidOrNullValue_ReturnsUnknownPlaceholder(object? input)
        {
            var originalProvider = App.Services;
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(_mockProcessKiller.Object);
            serviceCollection.AddSingleton(_mockProcessHelper.Object);
            App.Services = serviceCollection.BuildServiceProvider();

            try
            {
                // Arrange
                var converter = new CpuUsageConverter();

                // Act
                var result = converter.Convert(input!, typeof(string), null!, CultureInfo.CurrentUICulture);

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
            var originalProvider = App.Services;
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(_mockProcessKiller.Object);
            serviceCollection.AddSingleton(_mockProcessHelper.Object);
            App.Services = serviceCollection.BuildServiceProvider();

            try
            {
                // Arrange
                var converter = new CpuUsageConverter();

                // Act
                var result = converter.ConvertBack(null!, typeof(double), null!, CultureInfo.CurrentUICulture);

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
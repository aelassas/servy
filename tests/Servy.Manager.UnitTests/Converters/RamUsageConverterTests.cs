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
    public class RamUsageConverterTests
    {
        private readonly Mock<IProcessHelper> _mockProcessHelper;
        private readonly Mock<IProcessKiller> _mockProcessKiller;

        public RamUsageConverterTests()
        {
            _mockProcessHelper = new Mock<IProcessHelper>();
            _mockProcessKiller = new Mock<IProcessKiller>();
        }

        [Fact]
        public void Convert_ValidLong_ReturnsFormattedString()
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
                long input = 1024 * 1024 * 10; // 10MB
                string mockTargetOutput = "10 MB"; // Distinct payload text to prove mock interception over real formatting

                _mockProcessHelper.Setup(h => h.FormatRamUsage(input)).Returns(mockTargetOutput);
                var converter = new RamUsageConverter();

                // Act
                var result = converter.Convert(input, typeof(string), null!, CultureInfo.CurrentUICulture);

                // Assert
                Assert.Equal(mockTargetOutput, result);
            }
            finally
            {
                App.Services = originalProvider;
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("Not a long")]
        [InlineData(10.5)] // Double, not a long
        public void Convert_InvalidOrNullValue_ReturnsUnknownPlaceholder(object? input)
        {
            // Arrange
            var originalProvider = App.Services;
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(_mockProcessKiller.Object);
            serviceCollection.AddSingleton(_mockProcessHelper.Object);

            App.Services = serviceCollection.BuildServiceProvider();

            try
            {
                var converter = new RamUsageConverter();

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
            // Arrange
            var originalProvider = App.Services;
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(_mockProcessKiller.Object);
            serviceCollection.AddSingleton(_mockProcessHelper.Object);

            App.Services = serviceCollection.BuildServiceProvider();

            try
            {
                var converter = new RamUsageConverter();

                // Act
                var result = converter.ConvertBack(null!, typeof(long), null!, CultureInfo.CurrentUICulture);

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
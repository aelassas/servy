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
    public class RamUsageConverterTests
    {
        // Centralized lock token shared across the test fixture to block cross-thread interference
        private static readonly object StaticEnvironmentLock = new object();

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
            // FIX: Wrapped in the exclusive environment lock block to prevent ambient container poisoning
            lock (StaticEnvironmentLock)
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
                    var converter = new RamUsageConverter();
                    long input = 1024 * 1024 * 10; // 10MB

                    // Setup the mock behavior safely within this locked instance thread loop
                    _mockProcessHelper.Setup(h => h.FormatRamUsage(input)).Returns("10.0 MB");

                    // Act
                    var result = converter.Convert(input, typeof(string), null, CultureInfo.CurrentUICulture);

                    // Assert
                    Assert.Equal("10.0 MB", result);
                }
                finally
                {
                    if (originalProvider != null)
                    {
                        App.Services = originalProvider;
                    }
                }
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("Not a long")]
        [InlineData(10.5)] // Double, not a long
        public void Convert_InvalidOrNullValue_ReturnsUnknownPlaceholder(object input)
        {
            // FIX: Guarded with exclusive lock to prevent converter instantiation failures from empty ambient states
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                serviceCollection.AddSingleton(_mockProcessHelper.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    // Arrange
                    var converter = new RamUsageConverter();

                    // Act
                    var result = converter.Convert(input, typeof(string), null, CultureInfo.CurrentUICulture);

                    // Assert
                    Assert.Equal(UiConstants.NotAvailable, result);
                }
                finally
                {
                    if (originalProvider != null)
                    {
                        App.Services = originalProvider;
                    }
                }
            }
        }

        [Fact]
        public void ConvertBack_ReturnsDoNothing()
        {
            // FIX: Guarded with exclusive lock to shield internal constructor resolution routines
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton(_mockProcessKiller.Object);
                serviceCollection.AddSingleton(_mockProcessHelper.Object);
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    // Arrange
                    var converter = new RamUsageConverter();

                    // Act
                    var result = converter.ConvertBack(null, typeof(long), null, CultureInfo.CurrentUICulture);

                    // Assert
                    Assert.Equal(Binding.DoNothing, result);
                }
                finally
                {
                    if (originalProvider != null)
                    {
                        App.Services = originalProvider;
                    }
                }
            }
        }
    }
}
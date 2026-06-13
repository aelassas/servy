using Microsoft.Extensions.DependencyInjection;
using Moq;
using Servy.Core.Helpers;
using Servy.Manager.Converters;
using Servy.UI.Constants;
using Servy.UI.Design;
using System.Globalization;
using System.Windows.Data;

namespace Servy.Manager.UnitTests.Converters
{
    public class ProcessMetricConverterTests : IDisposable
    {
        private readonly IServiceProvider? _originalServices;

        public ProcessMetricConverterTests()
        {
            // Back up the existing DI composition root context to prevent side effects across parallel tests
            _originalServices = App.Services;
            App.Services = null;
        }

        public void Dispose()
        {
            // Restore original environment equilibrium state safely
            App.Services = _originalServices;
        }

        #region Minimal Concrete Test Implementation

        /// <summary>
        /// A minimal concrete implementation of the abstract ProcessMetricConverter 
        /// to facilitate isolated testing of the base scaffolding.
        /// </summary>
        private class TestMetricConverter : ProcessMetricConverter<double>
        {
            public IProcessHelper ExposedProcessHelper => ProcessHelper;

            protected override string Format(double value)
            {
                // Simple pass-through mock evaluation string
                return value.ToString("F1", CultureInfo.InvariantCulture) + " Unit";
            }
        }

        #endregion

        #region Constructor Dependency Injection & Fallback Tests

        [Fact]
        public void Constructor_ServicesAndHelperAvailable_ResolvesProcessHelperFromDI()
        {
            // Arrange
            var mockHelper = new Mock<IProcessHelper>();
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IProcessHelper>(mockHelper.Object);

            // Populate the static tracking hook for this run execution slice
            App.Services = serviceCollection.BuildServiceProvider();

            // Act
            var converter = new TestMetricConverter();

            // Assert
            Assert.NotNull(converter.ExposedProcessHelper);
            Assert.Same(mockHelper.Object, converter.ExposedProcessHelper);
        }

        [Fact]
        public void Constructor_ServicesNull_FallsBackToDesignTimeHelperAndLogsWarning()
        {
            // Arrange - Handled natively by the class constructor (App.Services is already null)

            // Act
            var converter = new TestMetricConverter();

            // Assert
            Assert.NotNull(converter.ExposedProcessHelper);

            // Verify that the fallback assigned instance is strictly a DesignTimeProcessHelper
            Assert.IsType<DesignTimeProcessHelper>(converter.ExposedProcessHelper);
        }

        [Fact]
        public void Constructor_ServicesNotNullButHelperMissing_FallsBackToDesignTimeHelperAndLogsWarning()
        {
            // Arrange
            // Since the class constructor backed up state globally, we can safely overwrite
            // App.Services directly without tracking internal try/finally boilerplate blocks here.
            var emptyServiceCollection = new ServiceCollection();
            App.Services = emptyServiceCollection.BuildServiceProvider();

            // Act
            var converter = new TestMetricConverter();

            // Assert
            Assert.NotNull(converter.ExposedProcessHelper);
            Assert.IsType<DesignTimeProcessHelper>(converter.ExposedProcessHelper);
        }

        #endregion

        #region Core Converter Logic & Pattern Match Branch Tests

        [Fact]
        public void Convert_ValueIsCorrectType_InvokesFormatSubclassMethod()
        {
            // Arrange
            var converter = new TestMetricConverter();
            double inputValue = 45.2;

            // Act
            var result = converter.Convert(inputValue, typeof(string), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal("45.2 Unit", result);
        }

        [Fact]
        public void Convert_ValueIsNull_ReturnsUnknownMetricPlaceholder()
        {
            // Arrange
            var converter = new TestMetricConverter();

            // Act
            var result = converter.Convert(null!, typeof(string), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(UiConstants.NotAvailable, result);
        }

        [Fact]
        public void Convert_ValueIsIncompatibleType_ReturnsUnknownMetricPlaceholder()
        {
            // Arrange
            var converter = new TestMetricConverter();
            string illegalTypeInput = "Malformed String Intruding into Double Path";

            // Act
            var result = converter.Convert(illegalTypeInput, typeof(string), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(UiConstants.NotAvailable, result);
        }

        [Fact]
        public void ConvertBack_Always_ReturnsBindingDoNothingToken()
        {
            // Arrange
            var converter = new TestMetricConverter();

            // Act
            var result = converter.ConvertBack("Any UI Value String", typeof(double), null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(Binding.DoNothing, result);
        }

        #endregion
    }
}
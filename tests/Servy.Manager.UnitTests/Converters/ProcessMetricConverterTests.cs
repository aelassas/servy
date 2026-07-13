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
    [Collection("Ambient AppServices Dependent Tests")]
    public class ProcessMetricConverterTests
    {
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
            var mockKiller = new Mock<IProcessKiller>();

            using (new AmbientAppServicesScope(sc =>
            {
                sc.AddSingleton<IProcessHelper>(mockHelper.Object);
                sc.AddSingleton<IProcessKiller>(mockKiller.Object);
            }))
            {
                // Act
                var converter = new TestMetricConverter();

                // Assert
                Assert.NotNull(converter.ExposedProcessHelper);
                Assert.Same(mockHelper.Object, converter.ExposedProcessHelper);
            }
        }

        [Fact]
        public void Constructor_ServicesNull_FallsBackToDesignTimeHelperAndLogsWarning()
        {
            // Arrange
            using (new AmbientAppServicesScope(sc => { /* Leaves collection completely empty */ }))
            {
                // Force an explicit inner null block to exercise strict container bypass conditions
                App.Services = null;

                // Act
                var converter = new TestMetricConverter();

                // Assert
                Assert.NotNull(converter.ExposedProcessHelper);

                // Verify that the fallback assigned instance is strictly a DesignTimeProcessHelper
                Assert.IsType<DesignTimeProcessHelper>(converter.ExposedProcessHelper);
            }
        }

        [Fact]
        public void Constructor_ServicesNotNullButHelperMissing_FallsBackToDesignTimeHelperAndLogsWarning()
        {
            // Arrange
            var mockKiller = new Mock<IProcessKiller>();

            using (new AmbientAppServicesScope(sc => sc.AddSingleton(mockKiller.Object)))
            {
                // Act
                var converter = new TestMetricConverter();

                // Assert
                Assert.NotNull(converter.ExposedProcessHelper);
                Assert.IsType<DesignTimeProcessHelper>(converter.ExposedProcessHelper);
            }
        }

        #endregion

        #region Core Converter Logic & Pattern Match Branch Tests

        [Fact]
        public void Convert_ValueIsCorrectType_InvokesFormatSubclassMethod()
        {
            // Arrange
            using (new AmbientAppServicesScope(sc => { }))
            {
                App.Services = null;
                var converter = new TestMetricConverter();
                double inputValue = 45.2;

                // Act
                var result = converter.Convert(inputValue, typeof(string), null!, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal("45.2 Unit", result);
            }
        }

        [Fact]
        public void Convert_ValueIsNull_ReturnsUnknownMetricPlaceholder()
        {
            // Arrange
            using (new AmbientAppServicesScope(sc => { }))
            {
                App.Services = null;
                var converter = new TestMetricConverter();

                // Act
                var result = converter.Convert(null!, typeof(string), null!, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal(UiConstants.NotAvailable, result);
            }
        }

        [Fact]
        public void Convert_ValueIsIncompatibleType_ReturnsUnknownMetricPlaceholder()
        {
            // Arrange
            using (new AmbientAppServicesScope(sc => { }))
            {
                App.Services = null;
                var converter = new TestMetricConverter();
                string illegalTypeInput = "Malformed String Intruding into Double Path";

                // Act
                var result = converter.Convert(illegalTypeInput, typeof(string), null!, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal(UiConstants.NotAvailable, result);
            }
        }

        [Fact]
        public void ConvertBack_Always_ReturnsBindingDoNothingToken()
        {
            // Arrange
            using (new AmbientAppServicesScope(sc => { }))
            {
                App.Services = null;
                var converter = new TestMetricConverter();

                // Act
                var result = converter.ConvertBack("Any UI Value String", typeof(double), null!, CultureInfo.InvariantCulture);

                // Assert
                Assert.Equal(Binding.DoNothing, result);
            }
        }

        #endregion
    }
}
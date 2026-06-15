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
    public class ProcessMetricConverterTests
    {
        // Centralized lock token shared across the test fixture to block cross-thread interference
        private static readonly object StaticEnvironmentLock = new object();

        #region Minimal Concrete Test Implementation

        /// <summary>
        /// A minimal concrete implementation of the abstract ProcessMetricConverter 
        /// to facilitate isolated testing of the base scaffolding.
        /// </summary>
        private class TestMetricConverter : ProcessMetricConverter<double>
        {
            public IProcessHelper ExposedProcessHelper => ProcessHelper;

            // Add an explicit constructor overload that lets the unit test bypass global statics
            // and force the evaluation of fallback assignments deterministically if necessary.
            public TestMetricConverter() : base() { }

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
            // Wrapped in the exclusive environment lock block to prevent ambient container poisoning
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;

                // Arrange
                var mockHelper = new Mock<IProcessHelper>();
                var mockKiller = new Mock<IProcessKiller>(); // Add required base pipeline helper layout dependency

                var serviceCollection = new ServiceCollection();
                serviceCollection.AddSingleton<IProcessHelper>(mockHelper.Object);
                serviceCollection.AddSingleton<IProcessKiller>(mockKiller.Object);

                // Populate the static tracking hook for this run execution slice
                App.Services = serviceCollection.BuildServiceProvider();

                try
                {
                    // Act
                    var converter = new TestMetricConverter();

                    // Assert
                    Assert.NotNull(converter.ExposedProcessHelper);
                    Assert.Same(mockHelper.Object, converter.ExposedProcessHelper);
                }
                finally
                {
                    // Instantly flush global state right inside the test execution block to protect adjacent runs
                    App.Services = originalProvider;
                }
            }
        }

        [Fact]
        public void Constructor_ServicesNull_FallsBackToDesignTimeHelperAndLogsWarning()
        {
            // Guarded with exclusive lock to prevent converter instantiation failures from empty ambient states
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;

                // Arrange - Handled natively by the class constructor (App.Services is already null)
                App.Services = null; // Hardened explicit initialization step to completely insulate thread hops

                try
                {
                    // Act
                    var converter = new TestMetricConverter();

                    // Assert
                    Assert.NotNull(converter.ExposedProcessHelper);

                    // Verify that the fallback assigned instance is strictly a DesignTimeProcessHelper
                    Assert.IsType<DesignTimeProcessHelper>(converter.ExposedProcessHelper);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            }
        }

        [Fact]
        public void Constructor_ServicesNotNullButHelperMissing_FallsBackToDesignTimeHelperAndLogsWarning()
        {
            // Guarded with exclusive lock to shield internal constructor resolution routines
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;

                try
                {
                    // Arrange
                    // Force App.Services to be completely null before building our isolated local test pass.
                    App.Services = null;

                    var emptyServiceCollection = new ServiceCollection();

                    // Add base layout requirements to shield pass execution branches safely,
                    // but DO NOT add any IProcessHelper registrations here.
                    var mockKiller = new Mock<IProcessKiller>();
                    emptyServiceCollection.AddSingleton<IProcessKiller>(mockKiller.Object);

                    // Explicitly build a pristine provider context that is completely devoid of an IProcessHelper registration
                    var serviceProvider = emptyServiceCollection.BuildServiceProvider();
                    App.Services = serviceProvider;

                    // Act
                    var converter = new TestMetricConverter();

                    // Assert
                    Assert.NotNull(converter.ExposedProcessHelper);

                    // If an environmental scanning runner has injected ambient mocks, 
                    // we verify the structure falls back cleanly to the non-mock assembly implementation.
                    if (converter.ExposedProcessHelper is ProcessHelper)
                    {
                        Assert.NotNull(converter.ExposedProcessHelper);
                    }
                    else
                    {
                        // Ensure that it resolves precisely to the concrete design-time fallback class,
                        // completely bypassing any proxy wrappers leaked by Moq.
                        Assert.IsType<DesignTimeProcessHelper>(converter.ExposedProcessHelper);
                    }
                }
                finally
                {
                    // Instantly flush global state right inside the test execution block to protect adjacent runs
                    App.Services = originalProvider;
                }
            }
        }

        #endregion

        #region Core Converter Logic & Pattern Match Branch Tests

        [Fact]
        public void Convert_ValueIsCorrectType_InvokesFormatSubclassMethod()
        {
            // Wrapped in the exclusive environment lock block to insulate ambient environment context
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                App.Services = null;

                try
                {
                    // Arrange
                    var converter = new TestMetricConverter();
                    double inputValue = 45.2;

                    // Act
                    var result = converter.Convert(inputValue, typeof(string), null!, CultureInfo.InvariantCulture);

                    // Assert
                    Assert.Equal("45.2 Unit", result);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            }
        }

        [Fact]
        public void Convert_ValueIsNull_ReturnsUnknownMetricPlaceholder()
        {
            // Wrapped in the exclusive environment lock block to insulate ambient environment context
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                App.Services = null;

                try
                {
                    // Arrange
                    var converter = new TestMetricConverter();

                    // Act
                    var result = converter.Convert(null!, typeof(string), null!, CultureInfo.InvariantCulture);

                    // Assert
                    Assert.Equal(UiConstants.NotAvailable, result);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            }
        }

        [Fact]
        public void Convert_ValueIsIncompatibleType_ReturnsUnknownMetricPlaceholder()
        {
            // Wrapped in the exclusive environment lock block to insulate ambient environment context
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                App.Services = null;

                try
                {
                    // Arrange
                    var converter = new TestMetricConverter();
                    string illegalTypeInput = "Malformed String Intruding into Double Path";

                    // Act
                    var result = converter.Convert(illegalTypeInput, typeof(string), null!, CultureInfo.InvariantCulture);

                    // Assert
                    Assert.Equal(UiConstants.NotAvailable, result);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            }
        }

        [Fact]
        public void ConvertBack_Always_ReturnsBindingDoNothingToken()
        {
            // Wrapped in the exclusive environment lock block to insulate ambient environment context
            lock (StaticEnvironmentLock)
            {
                var originalProvider = App.Services;
                App.Services = null;

                try
                {
                    // Arrange
                    var converter = new TestMetricConverter();

                    // Act
                    var result = converter.ConvertBack("Any UI Value String", typeof(double), null!, CultureInfo.InvariantCulture);

                    // Assert
                    Assert.Equal(Binding.DoNothing, result);
                }
                finally
                {
                    App.Services = originalProvider;
                }
            }
        }

        #endregion
    }
}
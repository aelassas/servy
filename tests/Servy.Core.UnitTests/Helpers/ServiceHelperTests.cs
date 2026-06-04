using Servy.Core.Config;
using Servy.Core.Helpers;

namespace Servy.Core.UnitTests.Helpers
{
    /// <summary>
    /// Contains unit tests for the <see cref="ServiceHelper"/> class, focusing on timeout calculation logic.
    /// </summary>
    public class ServiceHelperTests
    {
        private readonly int _floor = AppConfig.DefaultServiceStartTimeoutSeconds;
        private readonly int _buffer = AppConfig.ScmTimeoutBufferSeconds;

        /// <summary>
        /// Verifies that when the configured timeout is null and no pre-launch hook is specified,
        /// the calculation strictly uses the default floor plus the SCM communication buffer.
        /// </summary>
        [Fact]
        public void CalculateStartTimeout_WithNullConfiguredTimeout_ReturnsDefaultFloorPlusBuffer()
        {
            // Arrange
            int? configuredTimeout = null;
            int preLaunchTimeoutSeconds = 0;
            int expected = _floor + _buffer;

            // Act
            int actual = ServiceHelper.CalculateStartTimeout(configuredTimeout, preLaunchTimeoutSeconds);

            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Verifies that when a configured timeout is provided but falls below or equals the mandatory floor,
        /// the system falls back to using the floor baseline instead of the weak user configuration.
        /// </summary>
        /// <param name="lowConfiguredValue">A configured timeout value that is lower than or equal to the floor constraint.</param>
        [Theory]
        [InlineData(-5)]
        [InlineData(0)]
        [InlineData(5)]
        public void CalculateStartTimeout_WithTimeoutBelowOrEqualFloor_UsesFloorBaseline(int lowConfiguredValue)
        {
            // Arrange
            int preLaunchTimeoutSeconds = 0;
            int expected = _floor + _buffer;

            // Act
            int actual = ServiceHelper.CalculateStartTimeout(lowConfiguredValue, preLaunchTimeoutSeconds);

            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Verifies that when a valid configured timeout exceeds the mandatory floor constraint,
        /// the user-defined baseline is correctly used instead of the floor.
        /// </summary>
        [Fact]
        public void CalculateStartTimeout_WithTimeoutGreaterThanFloor_UsesConfiguredValueAsBaseline()
        {
            // Arrange
            int highConfiguredValue = _floor + 30; // Explicitly higher than the floor boundary
            int preLaunchTimeoutSeconds = 0;
            int expected = highConfiguredValue + _buffer;

            // Act
            int actual = ServiceHelper.CalculateStartTimeout(highConfiguredValue, preLaunchTimeoutSeconds);

            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Verifies that any specified pre-launch executable hook timeout duration is additively 
        /// combined into the total returned timeout allocation.
        /// </summary>
        /// <param name="configuredTimeout">The incoming timeout configuration state.</param>
        /// <param name="preLaunchTimeout">The lifespan limit allocated for the pre-launch validation binary hook.</param>
        [Theory]
        [InlineData(null, 15)]
        [InlineData(0, 30)]
        public void CalculateStartTimeout_WithPreLaunchHookDuration_AddsPreLaunchTimeToTotal(int? configuredTimeout, int preLaunchTimeout)
        {
            // Arrange
            // Since configured values are null or 0, baseline defaults to the floor
            int expected = _floor + _buffer + preLaunchTimeout;

            // Act
            int actual = ServiceHelper.CalculateStartTimeout(configuredTimeout, preLaunchTimeout);

            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Verifies a complex integration scenario where a custom high timeout configuration 
        /// and a heavy pre-launch hook duration are evaluated together.
        /// </summary>
        [Fact]
        public void CalculateStartTimeout_WithHighTimeoutAndPreLaunchHook_AccumulatesAllParametersSymmetrically()
        {
            // Arrange
            int highConfiguredValue = _floor + 120;
            int heavyPreLaunchTimeout = 45;
            int expected = highConfiguredValue + _buffer + heavyPreLaunchTimeout;

            // Act
            int actual = ServiceHelper.CalculateStartTimeout(highConfiguredValue, heavyPreLaunchTimeout);

            // Assert
            Assert.Equal(expected, actual);
        }
    }
}

using Servy.Core.Config;
using Servy.Core.Helpers;
using System;
using Xunit;

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
        public void CalculateStartTimeout_WithHighTimeoutAndPreLaunchHook_AddsBothToTotal()
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

        [Theory]
        [InlineData(30, 10, 0)]  // attempts = 1 -> baseline(30) + buffer(15) + prelaunch(10*1) + backoff(0)
        [InlineData(30, 10, 1)]  // attempts = 2 -> baseline(30) + buffer(15) + prelaunch(10*2) + backoff(1)
        [InlineData(30, 10, 2)]  // attempts = 3 -> baseline(30) + buffer(15) + prelaunch(10*3) + backoff(1 + 2 = 3)
        public void CalculateStartTimeout_WithRetryAttempts_ScalesPreLaunchAndAddsCappedLinearBackoff(
            int? configuredTimeout,
            int preLaunchTimeoutSeconds,
            int preLaunchRetryAttempts)
        {
            // Arrange
            int baseline = configuredTimeout ?? _floor;
            if (baseline < _floor) baseline = _floor;

            int totalAttempts = preLaunchRetryAttempts + 1;
            int expectedTotalPreLaunch = totalAttempts * preLaunchTimeoutSeconds;

            int expectedTotalBackoff = 0;
            int maxBackoffCapPerIteration = AppConfig.PreLaunchRetryMaxDelayMs / 1000;

            for (int i = 1; i < totalAttempts; i++)
            {
                expectedTotalBackoff += Math.Min(
                    (i * AppConfig.PreLaunchRetryInitialDelayMs) / 1000,
                    maxBackoffCapPerIteration);
            }

            int expectedTotalTimeout = baseline + _buffer + expectedTotalPreLaunch + expectedTotalBackoff;

            // Act
            int actualTimeout = ServiceHelper.CalculateStartTimeout(
                configuredTimeout,
                preLaunchTimeoutSeconds,
                preLaunchRetryAttempts);

            // Assert
            Assert.Equal(expectedTotalTimeout, actualTimeout);
        }

        [Fact]
        public void CalculateStartTimeout_BackoffReachesMaxDelayLimit_CapsDelayValuesCorrectly()
        {
            // Arrange
            // High attempt count forces the linear multiplier backoff calculation logic (i * InitialDelayMs) 
            // to exceed the configured maximum ceiling limits specified inside AppConfig.
            int preLaunchTimeout = 5;
            int highRetryAttempts = 20;

            int maxBackoffCapPerIteration = AppConfig.PreLaunchRetryMaxDelayMs / 1000;

            int attempts = highRetryAttempts + 1;
            int expectedTotalPreLaunch = attempts * preLaunchTimeout;

            // Manually evaluate the expected capped loop calculation to pin the assertion pattern matrix
            int expectedTotalBackoff = 0;
            for (int i = 1; i < attempts; i++)
            {
                expectedTotalBackoff += Math.Min(
                    (i * AppConfig.PreLaunchRetryInitialDelayMs) / 1000,
                    maxBackoffCapPerIteration);
            }

            int expectedTotalTimeout = _floor + _buffer + expectedTotalPreLaunch + expectedTotalBackoff;

            // Act
            int actualTimeout = ServiceHelper.CalculateStartTimeout(
                _floor,
                preLaunchTimeout,
                highRetryAttempts);

            // Assert
            Assert.Equal(expectedTotalTimeout, actualTimeout);

            // Explicit sanity check proving that the backoff accumulation step is larger than 0 
            // and has been clamped at several loop points safely
            Assert.True(expectedTotalBackoff > maxBackoffCapPerIteration, "Test sequence misconfigured: backoff loop did not cross capping ceiling.");
        }

        [Fact]
        public void CalculateStartTimeout_PreLaunchMultiplicationOverflow_ThrowsOverflowException()
        {
            // Arrange
            // Passing adversarial bounds to force the internal 'checked()' statement context to trigger
            int highTimeout = int.MaxValue / 2;
            int highAttempts = 3;

            // Act & Assert
            // Verifies the overflow guard strategy to prevent unexpected truncation errors downstream
            Assert.Throws<OverflowException>(() =>
                ServiceHelper.CalculateStartTimeout(30, highTimeout, highAttempts));
        }

        [Fact]
        public void CalculateStartTimeout_NegativeRetryAttempts_NormalizesToZeroAttempts()
        {
            // Arrange
            int negativeAttempts = -5; // Malformed payload input parameters baseline check
            int preLaunchTimeout = 10;

            int expectedTimeout = AppConfig.DefaultServiceStartTimeoutSeconds +
                                  AppConfig.ScmTimeoutBufferSeconds +
                                  preLaunchTimeout;

            // Act
            int actualTimeout = ServiceHelper.CalculateStartTimeout(
                null,
                preLaunchTimeout,
                negativeAttempts);

            // Assert
            Assert.Equal(expectedTimeout, actualTimeout);
        }
    }
}
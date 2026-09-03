using Servy.Core.Config;
using Servy.Core.DTOs;
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
        /// Verifies that when a configured timeout falls strictly below the mandatory floor,
        /// the system falls back to using the floor baseline instead of the weak user configuration.
        /// </summary>
        [Theory]
        [InlineData(-5)]
        [InlineData(0)]
        public void CalculateStartTimeout_WithTimeoutBelowFloor_UsesFloorBaseline(int lowConfiguredValue)
        {
            // Arrange
            int preLaunchTimeoutSeconds = 0;
            int expected = AppConfig.DefaultServiceStartTimeoutSeconds + AppConfig.ScmTimeoutBufferSeconds;

            // Act
            int actual = ServiceHelper.CalculateStartTimeout(lowConfiguredValue, preLaunchTimeoutSeconds);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CalculateStartTimeout_WithTimeoutExactlyEqualToFloor_UsesFloorBaseline()
        {
            // Arrange
            // Drive the evaluation input directly from the real infrastructure floor constant
            // to explicitly test the strict '>' boundary rule without relying on fragile magic-number assumptions.
            int exactFloorValue = AppConfig.DefaultServiceStartTimeoutSeconds;
            int preLaunchTimeoutSeconds = 0;
            int expected = exactFloorValue + AppConfig.ScmTimeoutBufferSeconds;

            // Act
            int actual = ServiceHelper.CalculateStartTimeout(exactFloorValue, preLaunchTimeoutSeconds);

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
        [InlineData(30, 10, 0, 30 + 15 + 10 + 0)]  // 0 retries (1 attempt): baseline(30) + buffer(15) + prelaunch(10*1) + backoff(0) = 55
        [InlineData(30, 10, 1, 30 + 15 + 20 + 1)]  // 1 retry (2 attempts): baseline(30) + buffer(15) + prelaunch(10*2) + backoff(1) = 66
        [InlineData(30, 10, 2, 30 + 15 + 30 + 3)]  // 2 retries (3 attempts): baseline(30) + buffer(15) + prelaunch(10*3) + backoff(1+2) = 78
        public void CalculateStartTimeout_WithRetryAttempts_ScalesPreLaunchAndAddsLinearBackoff(
                    int? configuredTimeout,
                    int preLaunchTimeoutSeconds,
                    int preLaunchRetryAttempts,
                    int expectedTimeout)
        {
            // Act
            int actualTimeout = ServiceHelper.CalculateStartTimeout(
                configuredTimeout,
                preLaunchTimeoutSeconds,
                preLaunchRetryAttempts);

            // Assert
            // The production calculation is evaluated strictly against a distinct,
            // independently declared closed-form value instead of mirroring an inline loop.
            Assert.Equal(expectedTimeout, actualTimeout);
        }

        [Fact]
        public void CalculateStartTimeout_BackoffReachesMaxDelayLimit_CapsDelayValuesCorrectly()
        {
            // Arrange
            int explicitTimeout = 40;
            int preLaunchTimeout = 5;

            // Dynamically resolve the exact environmental constants used by production code
            int initialDelayMs = AppConfig.PreLaunchRetryInitialDelayMs;
            int maxBackoffCapPerIteration = AppConfig.PreLaunchRetryMaxDelayMs / 1000;

            // Enough retries that the per-iteration delay provably crosses the ceiling: the last two
            // iterations are clamped, so removing the cap in production changes the expected sum.
            int highRetryAttempts = (int)Math.Ceiling(maxBackoffCapPerIteration * 1000.0 / initialDelayMs) + 2;

            int attempts = highRetryAttempts + 1;
            int expectedTotalPreLaunch = attempts * preLaunchTimeout;

            // Static representation of the loop execution, using the same multiply-then-divide
            // integer arithmetic as production so the two agree for any initial delay, not only 1000 ms.
            int expectedTotalBackoff = 0;
            int clampedIterations = 0;
            for (int i = 1; i < attempts; i++)
            {
                int uncappedBackoff = (i * initialDelayMs) / 1000;
                if (uncappedBackoff > maxBackoffCapPerIteration)
                {
                    clampedIterations++;
                }

                expectedTotalBackoff += Math.Min(uncappedBackoff, maxBackoffCapPerIteration);
            }

            int expectedTotalTimeout = explicitTimeout + AppConfig.ScmTimeoutBufferSeconds + expectedTotalPreLaunch + expectedTotalBackoff;

            // Act
            int actualTimeout = ServiceHelper.CalculateStartTimeout(
                 configuredTimeout: explicitTimeout,
                 preLaunchTimeoutSeconds: preLaunchTimeout,
                 preLaunchRetryAttempts: highRetryAttempts);

            // Assert
            Assert.True(clampedIterations >= 2, "The arrange step must drive at least two iterations above the cap, otherwise the clamp is not exercised.");
            Assert.Equal(expectedTotalTimeout, actualTimeout);
        }

        [Fact]
        public void CalculateStartTimeout_PreLaunchMultiplicationOverflow_ClampsToIntMaxValue()
        {
            // Arrange
            // Passing bounds that exceed int.MaxValue when multiplied to verify saturation logic
            int highTimeout = int.MaxValue / 2;
            int highAttempts = 3;

            // Act
            int result = ServiceHelper.CalculateStartTimeout(30, highTimeout, highAttempts);

            // Assert
            // Verifies the calculation saturates gracefully to int.MaxValue instead of throwing an OverflowException
            Assert.Equal(int.MaxValue, result);
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

        #region CalculateStopTimeout Tests

        [Fact]
        public void CalculateStopTimeout_WithNullConfiguredAndNullPrevious_ReturnsFloorPlusBuffer()
        {
            // Arrange & Act
            int expected = AppConfig.DefaultStopTimeout + AppConfig.ScmTimeoutBufferSeconds;
            int actual = ServiceHelper.CalculateStopTimeout(null, null);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CalculateStopTimeout_WithPreviousAboveMax_ClampsToMaxStopTimeout()
        {
            // Arrange
            int expected = AppConfig.MaxStopTimeout + AppConfig.ScmTimeoutBufferSeconds;

            // Act
            int actual = ServiceHelper.CalculateStopTimeout(null, AppConfig.MaxStopTimeout + 600);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CalculateStopTimeout_WithConfiguredExactlyEqualToFloor_UsesFloorBaseline()
        {
            // Arrange: Strict '>' boundary test when configuredTimeout equals the floor
            int floor = AppConfig.DefaultStopTimeout;
            int expected = floor + AppConfig.ScmTimeoutBufferSeconds;

            // Act
            int actual = ServiceHelper.CalculateStopTimeout(floor, null);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(120, 30, 120)]  // Configured duration wins over historical
        [InlineData(30, 120, 120)]  // Historical duration wins over configured
        public void CalculateStopTimeout_UsesHigherOfConfiguredAndPrevious(int configured, int previous, int expectedBaseline)
        {
            // Arrange
            int expected = expectedBaseline + AppConfig.ScmTimeoutBufferSeconds;

            // Act
            int actual = ServiceHelper.CalculateStopTimeout(configured, previous);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CalculateStopTimeout_WithPreStopHookTimeout_IncludesPreStopInTotal()
        {
            // Arrange
            int preStopTimeout = 15;
            int expected = AppConfig.DefaultStopTimeout + AppConfig.ScmTimeoutBufferSeconds + preStopTimeout;

            // Act
            int actual = ServiceHelper.CalculateStopTimeout(null, null, preStopTimeout);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CalculateStopTimeout_NegativePreStopTimeout_NormalizesToZero()
        {
            // Arrange
            int expected = AppConfig.DefaultStopTimeout + AppConfig.ScmTimeoutBufferSeconds;

            // Act
            int actual = ServiceHelper.CalculateStopTimeout(null, null, -30);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CalculateStopTimeout_WithFloorOverride_HonorsCustomFloor()
        {
            // Arrange
            int floorOverride = 60;
            int expected = floorOverride + AppConfig.ScmTimeoutBufferSeconds;

            // Act
            int actual = ServiceHelper.CalculateStopTimeout(null, null, preStopTimeout: 0, floorOverride: floorOverride);

            // Assert
            Assert.Equal(expected, actual);
        }

        #endregion

        #region ResolvePreStopTimeout Tests

        [Fact]
        public void ResolvePreStopTimeout_NullService_ReturnsZero()
        {
            // Arrange
            ServiceDto service = null;

            // Act
            int result = ServiceHelper.ResolvePreStopTimeout(service);

            // Assert
            Assert.Equal(0, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ResolvePreStopTimeout_NullOrEmptyPreStopExecutablePath_ReturnsZero(string path)
        {
            // Arrange
            var service = new ServiceDto
            {
                PreStopExecutablePath = path,
                PreStopTimeoutSeconds = 30
            };

            // Act
            int result = ServiceHelper.ResolvePreStopTimeout(service);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public void ResolvePreStopTimeout_ExecutablePathProvidedAndTimeoutConfigured_ReturnsConfiguredTimeout()
        {
            // Arrange
            var service = new ServiceDto
            {
                PreStopExecutablePath = @"C:\Scripts\pre-stop.bat",
                PreStopTimeoutSeconds = 45
            };

            // Act
            int result = ServiceHelper.ResolvePreStopTimeout(service);

            // Assert
            Assert.Equal(45, result);
        }

        [Fact]
        public void ResolvePreStopTimeout_ExecutablePathProvidedAndTimeoutNull_ReturnsDefaultPreStopTimeout()
        {
            // Arrange
            var service = new ServiceDto
            {
                PreStopExecutablePath = @"C:\Scripts\pre-stop.bat",
                PreStopTimeoutSeconds = null
            };

            // Act
            int result = ServiceHelper.ResolvePreStopTimeout(service);

            // Assert
            Assert.Equal(AppConfig.DefaultPreStopTimeoutSeconds, result);
        }

        #endregion
    }
}

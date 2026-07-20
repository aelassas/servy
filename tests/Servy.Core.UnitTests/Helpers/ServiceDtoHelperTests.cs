using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class ServiceDtoHelperTests
    {
        [Fact]
        public void ApplyDefaults_WhenAllPropertiesAreNull_PopulatesEveryDefault()
        {
            // Arrange: Create a DTO where all nullable properties are null
            // Note: ServiceDto has field initializers, so explicitly null
            // every nullable property to exercise ApplyDefaults on an incomplete import.
            var dto = new ServiceDto
            {
                StartupType = null,
                Priority = null,
                RunAsLocalSystem = null,
                EnableDebugLogs = null,
                StartTimeout = null,
                StopTimeout = null,
                EnableSizeRotation = null,
                RotationSize = null,
                EnableDateRotation = null,
                DateRotationType = null,
                MaxRotations = null,
                UseLocalTimeForRotation = null,
                EnableHealthMonitoring = null,
                HeartbeatInterval = null,
                MaxFailedChecks = null,
                MaxRestartAttempts = null,
                PreLaunchTimeoutSeconds = null,
                PreLaunchRetryAttempts = null,
                PreLaunchIgnoreFailure = null,
                PreStopTimeoutSeconds = null,
                PreStopLogAsError = null,
                EnableConsoleUI = null,
                RecoveryAction = null,
                RecoveryOnCleanExit = null
            };

            // Act
            ServiceDtoHelper.ApplyDefaultsAndResetIdentity(dto);

            // Assert
            Assert.Equal((int)AppConfig.DefaultStartupType, dto.StartupType);
            Assert.Equal((int)AppConfig.DefaultProcessPriority, dto.Priority);
            Assert.Equal(AppConfig.DefaultRunAsLocalSystem, dto.RunAsLocalSystem);
            Assert.Equal(AppConfig.DefaultEnableDebugLogs, dto.EnableDebugLogs);
            Assert.Equal(AppConfig.DefaultStartTimeout, dto.StartTimeout);
            Assert.Equal(AppConfig.DefaultStopTimeout, dto.StopTimeout);
            Assert.Equal(AppConfig.DefaultEnableSizeRotation, dto.EnableSizeRotation);
            Assert.Equal(AppConfig.DefaultRotationSizeMB, dto.RotationSize);
            Assert.Equal(AppConfig.DefaultEnableDateRotation, dto.EnableDateRotation);
            Assert.Equal((int)AppConfig.DefaultDateRotationType, dto.DateRotationType);
            Assert.Equal(AppConfig.DefaultMaxRotations, dto.MaxRotations);
            Assert.Equal(AppConfig.DefaultUseLocalTimeForRotation, dto.UseLocalTimeForRotation);
            Assert.Equal(AppConfig.DefaultEnableHealthMonitoring, dto.EnableHealthMonitoring);
            Assert.Equal(AppConfig.DefaultHeartbeatInterval, dto.HeartbeatInterval);
            Assert.Equal(AppConfig.DefaultMaxFailedChecks, dto.MaxFailedChecks);
            Assert.Equal(AppConfig.DefaultMaxRestartAttempts, dto.MaxRestartAttempts);
            Assert.Equal(AppConfig.DefaultPreLaunchTimeoutSeconds, dto.PreLaunchTimeoutSeconds);
            Assert.Equal(AppConfig.DefaultPreLaunchRetryAttempts, dto.PreLaunchRetryAttempts);
            Assert.Equal(AppConfig.DefaultPreLaunchIgnoreFailure, dto.PreLaunchIgnoreFailure);
            Assert.Equal(AppConfig.DefaultPreStopTimeoutSeconds, dto.PreStopTimeoutSeconds);
            Assert.Equal(AppConfig.DefaultPreStopLogAsError, dto.PreStopLogAsError);
            Assert.Equal(AppConfig.DefaultEnableConsoleUI, dto.EnableConsoleUI);
            Assert.Equal((int)AppConfig.DefaultRecoveryAction, dto.RecoveryAction);
            Assert.Equal(AppConfig.DefaultRecoveryOnCleanExit, dto.RecoveryOnCleanExit);
        }

        [Fact]
        public void ApplyDefaultsAndResetIdentity_WhenPropertiesAlreadyHaveValues_PreservesExplicitNonIdentityValues()
        {
            // Arrange: Assign explicit custom configurations that deviate from system defaults
            const int customTimeout = 999;
            bool customToggle = !AppConfig.DefaultEnableSizeRotation;

            var dto = new ServiceDto
            {
                Name = "TestService",
                StartTimeout = customTimeout,
                EnableSizeRotation = customToggle,
                // Populate arbitrary properties as null to ensure hydration still executes as a sibling track
                StopTimeout = null
            };

            // Act
            ServiceDtoHelper.ApplyDefaultsAndResetIdentity(dto);

            // Assert
            // 1. Verify custom parameter value allocations remain perfectly intact
            Assert.Equal(customTimeout, dto.StartTimeout);
            Assert.Equal(customToggle, dto.EnableSizeRotation);

            // 2. Verify unmatched null variables still pull successfully from base fallback policies
            Assert.Equal(AppConfig.DefaultStopTimeout, dto.StopTimeout);
        }

        [Fact]
        public void ApplyDefaultsAndResetIdentity_WithCustomIdentityPopulated_UnconditionallyResetsToLocalSystemBaseline()
        {
            // Arrange: Populate an explicit custom user account layout configuration
            var dto = new ServiceDto
            {
                Name = "IdentitySecurityService",
                RunAsLocalSystem = false,
                UserAccount = @".\test_svc",
                Password = "secret"
            };

            // Act
            ServiceDtoHelper.ApplyDefaultsAndResetIdentity(dto);

            // Assert
            // Verify that the Global Identity Reset on Import policy strictly overwrites and purges the account context
            Assert.True(dto.RunAsLocalSystem, "The identity was not securely reset to follow the password-less LocalSystem default state.");
            Assert.Null(dto.UserAccount);
            Assert.Null(dto.Password);
        }

        [Fact]
        public void ApplyDefaults_WhenDtoIsNull_ShouldNotThrow()
        {
            // Arrange
            ServiceDto dto = null;

            // Act & Assert
            // ApplyDefaultsAndResetIdentity returns immediately on null (see ServiceDtoHelper)
            var exception = Record.Exception(() => ServiceDtoHelper.ApplyDefaultsAndResetIdentity(dto));
            Assert.Null(exception);
        }
    }
}
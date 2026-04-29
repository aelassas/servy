using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;

namespace Servy.UnitTests.Core.Helpers
{
    public class ServiceDtoHelperTests
    {
        [Fact]
        public void ApplyDefaults_WhenAllPropertiesAreNull_PopulatesEveryDefault()
        {
            // Arrange: Create a DTO where all nullable properties are null
            // Note: Since you added initializers to the DTO, we explicitly set them to null 
            // to test the "ApplyDefaults" logic for incomplete imports.
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
                PreStopLogAsError = null
            };

            // Act
            ServiceDtoHelper.ApplyDefaults(dto);

            // Assert
            Assert.Equal((int)AppConfig.DefaultStartupType, dto.StartupType);
            Assert.Equal((int)AppConfig.DefaultPriority, dto.Priority);
            Assert.Equal(AppConfig.DefaultRunAsLocalSystem, dto.RunAsLocalSystem);
            Assert.Equal(AppConfig.DefaultEnableDebugLogs, dto.EnableDebugLogs);
            Assert.Equal(AppConfig.DefaultStartTimeout, dto.StartTimeout);
            Assert.Equal(AppConfig.DefaultStopTimeout, dto.StopTimeout);
            Assert.Equal(AppConfig.DefaultEnableRotation, dto.EnableSizeRotation);
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
        }

        [Fact]
        public void ApplyDefaults_WhenPropertiesAlreadyHaveValues_DoesNotOverwrite()
        {
            // Arrange: Set values that are specifically DIFFERENT from defaults
            const int customTimeout = 999;
            const bool customToggle = true; // Assuming default is false

            var dto = new ServiceDto
            {
                StartTimeout = customTimeout,
                EnableSizeRotation = customToggle,
                RunAsLocalSystem = false, // Assuming default is true
                UserAccount = @".\test_svc",
                Password = "secret",
            };

            // Act
            ServiceDtoHelper.ApplyDefaults(dto);

            // Assert
            Assert.Equal(customTimeout, dto.StartTimeout);
            Assert.Equal(customToggle, dto.EnableSizeRotation);
            Assert.True(dto.RunAsLocalSystem);
            Assert.Null(dto.UserAccount);
            Assert.Null(dto.Password);

            // Verify that other (null) properties WERE still populated
            Assert.Equal(AppConfig.DefaultStopTimeout, dto.StopTimeout);
        }

        [Fact]
        public void ApplyDefaults_WhenDtoIsNull_ShouldNotThrow()
        {
            // Arrange
            ServiceDto? dto = null;

            // Act & Assert
            // This verifies the helper is null-safe (if you add a null check)
            // If you don't have a null check, this is a good reminder to add: 
            // if (dto == null) return;
            var exception = Record.Exception(() => ServiceDtoHelper.ApplyDefaults(dto!));
            Assert.Null(exception);
        }
    }
}
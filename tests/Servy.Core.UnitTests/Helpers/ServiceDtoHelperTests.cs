using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using System;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class ServiceDtoHelperTests
    {
        [Fact]
        public void Clone_WhenDtoIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            ServiceDto dto = null;

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => ServiceDtoHelper.Clone(dto));
            Assert.Equal("dto", exception.ParamName);
        }

        [Fact]
        public void Clone_CreatesShallowCopyWithoutMutatingOriginal()
        {
            // Arrange
            var original = new ServiceDto
            {
                Name = "OriginalService",
                DisplayName = "Original Display",
                Description = "Original Description",
                ExecutablePath = @"C:\app\service.exe",
                StartupDirectory = @"C:\app",
                Parameters = "-arg1",
                StartupType = 2,
                Priority = 32,
                CpuAffinity = "0x1",
                StartTimeout = 30,
                StopTimeout = 30,
                EnableConsoleUI = true,
                StdoutPath = @"C:\logs\out.log",
                StderrPath = @"C:\logs\err.log",
                EnableSizeRotation = true,
                RotationSize = 10,
                EnableDateRotation = true,
                DateRotationType = 1,
                MaxRotations = 5,
                UseLocalTimeForRotation = true,
                EnableDebugLogs = true,
                EnableHealthMonitoring = true,
                HeartbeatInterval = 60,
                MaxFailedChecks = 3,
                RecoveryAction = 1,
                RecoveryOnCleanExit = false,
                MaxRestartAttempts = 5,
                HeartbeatUrl = "https://example.com/ping",
                HeartbeatUrlTimeoutSeconds = 15,
                EnableHeartbeatUrlFlags = true,
                FailureProgramPath = @"C:\app\fail.exe",
                FailureProgramStartupDirectory = @"C:\app",
                FailureProgramParameters = "--fail",
                EnvironmentVariables = "ENV=1",
                ServiceDependencies = "Dep1",
                RunAsLocalSystem = false,
                UserAccount = "Admin",
                Password = "SecretPassword",
                PreLaunchExecutablePath = @"C:\app\pre.exe",
                PreLaunchStartupDirectory = @"C:\app",
                PreLaunchParameters = "--pre",
                PreLaunchEnvironmentVariables = "PRE=1",
                PreLaunchStdoutPath = @"C:\logs\pre_out.log",
                PreLaunchStderrPath = @"C:\logs\pre_err.log",
                PreLaunchTimeoutSeconds = 10,
                PreLaunchRetryAttempts = 2,
                PreLaunchIgnoreFailure = true,
                PostLaunchExecutablePath = @"C:\app\post.exe",
                PostLaunchStartupDirectory = @"C:\app",
                PostLaunchParameters = "--post",
                PreviousStopTimeout = 20,
                ActiveStdoutPath = @"C:\logs\active_out.log",
                ActiveStderrPath = @"C:\logs\active_err.log",
                PreStopExecutablePath = @"C:\app\prestop.exe",
                PreStopStartupDirectory = @"C:\app",
                PreStopParameters = "--prestop",
                PreStopTimeoutSeconds = 10,
                PreStopLogAsError = true,
                PostStopExecutablePath = @"C:\app\poststop.exe",
                PostStopStartupDirectory = @"C:\app",
                PostStopParameters = "--poststop"
            };

            // Act
            var clone = ServiceDtoHelper.Clone(original);

            // Assert: Verify all properties were copied
            Assert.NotSame(original, clone);
            Assert.Equal(original.Name, clone.Name);
            Assert.Equal(original.DisplayName, clone.DisplayName);
            Assert.Equal(original.Description, clone.Description);
            Assert.Equal(original.ExecutablePath, clone.ExecutablePath);
            Assert.Equal(original.StartupDirectory, clone.StartupDirectory);
            Assert.Equal(original.Parameters, clone.Parameters);
            Assert.Equal(original.StartupType, clone.StartupType);
            Assert.Equal(original.Priority, clone.Priority);
            Assert.Equal(original.CpuAffinity, clone.CpuAffinity);
            Assert.Equal(original.StartTimeout, clone.StartTimeout);
            Assert.Equal(original.StopTimeout, clone.StopTimeout);
            Assert.Equal(original.EnableConsoleUI, clone.EnableConsoleUI);
            Assert.Equal(original.StdoutPath, clone.StdoutPath);
            Assert.Equal(original.StderrPath, clone.StderrPath);
            Assert.Equal(original.EnableSizeRotation, clone.EnableSizeRotation);
            Assert.Equal(original.RotationSize, clone.RotationSize);
            Assert.Equal(original.EnableDateRotation, clone.EnableDateRotation);
            Assert.Equal(original.DateRotationType, clone.DateRotationType);
            Assert.Equal(original.MaxRotations, clone.MaxRotations);
            Assert.Equal(original.UseLocalTimeForRotation, clone.UseLocalTimeForRotation);
            Assert.Equal(original.EnableDebugLogs, clone.EnableDebugLogs);
            Assert.Equal(original.EnableHealthMonitoring, clone.EnableHealthMonitoring);
            Assert.Equal(original.HeartbeatInterval, clone.HeartbeatInterval);
            Assert.Equal(original.MaxFailedChecks, clone.MaxFailedChecks);
            Assert.Equal(original.RecoveryAction, clone.RecoveryAction);
            Assert.Equal(original.RecoveryOnCleanExit, clone.RecoveryOnCleanExit);
            Assert.Equal(original.MaxRestartAttempts, clone.MaxRestartAttempts);
            Assert.Equal(original.HeartbeatUrl, clone.HeartbeatUrl);
            Assert.Equal(original.HeartbeatUrlTimeoutSeconds, clone.HeartbeatUrlTimeoutSeconds);
            Assert.Equal(original.EnableHeartbeatUrlFlags, clone.EnableHeartbeatUrlFlags);
            Assert.Equal(original.FailureProgramPath, clone.FailureProgramPath);
            Assert.Equal(original.FailureProgramStartupDirectory, clone.FailureProgramStartupDirectory);
            Assert.Equal(original.FailureProgramParameters, clone.FailureProgramParameters);
            Assert.Equal(original.EnvironmentVariables, clone.EnvironmentVariables);
            Assert.Equal(original.ServiceDependencies, clone.ServiceDependencies);
            Assert.Equal(original.RunAsLocalSystem, clone.RunAsLocalSystem);
            Assert.Equal(original.UserAccount, clone.UserAccount);
            Assert.Equal(original.Password, clone.Password);
            Assert.Equal(original.PreLaunchExecutablePath, clone.PreLaunchExecutablePath);
            Assert.Equal(original.PreLaunchStartupDirectory, clone.PreLaunchStartupDirectory);
            Assert.Equal(original.PreLaunchParameters, clone.PreLaunchParameters);
            Assert.Equal(original.PreLaunchEnvironmentVariables, clone.PreLaunchEnvironmentVariables);
            Assert.Equal(original.PreLaunchStdoutPath, clone.PreLaunchStdoutPath);
            Assert.Equal(original.PreLaunchStderrPath, clone.PreLaunchStderrPath);
            Assert.Equal(original.PreLaunchTimeoutSeconds, clone.PreLaunchTimeoutSeconds);
            Assert.Equal(original.PreLaunchRetryAttempts, clone.PreLaunchRetryAttempts);
            Assert.Equal(original.PreLaunchIgnoreFailure, clone.PreLaunchIgnoreFailure);
            Assert.Equal(original.PostLaunchExecutablePath, clone.PostLaunchExecutablePath);
            Assert.Equal(original.PostLaunchStartupDirectory, clone.PostLaunchStartupDirectory);
            Assert.Equal(original.PostLaunchParameters, clone.PostLaunchParameters);
            Assert.Equal(original.PreviousStopTimeout, clone.PreviousStopTimeout);
            Assert.Equal(original.ActiveStdoutPath, clone.ActiveStdoutPath);
            Assert.Equal(original.ActiveStderrPath, clone.ActiveStderrPath);
            Assert.Equal(original.PreStopExecutablePath, clone.PreStopExecutablePath);
            Assert.Equal(original.PreStopStartupDirectory, clone.PreStopStartupDirectory);
            Assert.Equal(original.PreStopParameters, clone.PreStopParameters);
            Assert.Equal(original.PreStopTimeoutSeconds, clone.PreStopTimeoutSeconds);
            Assert.Equal(original.PreStopLogAsError, clone.PreStopLogAsError);
            Assert.Equal(original.PostStopExecutablePath, clone.PostStopExecutablePath);
            Assert.Equal(original.PostStopStartupDirectory, clone.PostStopStartupDirectory);
            Assert.Equal(original.PostStopParameters, clone.PostStopParameters);

            // Assert: Verify mutating the clone does not affect the original object
            clone.Name = "MutatedName";
            clone.StartTimeout = 999;
            Assert.Equal("OriginalService", original.Name);
            Assert.Equal(30, original.StartTimeout);
        }

        [Fact]
        public void HydrateDefaults_WhenDtoIsNull_ShouldNotThrow()
        {
            // Arrange
            ServiceDto dto = null;

            // Act & Assert
            var exception = Record.Exception(() => ServiceDtoHelper.HydrateDefaults(dto));
            Assert.Null(exception);
        }

        [Fact]
        public void HydrateDefaults_WhenStructuralPropertiesAreNull_PopulatesDefaultsWithoutResettingIdentity()
        {
            // Arrange: Null every structural property, but give the identity trio explicit values so the no-reset guarantee is observable
            var dto = new ServiceDto
            {
                StartupType = null,
                Priority = null,
                RunAsLocalSystem = false,
                UserAccount = "CustomUser",
                Password = "CustomPassword",
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
                HeartbeatUrlTimeoutSeconds = null,
                EnableHeartbeatUrlFlags = null,
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
            ServiceDtoHelper.HydrateDefaults(dto);

            // Assert: Structural defaults populated
            Assert.Equal((int)AppConfig.DefaultStartupType, dto.StartupType);
            Assert.Equal((int)AppConfig.DefaultProcessPriority, dto.Priority);
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
            Assert.Equal(AppConfig.DefaultHeartbeatUrlTimeoutSeconds, dto.HeartbeatUrlTimeoutSeconds);
            Assert.Equal(AppConfig.DefaultEnableHeartbeatUrlFlags, dto.EnableHeartbeatUrlFlags);

            // Assert: Identity properties remain untouched
            Assert.False(dto.RunAsLocalSystem);
            Assert.Equal("CustomUser", dto.UserAccount);
            Assert.Equal("CustomPassword", dto.Password);
        }

        [Fact]
        public void ApplyDefaultsAndResetIdentity_WhenAllPropertiesAreNull_PopulatesEveryDefault()
        {
            // Arrange: Explicitly null every nullable property defensively to exercise ApplyDefaultsAndResetIdentity on an incomplete import
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
                HeartbeatUrlTimeoutSeconds = null,
                EnableHeartbeatUrlFlags = null,
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
            Assert.Equal(AppConfig.DefaultHeartbeatUrlTimeoutSeconds, dto.HeartbeatUrlTimeoutSeconds);
            Assert.Equal(AppConfig.DefaultEnableHeartbeatUrlFlags, dto.EnableHeartbeatUrlFlags);
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
                EnableSizeRotation = customToggle
            };

            // Precondition: StopTimeout is left unset, so hydration is the only thing that can fill it
            Assert.Null(dto.StopTimeout);

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
        public void ApplyDefaultsAndResetIdentity_WhenDtoIsNull_ShouldNotThrow()
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

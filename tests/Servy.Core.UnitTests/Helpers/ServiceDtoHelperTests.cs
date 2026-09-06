using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using System.Reflection;

namespace Servy.Core.UnitTests.Helpers
{
    public class ServiceDtoHelperTests
    {
        [Fact]
        public void Clone_WhenDtoIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            ServiceDto? dto = null;

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => ServiceDtoHelper.Clone(dto!));
            Assert.Equal("dto", exception.ParamName);
        }

        [Fact]
        public void Clone_ReturnsIndependentCopy()
        {
            // Arrange: CreateFull leaves the [JsonIgnore]/[XmlIgnore] identity fields at their defaults,
            // so set them here - the wrapper has to carry them across too
            var original = ServiceDtoFactory.CreateFull();
            original.Id = 42;
            original.Pid = 4242;

            // Act
            var clone = ServiceDtoHelper.Clone(original);

            // Assert: a distinct instance carrying every property value
            Assert.NotSame(original, clone);
            foreach (var prop in typeof(ServiceDto).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || !prop.CanWrite) continue;
                Assert.Equal(prop.GetValue(original), prop.GetValue(clone));
            }

            // Assert: Verify mutating the clone does not affect the original object
            var originalName = original.Name;
            var originalStartTimeout = original.StartTimeout;

            clone.Name = "MutatedName";
            clone.StartTimeout = 999;

            Assert.Equal(originalName, original.Name);
            Assert.Equal(originalStartTimeout, original.StartTimeout);
        }

        [Fact]
        public void HydrateDefaults_WhenDtoIsNull_ShouldNotThrow()
        {
            // Arrange
            ServiceDto? dto = null;

            // Act & Assert
            var exception = Record.Exception(() => ServiceDtoHelper.HydrateDefaults(dto));
            Assert.Null(exception);
        }

        [Fact]
        public void HydrateDefaults_WhenStructuralPropertiesAreNull_PopulatesDefaultsWithoutResettingIdentity()
        {
            // Arrange: Null every structural property, but give the identity trio explicit values so the no-reset guarantee is observable
            var dto = CreateAllNullDto();
            dto.RunAsLocalSystem = false;
            dto.UserAccount = "CustomUser";
            dto.Password = "CustomPassword";

            // Act
            ServiceDtoHelper.HydrateDefaults(dto);

            // Assert: Structural defaults populated
            AssertAllStructuralDefaults(dto);

            // Assert: Identity properties remain untouched
            Assert.False(dto.RunAsLocalSystem);
            Assert.Equal("CustomUser", dto.UserAccount);
            Assert.Equal("CustomPassword", dto.Password);
        }

        [Fact]
        public void ApplyDefaultsAndResetIdentity_WhenAllPropertiesAreNull_PopulatesEveryDefault()
        {
            // Arrange: Explicitly null every nullable property defensively to exercise ApplyDefaultsAndResetIdentity on an incomplete import
            var dto = CreateAllNullDto();

            // Act
            ServiceDtoHelper.ApplyDefaultsAndResetIdentity(dto);

            // Assert
            AssertAllStructuralDefaults(dto);
            Assert.Equal(AppConfig.DefaultRunAsLocalSystem, dto.RunAsLocalSystem);
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
            ServiceDto? dto = null;

            // Act & Assert
            // ApplyDefaultsAndResetIdentity returns immediately on null (see ServiceDtoHelper)
            var exception = Record.Exception(() => ServiceDtoHelper.ApplyDefaultsAndResetIdentity(dto));
            Assert.Null(exception);
        }

        /// <summary>
        /// Builds the shared null-out fixture both hydration tests arrange, so a new nullable
        /// property is nulled once rather than in two hand-maintained initializers.
        /// </summary>
        private static ServiceDto CreateAllNullDto()
        {
            return new ServiceDto
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
        }

        /// <summary>
        /// Asserts the 25 structural defaults HydrateDefaults populates. ApplyDefaultsAndResetIdentity
        /// delegates to HydrateDefaults for all of them, so both tests share one list and default
        /// number 26 is added in a single place.
        /// </summary>
        private static void AssertAllStructuralDefaults(ServiceDto dto)
        {
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
        }
    }
}

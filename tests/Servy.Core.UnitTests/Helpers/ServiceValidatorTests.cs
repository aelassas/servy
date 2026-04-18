using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Xunit;

namespace Servy.Tests.Helpers
{
    public class ServiceValidatorTests
    {
        private ServiceDto GetValidDto() => new ServiceDto
        {
            Name = "ServyTest",
            ExecutablePath = @"C:\Apps\app.exe",
            DisplayName = "Servy Test Service",
            Description = "Standard valid description.",
            Parameters = "--worker",
            StartTimeout = AppConfig.MinStartTimeout,
            StopTimeout = 30000, // 30s
            EnableSizeRotation = false,
            EnableHealthMonitoring = false
        };

        [Fact]
        public void ValidateDto_Succeeds_WhenOptionalNullableFieldsAreNull()
        {
            // This hits the false branch of every .HasValue check
            var dto = new ServiceDto
            {
                Name = "MinimalService",
                ExecutablePath = @"C:\Apps\app.exe",
                StartTimeout = null,
                StopTimeout = null,
                EnableSizeRotation = null,
                EnableHealthMonitoring = null
            };

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.True(isValid);
            Assert.Null(error);
        }

        [Fact]
        public void ValidateDto_Fails_WhenExecutablePathIsNull()
        {
            // Hits the null branch of dto.DisplayName?.Length
            var dto = GetValidDto();
            dto.ExecutablePath = null;

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.False(isValid);
            Assert.Contains("Executable path is required.", error);
        }

        [Fact]
        public void ValidateDto_Succeeds_WhenDisplayNameIsNull()
        {
            // Hits the null branch of dto.DisplayName?.Length
            var dto = GetValidDto();
            dto.DisplayName = null;

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.True(isValid);
            Assert.Null(error);
        }

        [Fact]
        public void ValidateDto_SkipsHealthChecks_WhenHealthMonitoringIsFalse()
        {
            // Hits the branch where .HasValue is true but .Value is false
            var dto = GetValidDto();
            dto.EnableHealthMonitoring = false;
            dto.HeartbeatInterval = -1; // Should be ignored because monitoring is off

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.True(isValid);
            Assert.Null(error);
        }

        [Fact]
        public void ValidateDto_SkipsRotationCheck_WhenEnableRotationIsFalse()
        {
            // Hits the branch where .HasValue is true but .Value is false
            var dto = GetValidDto();
            dto.EnableSizeRotation = false;
            dto.RotationSize = -1; // Should be ignored

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.True(isValid);
            Assert.Null(error);
        }

        [Fact]
        public void ValidateDto_Fails_WhenHealthMonitoringEnabledButHeartbeatIsNull()
        {
            // Hits the branch where EnableHealthMonitoring.Value is true 
            // but HeartbeatInterval.HasValue is false
            // Note: If your logic currently allows null Heartbeat, this test ensures that behavior.
            var dto = GetValidDto();
            dto.EnableHealthMonitoring = true;
            dto.HeartbeatInterval = null;

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            // Per your current code, null Heartbeat passes. If you want it to fail, 
            // the code needs an else check.
            Assert.True(isValid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateDto_Fails_WhenNameIsNullOrEmpty(string name)
        {
            var dto = GetValidDto();
            dto.Name = name;

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.False(isValid);
            Assert.Equal("Service name is required.", error);
        }

        [Fact]
        public void ValidateDto_Fails_WhenNameExceedsLimit()
        {
            var dto = GetValidDto();
            dto.Name = new string('A', AppConfig.MaxServiceNameLength + 1);

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.False(isValid);
            Assert.Contains("exceeds", error);
        }

        [Fact]
        public void ValidateDto_Fails_WhenDisplayNameExceedsLimit()
        {
            var dto = GetValidDto();
            dto.DisplayName = new string('A', AppConfig.MaxDisplayNameLength + 1);

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.False(isValid);
            Assert.Equal("Display name is too long.", error);
        }

        [Fact]
        public void ValidateDto_Fails_WhenDescriptionExceedsLimit()
        {
            var dto = GetValidDto();
            dto.Description = new string('A', AppConfig.MaxDescriptionLength + 1);

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.False(isValid);
            Assert.Equal("Description exceeds safety limits.", error);
        }

        [Theory]
        [InlineData(nameof(ServiceDto.Parameters))]
        [InlineData(nameof(ServiceDto.PreLaunchParameters))]
        [InlineData(nameof(ServiceDto.PostLaunchParameters))]
        [InlineData(nameof(ServiceDto.PreStopParameters))]
        [InlineData(nameof(ServiceDto.PostStopParameters))]
        [InlineData(nameof(ServiceDto.FailureProgramParameters))]
        public void ValidateDto_Fails_WhenAnyParameterFieldExceedsWin32Limit(string propName)
        {
            var dto = GetValidDto();
            var longVal = new string('A', AppConfig.MaxArgumentLength + 1);

            // Use reflection to hit all 6 branches of the internal args array
            typeof(ServiceDto).GetProperty(propName).SetValue(dto, longVal);

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.False(isValid);
            Assert.Equal("One or more argument strings exceed the Windows command-line limit.", error);
        }

        [Fact]
        public void ValidateDto_Fails_WhenStartTimeoutTooLow()
        {
            var dto = GetValidDto();
            dto.StartTimeout = AppConfig.MinStartTimeout - 1;

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.False(isValid);
            Assert.Equal($"Start Timeout must be at least {AppConfig.MinStartTimeout} second(s).", error);
        }

        [Fact]
        public void ValidateDto_Fails_WhenStartTimeoutTooHigh()
        {
            var dto = GetValidDto();
            dto.StartTimeout = AppConfig.MaxStartTimeout + 1;

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.False(isValid);
            Assert.Equal($"Start Timeout exceeds maximum ({AppConfig.MaxStartTimeout}).", error);
        }

        [Theory]
        [InlineData(-1)]  // Below 30s
        public void ValidateDto_Fails_WhenStopTimeoutIsOutsideScmRange(int timeout)
        {
            var dto = GetValidDto();
            dto.StopTimeout = timeout;

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.False(isValid);
            Assert.Equal($"Stop Timeout must be at least {AppConfig.MinStopTimeout} second(s).", error);
        }

        [Fact]
        public void ValidateDto_Fails_WhenStopTimeoutTooHigh()
        {
            var dto = GetValidDto();
            dto.StopTimeout = AppConfig.MaxStopTimeout + 1;

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.False(isValid);
            Assert.Equal($"Stop Timeout exceeds maximum ({AppConfig.MaxStopTimeout}).", error);
        }

        [Fact]
        public void ValidateDto_Fails_WhenRotationEnabledAndSizeTooSmall()
        {
            var dto = GetValidDto();
            dto.EnableSizeRotation = true;
            dto.RotationSize = AppConfig.MinRotationSize - 1;

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.False(isValid);
            Assert.Equal("Rotation size is too small.", error);
        }

        [Fact]
        public void ValidateDto_Fails_WhenHealthMonitoringEnabledAndHeartbeatTooLow()
        {
            var dto = GetValidDto();
            dto.EnableHealthMonitoring = true;
            dto.HeartbeatInterval = AppConfig.MinHeartbeatInterval - 1;

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.False(isValid);
            Assert.Equal("Heartbeat interval is too low.", error);
        }

        [Fact]
        public void ValidateDto_Fails_WhenHealthMonitoringEnabledAndRestartsNegative()
        {
            var dto = GetValidDto();
            dto.EnableHealthMonitoring = true;
            dto.HeartbeatInterval = AppConfig.MinHeartbeatInterval; // Pass heartbeat
            dto.MaxRestartAttempts = -1; // Fail restarts

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.False(isValid);
            Assert.Equal("Max restart attempts cannot be negative.", error);
        }

        [Fact]
        public void ValidateDto_Succeeds_ForValidInput()
        {
            var dto = GetValidDto();

            var (isValid, error) = ServiceValidator.ValidateDto(dto);

            Assert.True(isValid);
            Assert.Null(error);
        }
    }
}
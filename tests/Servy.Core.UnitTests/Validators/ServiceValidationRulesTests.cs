using Moq;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Resources;
using Servy.Core.Validators;
using System;
using System.IO;
using Xunit;

namespace Servy.Core.UnitTests.Validators
{
    public class ServiceValidationRulesTests
    {
        private readonly Mock<IProcessHelper> _processHelperMock;
        private readonly ServiceValidationRules _sut;

        public ServiceValidationRulesTests()
        {
            _processHelperMock = new Mock<IProcessHelper>();

            // Setup the mock to simulate ValidatePath logic.
            // It returns true for typical valid paths and false for the intentionally bad ones used in these tests.
            _processHelperMock.Setup(p => p.ValidatePath(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns((string path, bool isFile) =>
                {
                    if (string.IsNullOrWhiteSpace(path)) return false;
                    if (path.Contains("invalid") || path.Contains("bad")) return false;
                    return true;
                });

            _sut = new ServiceValidationRules(_processHelperMock.Object);
        }

        #region Helpers

        /// <summary>
        /// Creates a DTO that passes all validation rules.
        /// </summary>
        private ServiceDto CreateValidDto()
        {
            return new ServiceDto
            {
                Name = "ValidService",
                ExecutablePath = "C:\\Windows\\System32\\notepad.exe",
                DisplayName = "Valid Display Name",
                Description = "A valid description",
                StartupDirectory = "C:\\Windows",
                StartTimeout = 30,
                StopTimeout = 30,
                EnableHealthMonitoring = false,
                RunAsLocalSystem = true
            };
        }

        #endregion

        [Fact]
        public void Validate_NullDto_ReturnsError()
        {
            var result = _sut.Validate(null);
            Assert.Contains(Strings.Msg_ValidationError, result.Errors);
        }

        [Theory]
        [InlineData("", "C:\\path.exe")]
        [InlineData("Name", "")]
        [InlineData(null, "C:\\path.exe")]
        public void Validate_MissingVitalFields_ReturnsWarning(string name, string path)
        {
            var dto = new ServiceDto { Name = name, ExecutablePath = path };
            var result = _sut.Validate(dto);
            Assert.Contains(Strings.Msg_ValidationError, result.Errors);
        }

        [Fact]
        public void Validate_ExceedingLengthLimits_ReturnsWarnings()
        {
            var dto = CreateValidDto();
            dto.Name = new string('A', AppConfig.MaxServiceNameLength + 1);
            dto.DisplayName = new string('B', AppConfig.MaxDisplayNameLength + 1);
            dto.Description = new string('C', AppConfig.MaxDescriptionLength + 1);
            dto.Parameters = new string('D', AppConfig.MaxArgumentLength + 1);

            var result = _sut.Validate(dto);

            Assert.Equal(4, result.Warnings.Count);
            Assert.Contains(result.Warnings, w => w.Contains("exceeds"));
        }

        [Fact]
        public void Validate_InvalidPaths_ReturnsErrors()
        {
            var dto = CreateValidDto();
            dto.ExecutablePath = "invalid|path";
            dto.StartupDirectory = "invalid:dir";
            dto.StdoutPath = "invalid>out";

            // Testing non-existent wrapper path
            string nonExistentWrapper = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var result = _sut.Validate(dto, nonExistentWrapper);

            Assert.Contains(Strings.Msg_InvalidPath, result.Errors);
            Assert.Contains(Strings.Msg_InvalidWrapperExePath, result.Errors);
            Assert.Contains(Strings.Msg_InvalidStartupDirectory, result.Errors);
            Assert.Contains(Strings.Msg_InvalidStdoutPath, result.Errors);
        }

        [Fact]
        public void Validate_InvalidTimeoutsAndRotation_ReturnsErrors()
        {
            var dto = CreateValidDto();
            dto.StartTimeout = AppConfig.MinStartTimeout - 1;
            dto.StopTimeout = AppConfig.MaxStopTimeout + 1;
            dto.RotationSize = AppConfig.MinRotationSize - 1;
            dto.MaxRotations = AppConfig.MaxMaxRotations + 1;

            var result = _sut.Validate(dto);

            Assert.Contains(result.Errors, e => e.Contains("Start timeout"));
            Assert.Contains(result.Errors, e => e.Contains("Stop timeout"));
            Assert.Contains(result.Errors, e => e.Contains("Rotation size"));
            Assert.Contains(result.Errors, e => e.Contains("Max rotations"));
        }

        [Fact]
        public void Validate_HealthMonitoringEnabled_InvalidRanges_ReturnsErrors()
        {
            var dto = CreateValidDto();
            dto.EnableHealthMonitoring = true;
            dto.HeartbeatInterval = AppConfig.MinHeartbeatInterval - 1;
            dto.MaxFailedChecks = unchecked(AppConfig.MaxMaxFailedChecks + 1);
            dto.MaxRestartAttempts = -5;

            var result = _sut.Validate(dto);

            Assert.Contains(result.Errors, e => e.Contains("Heartbeat interval"));
            Assert.Contains(result.Errors, e => e.Contains("Max Failed Checks"));
            Assert.Contains(result.Errors, e => e.Contains("Max Restart Attempts"));
        }

        [Fact]
        public void Validate_Credentials_PasswordsMismatch_ReturnsError()
        {
            var dto = CreateValidDto();
            dto.RunAsLocalSystem = false;
            dto.UserAccount = "Admin";
            dto.Password = "Secret123";

            var result = _sut.Validate(dto, confirmPassword: "WrongPassword");

            Assert.Contains(Strings.Msg_PasswordsDontMatch, result.Errors);
        }

        [Fact]
        public void Validate_InvalidEnvVarsAndDependencies_ReturnsErrors()
        {
            var dto = CreateValidDto();
            dto.EnvironmentVariables = "INVALID_VAR"; // Missing '='
            dto.ServiceDependencies = "MissingDep;"; // Ends with semicolon often allowed but let's assume validator catches empty entries

            var result = _sut.Validate(dto);

            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void Validate_PreLaunch_InvalidSettings_ReturnsErrors()
        {
            var dto = CreateValidDto();
            dto.PreLaunchExecutablePath = "invalid|path";
            dto.PreLaunchTimeoutSeconds = AppConfig.MaxPreLaunchTimeoutSeconds + 1;
            dto.PreLaunchRetryAttempts = -1;

            var result = _sut.Validate(dto);

            Assert.Contains(Strings.Msg_InvalidPreLaunchPath, result.Errors);
            Assert.Contains(result.Errors, e => e.Contains("Pre-Launch timeout"));
            Assert.Contains(result.Errors, e => e.Contains("Pre-Launch retry attempts"));
        }

        [Fact]
        public void Validate_Hooks_InvalidPaths_ReturnsErrors()
        {
            var dto = CreateValidDto();
            dto.PostLaunchExecutablePath = "bad|path";
            dto.PreStopExecutablePath = "bad|path";
            dto.PostStopExecutablePath = "bad|path";

            var result = _sut.Validate(dto);

            Assert.Contains(Strings.Msg_InvalidPostLaunchPath, result.Errors);
            Assert.Contains(Strings.Msg_InvalidPreStopPath, result.Errors);
            Assert.Contains(Strings.Msg_InvalidPostStopPath, result.Errors);
        }

        [Fact]
        public void Validate_PerfectDto_ReturnsValid()
        {
            var dto = CreateValidDto();
            var result = _sut.Validate(dto);

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
            Assert.Empty(result.Warnings);
        }
    }
}
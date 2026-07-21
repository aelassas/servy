using Moq;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Resources;
using Servy.Core.UnitTests.Helpers;
using Servy.Core.Validation;

namespace Servy.Core.UnitTests.Validation
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
            _processHelperMock.Setup(p => p.ValidatePath(It.IsAny<string?>(), It.IsAny<bool>()))
                .Returns((string? path, bool isFile) =>
                {
                    if (string.IsNullOrWhiteSpace(path)) return false;
                    if (path.Contains("invalid") || path.Contains("bad")) return false;
                    return true;
                });

            _sut = new ServiceValidationRules(_processHelperMock.Object);
        }

        [Fact]
        public void Validate_NullDto_ReturnsError()
        {
            // Act
            var result = _sut.Validate(null);

            // Assert
            Assert.Contains(Strings.Msg_ValidationError, result.Errors);
        }

        [Theory]
        [InlineData("", "C:\\path.exe", nameof(Strings.Msg_ServiceNameRequired))]
        [InlineData(null, "C:\\path.exe", nameof(Strings.Msg_ServiceNameRequired))]
        [InlineData("Name", "", nameof(Strings.Msg_ExecutablePathRequired))]
        [InlineData("Name", null, nameof(Strings.Msg_ExecutablePathRequired))]
        public void Validate_MissingVitalFields_ReturnsSpecificValidationError(string? name, string? path, string expectedResourceKey)
        {
            // Arrange
            var dto = new ServiceDto { Name = name!, ExecutablePath = path! };

            // Resolve the expected error string dynamically from resources based on the key
            var expectedError = typeof(Strings)
                .GetProperty(expectedResourceKey, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                ?.GetValue(null) as string;

            Assert.NotNull(expectedError);

            // Act
            var result = _sut.Validate(dto);

            // Assert
            Assert.Contains(expectedError, result.Errors);
        }

        [Fact]
        public void Validate_ExceedingDescriptionLength_ReturnsError()
        {
            // Arrange
            var dto = ServiceDtoFactory.CreateValidValidationBase();
            dto.Description = new string('C', AppConfig.MaxDescriptionLength + 1);

            // Act
            var result = _sut.Validate(dto);

            // Assert
            Assert.Single(result.Errors);
            Assert.Contains(string.Format(Strings.Msg_DescriptionLengthReached, AppConfig.MaxDescriptionLength), result.Errors);
        }

        [Fact]
        public void Validate_InvalidPaths_ReturnsErrors()
        {
            // Arrange
            var dto = ServiceDtoFactory.CreateValidValidationBase();
            dto.ExecutablePath = "invalid|path";
            dto.StartupDirectory = "invalid:dir";
            dto.StdoutPath = "invalid>out";
            dto.StderrPath = "invalid>err";

            // Testing non-existent wrapper path
            string nonExistentWrapper = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            _processHelperMock.Setup(h => h.ValidatePath(It.IsAny<string?>(), It.IsAny<bool>())).Returns(false);

            // Act
            var result = _sut.Validate(dto, nonExistentWrapper);

            // Assert
            Assert.Contains(Strings.Msg_InvalidPath, result.Errors);
            Assert.Contains(Strings.Msg_InvalidWrapperExePath, result.Errors);
            Assert.Contains(Strings.Msg_InvalidStartupDirectory, result.Errors);
            Assert.Contains(Strings.Msg_InvalidStdoutPath, result.Errors);
            Assert.Contains(Strings.Msg_InvalidStderrPath, result.Errors);
        }

        [Fact]
        public void Validate_InvalidTimeoutsAndRotation_ReturnsErrors()
        {
            // Arrange
            var dto = ServiceDtoFactory.CreateValidValidationBase();
            dto.StartTimeout = AppConfig.MinStartTimeout - 1;
            dto.StopTimeout = AppConfig.MaxStopTimeout + 1;
            dto.RotationSize = AppConfig.MinRotationSize - 1;
            dto.MaxRotations = AppConfig.MaxMaxRotations + 1;
            dto.HeartbeatUrlTimeoutSeconds = AppConfig.MaxHeartbeatUrlTimeoutSeconds + 1;

            // Act
            var result = _sut.Validate(dto);

            // Assert
            Assert.Contains(string.Format(Strings.Msg_InvalidStartTimeout, AppConfig.MinStartTimeout, AppConfig.MaxStartTimeout), result.Errors);
            Assert.Contains(string.Format(Strings.Msg_InvalidStopTimeout, AppConfig.MinStopTimeout, AppConfig.MaxStopTimeout), result.Errors);
            Assert.Contains(string.Format(Strings.Msg_InvalidRotationSize, AppConfig.MinRotationSize, AppConfig.MaxRotationSize), result.Errors);
            Assert.Contains(string.Format(Strings.Msg_InvalidMaxRotations, AppConfig.MinMaxRotations, AppConfig.MaxMaxRotations), result.Errors);
            Assert.Contains(string.Format(Strings.Msg_InvalidHeartbeatUrlTimeout, AppConfig.MinHeartbeatUrlTimeoutSeconds, AppConfig.MaxHeartbeatUrlTimeoutSeconds), result.Errors);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [InlineData(null)]
        public void Validate_HealthMonitoringRanges_OutOfBounds_ReturnsErrors_RegardlessOfMonitoringFlag(bool? enableMonitoringState)
        {
            // Arrange
            var dto = ServiceDtoFactory.CreateValidValidationBase();

            // UNCONDITIONAL BOUNDS: Explicitly check various flag states to pin down 
            // the invariant range validation contract of the core validation engine.
            dto.EnableHealthMonitoring = enableMonitoringState;

            dto.HeartbeatInterval = AppConfig.MinHeartbeatInterval - 1;
            dto.MaxFailedChecks = AppConfig.MaxMaxFailedChecks + 1;
            dto.MaxRestartAttempts = -5;

            // Act
            var result = _sut.Validate(dto);

            // Assert
            // Render basic unit properties using clean markdown layout rules
            Assert.Contains(string.Format(Strings.Msg_InvalidHeartbeatInterval, AppConfig.MinHeartbeatInterval, AppConfig.MaxHeartbeatInterval), result.Errors);
            Assert.Contains(string.Format(Strings.Msg_InvalidMaxFailedChecks, AppConfig.MinMaxFailedChecks, AppConfig.MaxMaxFailedChecks), result.Errors);
            Assert.Contains(string.Format(Strings.Msg_InvalidMaxRestartAttempts, AppConfig.MinMaxRestartAttempts, AppConfig.MaxMaxRestartAttempts), result.Errors);
        }

        [Fact]
        public void Validate_Credentials_PasswordsMismatch_ReturnsError()
        {
            // Arrange
            var dto = ServiceDtoFactory.CreateValidValidationBase();
            dto.RunAsLocalSystem = false;
            dto.UserAccount = "Admin";
            dto.Password = "Secret123";

            // Act
            var result = _sut.Validate(dto, confirmPassword: "WrongPassword");

            // Assert
            Assert.Contains(Strings.Msg_PasswordsDontMatch, result.Errors);
        }

        [Fact]
        public void Validate_InvalidEnvVarsAndDependencies_ReturnsErrors()
        {
            // Arrange
            var dto = ServiceDtoFactory.CreateValidValidationBase();
            dto.EnvironmentVariables = "INVALID_VAR"; // Missing '=' -> Triggers Strings.Msg_EnvironmentVariableMissingEquals
            dto.ServiceDependencies = "MissingDep?"; // '?' is illegal -> Triggers Strings.Msg_InvalidServiceDependencyName

            // Act
            var result = _sut.Validate(dto);

            // Assert
            // 1. Verify environment variable syntax validation error
            Assert.Contains(Strings.Msg_EnvironmentVariableMissingEquals, result.Errors);

            // 2. Verify service dependency regex character validation error
            Assert.Contains(string.Format(Strings.Msg_InvalidServiceDependencyName, "MissingDep?"), result.Errors);
        }

        [Fact]
        public void Validate_PreLaunch_InvalidSettings_ReturnsErrors()
        {
            // Arrange
            var dto = ServiceDtoFactory.CreateValidValidationBase();
            dto.PreLaunchExecutablePath = "invalid|path";
            dto.PreLaunchTimeoutSeconds = AppConfig.MaxPreLaunchTimeoutSeconds + 1;
            dto.PreLaunchRetryAttempts = -1;
            dto.PreLaunchStdoutPath = "invalid>out";
            dto.PreLaunchStderrPath = "invalid>err";

            // Act
            var result = _sut.Validate(dto);

            // Assert
            Assert.Contains(Strings.Msg_InvalidPreLaunchPath, result.Errors);
            Assert.Contains(string.Format(Strings.Msg_InvalidPreLaunchTimeout, AppConfig.MinPreLaunchTimeoutSeconds, AppConfig.MaxPreLaunchTimeoutSeconds), result.Errors);
            Assert.Contains(string.Format(Strings.Msg_InvalidPreLaunchRetryAttempts, AppConfig.MinPreLaunchRetryAttempts, AppConfig.MaxPreLaunchRetryAttempts), result.Errors);
            Assert.Contains(Strings.Msg_InvalidPreLaunchStdoutPath, result.Errors);
            Assert.Contains(Strings.Msg_InvalidPreLaunchStderrPath, result.Errors);
        }

        [Fact]
        public void Validate_Hooks_InvalidPaths_ReturnsErrors()
        {
            // Arrange
            var dto = ServiceDtoFactory.CreateValidValidationBase();
            dto.FailureProgramPath = "bad|path";
            dto.FailureProgramStartupDirectory = "bad|dir";
            dto.PostLaunchExecutablePath = "bad|path";
            dto.PostLaunchStartupDirectory = "bad|dir";
            dto.PreStopExecutablePath = "bad|path";
            dto.PreStopStartupDirectory = "bad|dir";
            dto.PostStopExecutablePath = "bad|path";
            dto.PostStopStartupDirectory = "bad|dir";

            // Act
            var result = _sut.Validate(dto);

            // Assert
            Assert.Contains(Strings.Msg_InvalidFailureProgramPath, result.Errors);
            Assert.Contains(Strings.Msg_InvalidFailureProgramStartupDirectory, result.Errors);
            Assert.Contains(Strings.Msg_InvalidPostLaunchPath, result.Errors);
            Assert.Contains(Strings.Msg_InvalidPostLaunchStartupDirectory, result.Errors);
            Assert.Contains(Strings.Msg_InvalidPreStopPath, result.Errors);
            Assert.Contains(Strings.Msg_InvalidPreStopStartupDirectory, result.Errors);
            Assert.Contains(Strings.Msg_InvalidPostStopPath, result.Errors);
            Assert.Contains(Strings.Msg_InvalidPostStopStartupDirectory, result.Errors);
        }

        [Theory]
        [InlineData("not-a-url")]
        [InlineData("C:\\local\\path\\instead\\of\\web\\url")]
        [InlineData("ftp://hc-ping.com/your-uuid")]
        [InlineData("mailto:alert@hc-ping.com")]
        [InlineData("http://")]
        public void Validate_InvalidHeartbeatUrlFormat_ReturnsValidationError(string malformedUrl)
        {
            // Arrange
            var dto = ServiceDtoFactory.CreateValidValidationBase();
            dto.HeartbeatUrl = malformedUrl;

            // Act
            var result = _sut.Validate(dto);

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(Strings.Msg_InvalidHeartbeatUrl, result.Errors);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_HeartbeatUrl_NullOrWhitespace_IsConsideredOptionalAndValid(string? omittedUrl)
        {
            // Arrange
            var dto = ServiceDtoFactory.CreateValidValidationBase();
            dto.HeartbeatUrl = omittedUrl;

            // Act
            var result = _sut.Validate(dto);

            // Assert
            Assert.True(result.IsValid);
            Assert.DoesNotContain(Strings.Msg_InvalidHeartbeatUrl, result.Errors);
        }

        [Theory]
        [InlineData("http://hc-ping.com/your-uuid")]
        [InlineData("https://hc-ping.com/your-uuid")]
        [InlineData("https://127.0.0.1:8080/ping")]
        [InlineData("https://localhost/health?service=servy")]
        public void Validate_WellFormedAbsoluteWebHeartbeatUrl_ReturnsValid(string validUrl)
        {
            // Arrange
            var dto = ServiceDtoFactory.CreateValidValidationBase();
            dto.HeartbeatUrl = validUrl;

            // Act
            var result = _sut.Validate(dto);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Validate_PerfectDto_ReturnsValid()
        {
            // Arrange
            var dto = ServiceDtoFactory.CreateValidValidationBase();

            // Act
            var result = _sut.Validate(dto);

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Validate_ImportMode_SkipsCredentialValidation()
        {
            // Arrange
            var dto = ServiceDtoFactory.CreateValidValidationBase();
            dto.RunAsLocalSystem = false;
            dto.UserAccount = "Admin";
            dto.Password = "Secret123";

            // Act
            var result = _sut.Validate(dto, confirmPassword: "WrongPassword", importMode: true);

            // Assert
            // Render basic comparison ratios safely as clean words
            Assert.DoesNotContain(Strings.Msg_PasswordsDontMatch, result.Errors);
        }
    }
}
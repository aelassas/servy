using Moq;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.CLI.Validation;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Validation;
using Servy.Testing;
using System.Reflection;

namespace Servy.CLI.UnitTests.Validation
{
    public class ServiceInstallValidatorTests
    {
        private readonly Mock<IServiceValidationRules> _rulesMock;
        private readonly ServiceInstallValidator _validator;

        public ServiceInstallValidatorTests()
        {
            _rulesMock = new Mock<IServiceValidationRules>();
            _validator = new ServiceInstallValidator(_rulesMock.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_NullRules_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ServiceInstallValidator(null!));
            Assert.Equal("serviceValidationRules", ex.ParamName);
        }

        #endregion

        #region Validate Null Options

        [Fact]
        public void Validate_NullOptions()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => _validator.Validate(null!));
        }

        #endregion

        #region TryMapToDto Parsing Error Short-Circuit Tests

        [Fact]
        public void Validate_InvalidEnum_ReturnsEarlyMappingFailureResult()
        {
            // Arrange
            var opts = CreateValidOptions();
            opts.ServiceStartType = "CorruptEnumValue"; // Triggers MapEnum error branch

            // The full message: option flag, offending value, and the valid-values list MapEnum builds.
            // ServiceStartType carries no [Flags], so the list is comma-separated.
            var expectedList = string.Join(", ", Enum.GetNames(typeof(ServiceStartType)));
            var expectedError = string.Format(Strings.Msg_InvalidEnumValue, "--startupType", "CorruptEnumValue", expectedList);

            // Act
            var result = _validator.Validate(opts);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(expectedError, result.Message);
            _rulesMock.Verify(r => r.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public void Validate_InvalidIntegerFormat_ReturnsEarlyMappingFailureResult()
        {
            // Arrange
            var opts = CreateValidOptions();
            opts.RotationSize = "NotAnInteger"; // Triggers MapInt error branch

            var expectedError = string.Format(Strings.Msg_InvalidIntegerFormat, "--rotationSize", "NotAnInteger");

            // Act
            var result = _validator.Validate(opts);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(expectedError, result.Message);
            _rulesMock.Verify(r => r.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public void Validate_MultipleFailures_ShortCircuitsAtFirstError()
        {
            // Arrange
            var opts = CreateValidOptions();
            opts.ServiceStartType = "CorruptValue1"; // First mapping call
            opts.RotationSize = "CorruptValue2";     // Subsequent mapping call

            // Act
            var result = _validator.Validate(opts);

            // Assert
            Assert.False(result.IsSuccess);
            // Must complain about the first encountered failure and skip computing the next one
            Assert.Contains("--startupType", result.Message);
            Assert.DoesNotContain("--rotationSize", result.Message);
        }

        [Theory]
        [InlineData("RestartService, RestartProcess")]
        [InlineData("RestartService, RestartComputer")]
        public void Validate_CommaSeparatedNonFlagsEnum_ReturnsEarlyMappingFailureResult(string commaSeparatedInput)
        {
            // Arrange
            var opts = CreateValidOptions();
            opts.RecoveryAction = commaSeparatedInput; // Non-[Flags] enum input containing comma

            // Act
            // Verifies that comma-separated values for non-Flags enums are rejected during option mapping
            var result = _validator.Validate(opts);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("--recoveryAction", result.Message);
            _rulesMock.Verify(r => r.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Validate_EmptyOrWhitespaceNullableInputs_AreMappedAsNullWithoutErrors(string? unparsedInput)
        {
            // Arrange
            var opts = CreateValidOptions();
            opts.RotationSize = unparsedInput;
            opts.ProcessPriority = unparsedInput;

            var validationResult = new ValidationResult { };
            _rulesMock.Setup(r => r.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).Returns(validationResult);

            // Act
            var result = _validator.Validate(opts);

            // Assert
            Assert.True(result.IsSuccess);
            _rulesMock.Verify(r => r.Validate(It.Is<ServiceDto>(dto =>
                dto.RotationSize == null &&
                dto.Priority == null),
                null,    // wrapperExePath - the CLI supplies none, so no ValidatePath check is added
                null,    // confirmPassword - the CLI deliberately opts out of the confirm-password check
                false),  // importMode - must stay false or credential validation is skipped
                Times.Once);
        }

        #endregion

        #region Domain Centralized Validation Rule Tests

        [Fact]
        public void Validate_SharedValidationRulesFail_ReturnsPrioritizedFirstIssue()
        {
            // Arrange
            var opts = CreateValidOptions();
            var validationResult = new ValidationResult { };
            validationResult.Errors.Add("First Core Error Rule Violation");
            validationResult.Errors.Add("Second Core Error");
            _rulesMock.Setup(r => r.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).Returns(validationResult);

            // Act
            var result = _validator.Validate(opts);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("First Core Error Rule Violation", result.Message);
        }

        [Fact]
        public void Validate_AllChecksPass_ReturnsOkResult()
        {
            // Arrange
            var opts = CreateValidOptions();
            opts.ServiceName = "ServyEngine";
            opts.EnableRotation = true; // Branches into logic: `opts.EnableSizeRotation || opts.EnableRotation`

            var validationResult = new ValidationResult { };
            _rulesMock.Setup(r => r.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).Returns(validationResult);

            // Act
            var result = _validator.Validate(opts);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Contains("ServyEngine", result.Message);

            _rulesMock.Verify(r => r.Validate(It.Is<ServiceDto>(dto =>
                dto.Name == "ServyEngine" &&
                dto.EnableSizeRotation == true),
                null,    // wrapperExePath - the CLI supplies none, so no ValidatePath check is added
                null,    // confirmPassword - the CLI deliberately opts out of the confirm-password check
                false),  // importMode - must stay false or credential validation is skipped
                Times.Once);
        }

        #endregion

        #region Reflection & Fallback Lookups Tests

        [Fact]
        public void Validate_InvalidEnum_IncludesOptionFlagInMessage()
        {
            // Arrange
            var opts = CreateValidOptions();
            // ServiceStartType is a real [Option("startupType")] property, so GetOptionName
            // resolves it through the attribute path and the message carries the long name.
            // The prop == null fallback is covered separately by
            // GetOptionName_NonExistentPropertyName_ReturnsProvidedStringFallback below.
            opts.ServiceStartType = "Invalid";

            // Act
            var result = _validator.Validate(opts);

            // Assert
            // Verifies the reflection helper checks 'typeof(InstallServiceOptions).GetProperty(propertyName)'
            // and appends option flags properly if present.
            Assert.Contains("--startupType", result.Message);
        }

        [Fact]
        public void GetOptionName_NonExistentPropertyName_ReturnsProvidedStringFallback()
        {
            // Arrange
            // Since GetOptionName is private, we invoke it securely via reflection helper to test
            // the early loop boundary: `if (prop == null) return propertyName;`
            const string propertyName = "GhostPropertyThatDoesNotExist";

            // Act
            var output = TestReflection.InvokeNonPublicStatic(typeof(ServiceInstallValidator), "GetOptionName", propertyName) as string;

            // Assert
            Assert.Equal(propertyName, output);
        }

        [Fact]
        public void MapEnum_FlagsEnum_CombinedValidValue_ReturnsCombinedInt()
        {
            // Arrange
            // No production enum carries [Flags] today, so the bitmask arm of MapEnum is only
            // reachable with a [Flags] type declared here. MapEnum is a private generic method,
            // which TestReflection.InvokeNonPublicStatic cannot call (it would invoke the open
            // generic definition), so the generic method is closed explicitly first.
            var method = typeof(ServiceInstallValidator).GetMethod("MapEnum", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var closed = method.MakeGenericMethod(typeof(TestOnlyFlagsEnum));

            // args[2] is the 'ref string error' parameter; Invoke writes it back into the array.
            var args = new object[3];
            args[0] = "A, B";
            args[1] = "SomeProp";

            // Act
            var result = closed.Invoke(null, args);

            // Assert
            // A (1) | B (2): every bit maps to a declared name, so ToString() stays textual and
            // differs from the underlying "3" - the branch returns the combined value.
            Assert.Equal(3, Assert.IsType<int>(result));
            Assert.Null(args[2]);
        }

        [Fact]
        public void MapEnum_FlagsEnum_UnmappedBits_ReturnsNullWithPipeSeparatedOptions()
        {
            // Arrange
            var method = typeof(ServiceInstallValidator).GetMethod("MapEnum", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            var closed = method.MakeGenericMethod(typeof(TestOnlyFlagsEnum));

            var args = new object[3];
            args[0] = "A, 8"; // bit 8 is neither A nor B
            args[1] = "SomeProp";

            // Act
            var result = closed.Invoke(null, args);

            // Assert
            // ToString() falls back to the raw "9", which equals the underlying value, so the
            // unmapped bit is rejected and the options list is pipe-joined for a [Flags] enum.
            Assert.Null(result);
            Assert.Contains("A | B", (string)args[2]);
        }

        /// <summary>
        /// Test-only [Flags] enumeration. The production enums MapEnum is called with
        /// (ServiceStartType, ProcessPriority, DateRotationType, RecoveryAction) are all
        /// non-[Flags], so nothing else in the repository drives the bitmask arm.
        /// </summary>
        [Flags]
        private enum TestOnlyFlagsEnum
        {
            None = 0,
            A = 1,
            B = 2
        }

        #endregion

        #region Helper Generation Utilities

        private InstallServiceOptions CreateValidOptions()
        {
            return new InstallServiceOptions
            {
                ServiceName = "TestService",
                ServiceDisplayName = "Test Display Name",
                ServiceDescription = "Test Description",
                ProcessPath = @"C:\App\service.exe",
                StartupDirectory = @"C:\App",
                ProcessParameters = "--run",
                ServiceStartType = "Automatic",
                ProcessPriority = "Normal",
                RotationSize = "1048576",
                DateRotationType = "Daily",
                MaxRotations = "5",
                HeartbeatInterval = "30",
                MaxFailedChecks = "3",
                RecoveryAction = "RestartProcess",
                MaxRestartAttempts = "3",
                PreLaunchTimeout = "60",
                PreLaunchRetryAttempts = "2",
                StartTimeout = "15",
                StopTimeout = "15",
                PreStopTimeout = "10"
            };
        }

        #endregion
    }
}

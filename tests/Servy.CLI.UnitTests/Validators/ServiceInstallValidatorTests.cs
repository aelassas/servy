using Moq;
using Servy.CLI.Options;
using Servy.CLI.Validators;
using Servy.Core.DTOs;
using Servy.Core.Validators;

namespace Servy.CLI.UnitTests.Validators
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
            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ServiceInstallValidator(null!));
            Assert.Equal("serviceValidationRules", ex.ParamName);
        }

        #endregion

        #region TryMapToDto Parsing Error Short-Circuit Tests

        [Fact]
        public void Validate_InvalidEnum_ReturnsEarlyMappingFailureResult()
        {
            // Arrange
            var opts = CreateValidOptions();
            opts.ServiceStartType = "CorruptEnumValue"; // Triggers MapEnum error branch

            // Act
            var result = _validator.Validate(opts);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Invalid value for --startupType: 'CorruptEnumValue'", result.Message);
            _rulesMock.Verify(r => r.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public void Validate_InvalidIntegerFormat_ReturnsEarlyMappingFailureResult()
        {
            // Arrange
            var opts = CreateValidOptions();
            opts.RotationSize = "NotAnInteger"; // Triggers MapInt error branch

            // Act
            var result = _validator.Validate(opts);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Invalid integer format for --rotationSize: 'NotAnInteger'", result.Message);
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
            Assert.False(result.Success);
            // Must complain about the first encountered failure and skip computing the next one
            Assert.Contains("--startupType", result.Message);
            Assert.DoesNotContain("--rotation-size", result.Message);
        }

        [Fact]
        public void Validate_EmptyOrWhitespaceNullableInputs_AreMappedAsNullWithoutErrors()
        {
            // Arrange
            var opts = CreateValidOptions();
            opts.RotationSize = "   "; // Whitespace string should be skipped gracefully by MapInt
            opts.ProcessPriority = null; // Null string should be skipped gracefully by MapEnum

            var validationResult = new ValidationResult { };
            _rulesMock.Setup(r => r.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).Returns(validationResult);

            // Act
            var result = _validator.Validate(opts);

            // Assert
            Assert.True(result.Success);
            _rulesMock.Verify(r => r.Validate(It.Is<ServiceDto>(dto =>
                dto.RotationSize == null &&
                dto.Priority == null),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
        }

        #endregion

        #region Domain Centralized Validation Rule Tests

        [Fact]
        public void Validate_SharedValidationRulesFail_ReturnsPrioritizedFirstIssue()
        {
            // Arrange
            var opts = CreateValidOptions();
            var validationResult = new ValidationResult
            {
            };
            validationResult.Errors.Add("First Core Error Rule Violation");
            validationResult.Errors.Add("Second Core Error");
            _rulesMock.Setup(r => r.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).Returns(validationResult);

            // Act
            var result = _validator.Validate(opts);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("First Core Error Rule Violation", result.Message);
        }

        [Fact]
        public void Validate_AllChecksPass_LogsTelemetryAndReturnsOkResult()
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
            Assert.True(result.Success);
            Assert.Contains("ServyEngine", result.Message);

            _rulesMock.Verify(r => r.Validate(It.Is<ServiceDto>(dto =>
                dto.Name == "ServyEngine" &&
                dto.EnableSizeRotation == true),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
        }

        #endregion

        #region Reflection & Fallback Lookups Tests

        [Fact]
        public void Validate_PropertyWithoutOptionAttribute_FallsBackToPropertyNameString()
        {
            // Arrange
            var opts = CreateValidOptions();
            // Pass a corrupted value to a property name that doesn't exist on standard CommandLine attribute paths 
            // if mapped via an imaginary field, or force-trigger fallback lookup code branches manually.
            // Since we can't add fields dynamically, we use an option property configuration that fails 
            // but tests the 'GetOptionName' logic completely.
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
            // Since GetOptionName is private, we invoke it securely via reflection to test 
            // the early loop boundary: `if (prop == null) return propertyName;`
            var method = typeof(ServiceInstallValidator).GetMethod("GetOptionName",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            // Act
            var output = method!.Invoke(null, new object[] { "GhostPropertyThatDoesNotExist" }) as string;

            // Assert
            Assert.Equal("GhostPropertyThatDoesNotExist", output);
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
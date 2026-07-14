using Moq;
using Newtonsoft.Json;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Resources;
using Servy.Core.Services;
using Servy.Core.Validation;

namespace Servy.Core.UnitTests.Services
{
    public class JsonServiceValidatorTests
    {
        private readonly JsonServiceValidator _validator;
        private readonly Mock<IProcessHelper> _processHelperMock;

        public JsonServiceValidatorTests()
        {
            _processHelperMock = new Mock<IProcessHelper>();
            _validator = new JsonServiceValidator(new ServiceValidationRules(_processHelperMock.Object));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void TryValidate_NullOrEmptyJson_ReturnsFalse(string? input)
        {
            // Arrange
            var expectedError = string.Format(Strings.Msg_ImportInputEmptyOrWhitespace, "JSON");

            // Act
            var result = _validator.TryValidate(input, out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(expectedError, error);
        }

        [Fact]
        public void TryValidate_InvalidJsonFormat_ReturnsFalse()
        {
            // Arrange
            // Structural JSON failure
            var invalidJson = "{ 'invalid': 'json' ";
            var expectedPrefix = string.Format(Strings.Msg_ImportInvalidStructure, "JSON", string.Empty).TrimEnd();

            // Act
            var result = _validator.TryValidate(invalidJson, out var error);

            // Assert
            Assert.False(result);
            Assert.StartsWith(expectedPrefix, error);
        }

        [Fact]
        public void TryValidate_ValidJson_ButNullObject_ReturnsFalse()
        {
            // Arrange
            // Valid JSON syntax for a null literal
            var json = "null";
            var expectedError = string.Format(Strings.Msg_ImportEmptyDefinition, "JSON");

            // Act
            var result = _validator.TryValidate(json, out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(expectedError, error);
        }

        [Fact]
        public void TryValidate_DomainValidationFailure_ReturnsFalse()
        {
            // Arrange
            // Testing the shared ServiceValidator.ValidateDto branch via DisplayName length
            var dto = new ServiceDto
            {
                Name = "TestService",
                ExecutablePath = "C:\\path\\to\\exe.exe",
                DisplayName = new string('A', AppConfig.MaxDisplayNameLength + 1)
            };
            var json = JsonConvert.SerializeObject(dto);

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            // Act
            var result = _validator.TryValidate(json, out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(string.Format(Strings.Msg_DisplayNameLengthReached, AppConfig.MaxDisplayNameLength), error);
        }

        [Theory]
        [InlineData(0)]                               // below min
        [InlineData(AppConfig.MaxStopTimeout + 1)]    // above max
        public void TryValidate_InvalidStopTimeout_ReturnsFalse(int invalidTimeout)
        {
            // Arrange
            var dto = new ServiceDto
            {
                Name = "TestService",
                ExecutablePath = "C:\\path\\to\\exe.exe",
                StopTimeout = invalidTimeout
            };
            var json = JsonConvert.SerializeObject(dto);

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            // Act
            var result = _validator.TryValidate(json, out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(string.Format(Strings.Msg_InvalidStopTimeout, AppConfig.MinStopTimeout, AppConfig.MaxStopTimeout), error);
        }

        [Fact]
        public void TryValidate_InvalidExecutablePath_ReturnsFalse()
        {
            // Arrange
            // Triggers the ProcessHelper.ValidatePath branch
            var dto = new ServiceDto
            {
                Name = "TestService",
                ExecutablePath = "C:\\Invalid|Chars\\test.exe"
            };
            var json = JsonConvert.SerializeObject(dto);

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(false);

            // Act
            var result = _validator.TryValidate(json, out var error);

            // Assert
            Assert.False(result);
            Assert.Equal(Strings.Msg_InvalidPath, error);
        }

        [Fact]
        public void TryValidate_ValidServiceDto_ReturnsTrue()
        {
            // Arrange
            var dto = new ServiceDto
            {
                Name = "TestService",
                ExecutablePath = "C:\\Windows\\System32\\calc.exe",
                StopTimeout = 30000 // Valid: 30000 seconds (~8.3h, under the 86400 max)
            };
            var json = JsonConvert.SerializeObject(dto);

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            // Act
            var result = _validator.TryValidate(json, out var error);

            // Assert
            Assert.True(result);
            Assert.Null(error);
        }
    }
}
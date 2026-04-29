using Moq;
using Newtonsoft.Json;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Resources;
using Servy.Core.Services;
using Servy.Core.Validators;

namespace Servy.Core.UnitTests.Services
{
    public class JsonServiceValidatorTests
    {
        private readonly JsonServiceValidator _validator;
        private readonly Mock<IProcessHelper> _processHelperMock;

        public JsonServiceValidatorTests()
        {
            _processHelperMock = new Mock<IProcessHelper>();
            _validator = new JsonServiceValidator(_processHelperMock.Object, new ServiceValidationRules(_processHelperMock.Object));
        }

        [Fact]
        public void TryValidate_NullOrEmptyJson_ReturnsFalse()
        {
            var result = _validator.TryValidate(null, out var error);
            Assert.False(result);
            Assert.Equal("JSON input cannot be null or empty.", error);

            result = _validator.TryValidate("  ", out error);
            Assert.False(result);
            Assert.Equal("JSON input cannot be null or empty.", error);
        }

        [Fact]
        public void TryValidate_InvalidJsonFormat_ReturnsFalse()
        {
            // Structural JSON failure
            var result = _validator.TryValidate("{ 'invalid': 'json' ", out var error);
            Assert.False(result);
            Assert.StartsWith("Invalid JSON structure:", error);
        }

        [Fact]
        public void TryValidate_ValidJson_ButNullObject_ReturnsFalse()
        {
            // Valid JSON syntax for a null literal
            var result = _validator.TryValidate("null", out var error);
            Assert.False(result);
            Assert.Equal("Deserialization resulted in an empty service definition.", error);
        }

        [Fact]
        public void TryValidate_DomainValidationFailure_ReturnsFalse()
        {
            // Testing the shared ServiceValidator.ValidateDto branch via Description length
            var dto = new ServiceDto
            {
                Name = "TestService",
                ExecutablePath = "C:\\path\\to\\exe.exe",
                Description = new string('A', AppConfig.MaxDescriptionLength + 1)
            };
            var json = JsonConvert.SerializeObject(dto);

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            var result = _validator.TryValidate(json, out var error);

            Assert.False(result);
            Assert.Equal(string.Format(Strings.Msg_DescriptionLengthReached, AppConfig.MaxDescriptionLength), error);
        }

        [Theory]
        [InlineData(0)] // Below threshold
        public void TryValidate_InvalidStopTimeout_ReturnsFalse(int invalidTimeout)
        {
            var dto = new ServiceDto
            {
                Name = "TestService",
                ExecutablePath = "C:\\path\\to\\exe.exe",
                StopTimeout = invalidTimeout
            };
            var json = JsonConvert.SerializeObject(dto);

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            var result = _validator.TryValidate(json, out var error);

            Assert.False(result);
            Assert.Equal(string.Format(Strings.Msg_InvalidStopTimeout, AppConfig.MinStopTimeout, AppConfig.MaxStopTimeout), error);
        }

        [Fact]
        public void TryValidate_InvalidExecutablePath_ReturnsFalse()
        {
            // Triggers the ProcessHelper.ValidatePath branch
            var dto = new ServiceDto
            {
                Name = "TestService",
                ExecutablePath = "C:\\Invalid|Chars\\test.exe"
            };
            var json = JsonConvert.SerializeObject(dto);

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(false);

            var result = _validator.TryValidate(json, out var error);

            Assert.False(result);
            Assert.Equal(Strings.Msg_InvalidPath, error);
        }

        [Fact]
        public void TryValidate_ValidServiceDto_ReturnsTrue()
        {
            var dto = new ServiceDto
            {
                Name = "TestService",
                ExecutablePath = "C:\\Windows\\System32\\calc.exe",
                StopTimeout = 30000 // Valid 30s
            };
            var json = JsonConvert.SerializeObject(dto);

            _processHelperMock.Setup(ph => ph.ValidatePath(dto.ExecutablePath, It.IsAny<bool>())).Returns(true);

            var result = _validator.TryValidate(json, out var error);

            Assert.True(result);
            Assert.Null(error);
        }
    }
}
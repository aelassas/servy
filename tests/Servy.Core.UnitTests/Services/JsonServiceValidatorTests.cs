using Newtonsoft.Json;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Services;
using Xunit;

namespace Servy.Core.UnitTests.Services
{
    public class JsonServiceValidatorTests
    {
        [Fact]
        public void TryValidate_NullOrEmptyJson_ReturnsFalse()
        {
            var result = JsonServiceValidator.TryValidate(null, out var error);
            Assert.False(result);
            Assert.Equal("JSON input cannot be null or empty.", error);

            result = JsonServiceValidator.TryValidate("  ", out error);
            Assert.False(result);
            Assert.Equal("JSON input cannot be null or empty.", error);
        }

        [Fact]
        public void TryValidate_InvalidJsonFormat_ReturnsFalse()
        {
            // Structural JSON failure
            var result = JsonServiceValidator.TryValidate("{ 'invalid': 'json' ", out var error);
            Assert.False(result);
            Assert.StartsWith("Invalid JSON structure:", error);
        }

        [Fact]
        public void TryValidate_ValidJson_ButNullObject_ReturnsFalse()
        {
            // Valid JSON syntax for a null literal
            var result = JsonServiceValidator.TryValidate("null", out var error);
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

            var result = JsonServiceValidator.TryValidate(json, out var error);

            Assert.False(result);
            Assert.Equal("Description exceeds safety limits.", error);
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

            var result = JsonServiceValidator.TryValidate(json, out var error);

            Assert.False(result);
            Assert.Equal("Stop Timeout must be between 30s and 1 hour.", error);
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

            var result = JsonServiceValidator.TryValidate(json, out var error);

            Assert.False(result);
            Assert.Equal("The provided executable path is invalid or inaccessible.", error);
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

            var result = JsonServiceValidator.TryValidate(json, out var error);

            Assert.True(result);
            Assert.Null(error);
        }
    }
}
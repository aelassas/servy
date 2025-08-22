using Newtonsoft.Json;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class JsonServiceValidatorTests
    {
        [Fact]
        public void TryValidate_NullJson_ReturnsFalse()
        {
            var result = JsonServiceValidator.TryValidate(null, out var error);

            Assert.False(result);
            Assert.Equal("JSON cannot be null or empty.", error);
        }

        [Fact]
        public void TryValidate_EmptyJson_ReturnsFalse()
        {
            var result = JsonServiceValidator.TryValidate("", out var error);

            Assert.False(result);
            Assert.Equal("JSON cannot be null or empty.", error);
        }

        [Fact]
        public void TryValidate_InvalidJsonFormat_ReturnsFalse()
        {
            var result = JsonServiceValidator.TryValidate("{ invalid json", out var error);

            Assert.False(result);
            Assert.StartsWith("Invalid JSON format:", error);
        }

        [Fact]
        public void TryValidate_ValidJson_ButNullObject_ReturnsFalse()
        {
            // This is valid JSON syntax, but doesn't match ServiceDto structure
            var result = JsonServiceValidator.TryValidate("null", out var error);

            Assert.False(result);
            Assert.Equal("Failed to deserialize JSON to ServiceDto.", error);
        }

        [Fact]
        public void TryValidate_MissingName_ReturnsFalse()
        {
            var dto = new ServiceDto
            {
                ExecutablePath = "C:\\test.exe"
            };
            var json = JsonConvert.SerializeObject(dto);

            var result = JsonServiceValidator.TryValidate(json, out var error);

            Assert.False(result);
            Assert.Equal("Service name is required.", error);
        }

        [Fact]
        public void TryValidate_MissingExecutablePath_ReturnsFalse()
        {
            var dto = new ServiceDto
            {
                Name = "TestService"
            };
            var json = JsonConvert.SerializeObject(dto);

            var result = JsonServiceValidator.TryValidate(json, out var error);

            Assert.False(result);
            Assert.Equal("Executable path is required.", error);
        }

        [Fact]
        public void TryValidate_ValidServiceDto_ReturnsTrue()
        {
            var dto = new ServiceDto
            {
                Name = "TestService",
                ExecutablePath = "C:\\test.exe"
            };
            var json = JsonConvert.SerializeObject(dto);

            var result = JsonServiceValidator.TryValidate(json, out var error);

            Assert.True(result);
            Assert.Null(error);
        }
    }
}

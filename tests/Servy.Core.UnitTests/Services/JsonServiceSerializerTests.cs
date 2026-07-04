using Newtonsoft.Json;
using Servy.Core.Config;
using Servy.Core.Services;
using Servy.Core.UnitTests.Helpers;

namespace Servy.Core.UnitTests.Services
{
    public class JsonServiceSerializerTests
    {
        private readonly JsonServiceSerializer _serializer = new JsonServiceSerializer();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Deserialize_NullOrWhitespace_ReturnsNull(string? input)
        {
            // Act
            var result = _serializer.Deserialize(input);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Deserialize_JsonNullLiteral_ReturnsNull()
        {
            // Act
            // "null" is a valid JSON string that deserializes to a null object
            var result = _serializer.Deserialize("null");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Deserialize_PartialJson_AppliesDefaults()
        {
            // Arrange
            string json = "{\"Name\": \"PartialService\"}";

            // Act
            var result = _serializer.Deserialize(json);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("PartialService", result.Name);
            // Verify hydration from ServiceDtoHelper.ApplyDefaults
            Assert.Equal(AppConfig.DefaultStartTimeout, result.StartTimeout);
            Assert.Equal(AppConfig.DefaultRunAsLocalSystem, result.RunAsLocalSystem);
        }

        [Fact]
        public void Deserialize_AllFields_MapsCorrectly()
        {
            // Arrange: Create a DTO with specific non-default values for every field
            var expected = ServiceDtoFactory.CreateFull();

            string json = JsonConvert.SerializeObject(expected);

            // Act
            var actual = _serializer.Deserialize(json);

            // Assert
            Assert.NotNull(actual);

            // Validate mapping for key categories
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.ExecutablePath, actual.ExecutablePath);
            Assert.Equal(expected.StartupType, actual.StartupType);
            Assert.Equal(expected.RotationSize, actual.RotationSize);
            Assert.Equal(expected.RecoveryAction, actual.RecoveryAction);
            Assert.Equal(expected.PreLaunchTimeoutSeconds, actual.PreLaunchTimeoutSeconds);
            Assert.Equal(expected.PreStopLogAsError, actual.PreStopLogAsError);
            Assert.Equal(expected.PostStopParameters, actual.PostStopParameters);

            // Check that the Password/Account (Sensitive data) handled by UntrustedDataSettings
            Assert.Null(actual.UserAccount);
            Assert.Null(actual.Password);
            Assert.True(actual.RunAsLocalSystem);
        }

        [Fact]
        public void Deserialize_InvalidJson_ReturnsNull()
        {
            // Arrange
            string invalidJson = "{ \"Name\": \"BadJson\" "; // Missing closing brace

            // Act & Assert
            Assert.Null(_serializer.Deserialize(invalidJson));
        }
    }
}
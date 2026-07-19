using Newtonsoft.Json;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Services;
using Servy.Core.UnitTests.Helpers;
using Servy.Testing;
using Xunit;

namespace Servy.Core.UnitTests.Services
{
    public class JsonServiceSerializerTests
    {
        private readonly JsonServiceSerializer _serializer = new JsonServiceSerializer();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Deserialize_NullOrWhitespace_ReturnsNull(string input)
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
            Assert.Equal(AppConfig.DefaultStopTimeout, result.StopTimeout);
            Assert.Equal(AppConfig.DefaultRunAsLocalSystem, result.RunAsLocalSystem);
        }

        [Fact]
        public void Deserialize_EmptyObject_ReturnsHydratedDto()
        {
            // Arrange
            // Test empty JSON structural hydration to mirror the XML twin's '<ServiceDto />' fallback test.
            // This ensures default values are consistently populated even when zero explicitly configured fields exist.
            string json = "{}";

            // Act
            var result = _serializer.Deserialize(json);

            // Assert
            Assert.NotNull(result);

            // Document baseline safe platform properties enforced by ServiceDtoHelper.ApplyDefaults
            Assert.Equal(AppConfig.DefaultStartTimeout, result.StartTimeout);
            Assert.Equal(AppConfig.DefaultStopTimeout, result.StopTimeout);
            Assert.Equal(AppConfig.DefaultRunAsLocalSystem, result.RunAsLocalSystem);

            // Confirm missing identity and description properties fall back cleanly to empty or system null values
            Assert.True(string.IsNullOrEmpty(result.Name));
            Assert.Null(result.Description);
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

            // Reflection-driven comprehensive comparison of all properties mapped across serialization boundaries
            var excludedProperties = new[] { "Id", "Pid", "UserAccount", "Password", "RunAsLocalSystem", "PreviousStopTimeout", "ActiveStdoutPath", "ActiveStderrPath" };
            var properties = TestReflection.GetMappedProperties<ServiceDto>(excludedProperties);

            foreach (var prop in properties)
            {
                var expectedValue = prop.GetValue(expected);
                var actualValue = prop.GetValue(actual);

                // If the expected fixture value is null, it skips evaluation to prevent 
                // breaking on values hydrated to system defaults during deserialization.
                if (expectedValue == null)
                {
                    continue;
                }

                Assert.Equal(expectedValue, actualValue);
            }

            // Assert baseline defaults here. Due to [JsonIgnore] decorations, 
            // these properties never hit the serialized string payload loop during SerializeObject passes.
            Assert.Null(actual.UserAccount);
            Assert.Null(actual.Password);
            Assert.True(actual.RunAsLocalSystem);
        }

        [Fact]
        public void Deserialize_HostileJsonPayloadWithCredentials_IgnoresSensitiveFields()
        {
            // Arrange
            // Craft an untrusted raw JSON payload explicitly injecting credential parameters
            // to verify that the deserializer actively shields the core object lifecycle.
            string maliciousJson = @"
            {
                ""Name"": ""MaliciousService"",
                ""ExecutablePath"": ""C:\\malicious.exe"",
                ""UserAccount"": ""TargetDomain\\Administrator"",
                ""Password"": ""RoguePassword123!"",
                ""RunAsLocalSystem"": false
            }";

            // Act
            var actual = _serializer.Deserialize(maliciousJson);

            // Assert
            Assert.NotNull(actual);
            Assert.Equal("MaliciousService", actual.Name);
            Assert.Equal("C:\\malicious.exe", actual.ExecutablePath);

            // SECURITY CONTRACT BOUNDARY: Verify that incoming credential vectors are completely ignored,
            // preserving safe system defaults regardless of raw JSON stream content.
            Assert.Null(actual.UserAccount);
            Assert.Null(actual.Password);
            Assert.True(actual.RunAsLocalSystem, "RunAsLocalSystem must fall back to its safe system default (true).");
        }

        [Fact]
        public void Deserialize_InvalidJson_ReturnsNull()
        {
            // Arrange
            string invalidJson = "{ \"Name\": \"BadJson\" "; // Missing closing brace

            // Act & Assert
            Assert.Null(_serializer.Deserialize(invalidJson));
        }

        #region Serialize Tests

        [Fact]
        public void Serialize_NullDto_ReturnsNull()
        {
            // Arrange & Act
            var result = _serializer.Serialize(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Serialize_PopulatedDto_ProducesIndentedJsonExcludingSensitiveFields()
        {
            // Arrange
            var dto = ServiceDtoFactory.CreateFull();
            dto.UserAccount = "Domain\\Admin";
            dto.Password = "SuperSecret123!";
            dto.RunAsLocalSystem = false;

            // Act
            string json = _serializer.Serialize(dto);

            // Assert
            Assert.NotNull(json);

            // Verify that the formatting is human-readable indented JSON
            Assert.Contains("\n", json);
            Assert.Contains("  \"Name\":", json);

            // SECURITY CONTRACT BOUNDARY: Verify that sensitive credentials are actively 
            // stripped or omitted from the outbound file payload by UntrustedDataSettings rules.
            Assert.DoesNotContain("UserAccount", json);
            Assert.DoesNotContain("Password", json);
            Assert.DoesNotContain("Domain\\Admin", json);
            Assert.DoesNotContain("SuperSecret123!", json);
        }

        [Fact]
        public void SerializeAndDeserialize_RoundTripSymmetry_MaintainsObjectIntegrity()
        {
            // Arrange
            var original = ServiceDtoFactory.CreateFull();
            original.Name = "SymmetryRoundTrip";
            original.ExecutablePath = "C:\\Servy\\App.exe";

            // Act
            string serializedJson = _serializer.Serialize(original);
            Assert.NotNull(serializedJson);

            var recovered = _serializer.Deserialize(serializedJson);

            // Assert
            Assert.NotNull(recovered);
            Assert.Equal(original.Name, recovered.Name);
            Assert.Equal(original.ExecutablePath, recovered.ExecutablePath);
            Assert.Equal(original.StartupType, recovered.StartupType);
            Assert.Equal(original.RotationSize, recovered.RotationSize);

            // Confirm structural integrity for unmapped fallback security fields
            Assert.Null(recovered.UserAccount);
            Assert.Null(recovered.Password);
            Assert.True(recovered.RunAsLocalSystem);
        }

        #endregion
    }
}
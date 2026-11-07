using Newtonsoft.Json;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Services;
using Servy.Core.UnitTests.Helpers;
using Servy.Core.Security;
using Servy.Testing;
using System;
using System.Linq;
using Xunit;

namespace Servy.Core.UnitTests.Services
{
    public class JsonServiceSerializerTests
    {
        private readonly JsonServiceSerializer _serializer = new JsonServiceSerializer();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
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

            // Verify hydration from ServiceDtoHelper.ApplyDefaultsAndResetIdentity
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

            // Document baseline safe platform properties enforced by ServiceDtoHelper.ApplyDefaultsAndResetIdentity
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
            ServiceDtoRoundTrip.AssertPropertiesSurvived(expected, actual);

            // Assert baseline defaults here. Password and RunAsLocalSystem carry [JsonIgnore], and the
            // fixture's UserAccount needs a password, so none of them reaches the payload.
            Assert.Null(actual.UserAccount);
            Assert.Null(actual.Password);
            Assert.Equal(AppConfig.DefaultRunAsLocalSystem, actual.RunAsLocalSystem);
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
            // SECURITY CONTRACT BOUNDARY: Verify that incoming credential vectors are ignored: the password
            // is never read, and an account that needs one is reset to the safe system defaults.
            Assert.NotNull(actual);
            Assert.Equal("MaliciousService", actual.Name);
            Assert.Equal("C:\\malicious.exe", actual.ExecutablePath);

            Assert.Null(actual.UserAccount);
            Assert.Null(actual.Password);
            // Identity is reset to the configured default, never taken from the payload.
            Assert.Equal(AppConfig.DefaultRunAsLocalSystem, actual.RunAsLocalSystem);
        }

        [Fact]
        public void Deserialize_InvalidJson_ReturnsNull()
        {
            // Arrange
            string invalidJson = "{ \"Name\": \"BadJson\" "; // Missing closing brace

            // Act & Assert
            Assert.Null(_serializer.Deserialize(invalidJson));
        }

        #region FormatLineInfo Tests

        [Fact]
        public void FormatLineInfo_JsonReaderException_ReportsLineAndPosition()
        {
            // Arrange
            // A reader-level failure carries real line/position metadata, the first switch case.
            var ex = Assert.Throws<JsonReaderException>(() =>
                JsonConvert.DeserializeObject<ServiceDto>("{\n  \"StopTimeout\": 12x\n}", JsonSecurity.UntrustedDataSettings));

            // Act
            var info = (string)TestReflection.InvokeNonPublic(_serializer, "FormatLineInfo", ex);

            // Assert
            Assert.Equal($" at line {ex.LineNumber}, position {ex.LinePosition}", info);
            Assert.NotEqual(string.Empty, info);
        }

        [Fact]
        public void FormatLineInfo_JsonSerializationException_ReportsLineAndPosition()
        {
            // Arrange
            // MissingMemberHandling.Error in UntrustedDataSettings turns an unknown property into a
            // JsonSerializationException, the switch's second case. It does not derive from
            // JsonReaderException, so the pre-#4459 IJsonLineInfo cast reported nothing for it.
            var ex = Assert.Throws<JsonSerializationException>(() =>
                JsonConvert.DeserializeObject<ServiceDto>(
                    "{\n  \"NoSuchProperty\": 1\n}", JsonSecurity.UntrustedDataSettings));

            // Act
            var info = (string)TestReflection.InvokeNonPublic(_serializer, "FormatLineInfo", ex);

            // Assert
            Assert.Equal($" at line {ex.LineNumber}, position {ex.LinePosition}", info);
            Assert.NotEqual(string.Empty, info);
        }

        [Fact]
        public void FormatLineInfo_ExceptionWithoutPositionMetadata_ReturnsEmpty()
        {
            // Arrange & Act
            // Pins the default branch and the lineNumber/linePosition > 0 gate, so that no
            // " at line 0, position 0" suffix can reach the log.
            var info = (string)TestReflection.InvokeNonPublic(
                _serializer, "FormatLineInfo", new InvalidOperationException("no position"));

            // Assert
            Assert.Equal(string.Empty, info);
        }

        #endregion

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

            // SECURITY CONTRACT BOUNDARY: Verify that sensitive credentials are actively omitted from the
            // outbound file payload: Password carries [JsonIgnore], and an account that needs a password is
            // never written (ServiceDto.ShouldSerializeUserAccount).
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

            ServiceDtoRoundTrip.AssertPropertiesSurvived(original, recovered);

            // Confirm structural integrity for unmapped fallback security fields (the fixture's account needs a
            // password, so it is not written and the identity comes back as the LocalSystem baseline)
            Assert.Null(recovered.UserAccount);
            Assert.Null(recovered.Password);
            Assert.Equal(AppConfig.DefaultRunAsLocalSystem, recovered.RunAsLocalSystem);
        }

        [Theory]
        [InlineData(@"NT SERVICE\MyService")]
        [InlineData(@"NT AUTHORITY\NetworkService")]
        [InlineData(@"DOMAIN\svc_gmsa$")]
        public void Deserialize_PasswordlessUserAccount_KeepsTheAccountAndNeverReadsThePassword(string account)
        {
            // Arrange
            var json = JsonConvert.SerializeObject(new
            {
                Name = "PasswordlessService",
                ExecutablePath = "C:\\app.exe",
                UserAccount = account,
                Password = "RoguePassword123!",
                RunAsLocalSystem = true
            });

            // Act
            var actual = _serializer.Deserialize(json);

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(account, actual.UserAccount);
            Assert.False(actual.RunAsLocalSystem);
            Assert.Null(actual.Password);
        }

        [Fact]
        public void Serialize_PasswordlessUserAccount_WritesTheAccountButNeverThePassword()
        {
            // Arrange
            var dto = ServiceDtoFactory.CreateFull();
            dto.UserAccount = @"NT SERVICE\MyService";
            dto.Password = string.Empty;
            dto.RunAsLocalSystem = false;

            // Act
            string json = _serializer.Serialize(dto);

            // Assert
            Assert.NotNull(json);
            Assert.Contains("\"UserAccount\": \"NT SERVICE\\\\MyService\"", json);
            Assert.DoesNotContain("Password", json);
            Assert.DoesNotContain("RunAsLocalSystem", json);
        }

        [Fact]
        public void SerializeAndDeserialize_PasswordlessUserAccount_SurvivesTheRoundTrip()
        {
            // Arrange
            var original = ServiceDtoFactory.CreateFull();
            original.UserAccount = @"NT SERVICE\MyService";
            original.Password = null;
            original.RunAsLocalSystem = false;

            // Act
            var recovered = _serializer.Deserialize(_serializer.Serialize(original));

            // Assert
            Assert.NotNull(recovered);
            Assert.Equal(@"NT SERVICE\MyService", recovered.UserAccount);
            Assert.False(recovered.RunAsLocalSystem);
            Assert.Null(recovered.Password);
        }

        #endregion
    }
}

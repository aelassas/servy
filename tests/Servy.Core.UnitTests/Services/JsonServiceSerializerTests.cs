using System;
using Newtonsoft.Json;
using Xunit;
using Servy.Core.DTOs;
using Servy.Core.Services;
using Servy.Core.Config;

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
            var expected = new ServiceDto
            {
                Name = "FullService",
                DisplayName = "Full Service Display",
                Description = "Description",
                ExecutablePath = @"C:\App\exe.exe",
                StartupDirectory = @"C:\App",
                Parameters = "--arg",
                StartupType = 2,
                Priority = 128,
                StdoutPath = "out.log",
                StderrPath = "err.log",
                EnableRotation = true,
                RotationSize = 50,
                EnableDateRotation = true,
                DateRotationType = 1,
                MaxRotations = 10,
                UseLocalTimeForRotation = true,
                EnableHealthMonitoring = true,
                HeartbeatInterval = 60,
                MaxFailedChecks = 5,
                RecoveryAction = 1,
                MaxRestartAttempts = 10,
                FailureProgramPath = "fail.exe",
                FailureProgramStartupDirectory = "fail_dir",
                FailureProgramParameters = "fail_args",
                EnvironmentVariables = "VAR=1",
                ServiceDependencies = "s1;s2",
                RunAsLocalSystem = false,
                UserAccount = "User",
                Password = "Password",
                PreLaunchExecutablePath = "pre.exe",
                PreLaunchStartupDirectory = "pre_dir",
                PreLaunchParameters = "pre_args",
                PreLaunchEnvironmentVariables = "PVAR=1",
                PreLaunchStdoutPath = "pre_out.log",
                PreLaunchStderrPath = "pre_err.log",
                PreLaunchTimeoutSeconds = 45,
                PreLaunchRetryAttempts = 2,
                PreLaunchIgnoreFailure = true,
                PostLaunchExecutablePath = "post.exe",
                PostLaunchStartupDirectory = "post_dir",
                PostLaunchParameters = "post_args",
                EnableDebugLogs = true,
                StartTimeout = 20,
                StopTimeout = 25,
                PreStopExecutablePath = "pre_stop.exe",
                PreStopStartupDirectory = "pre_stop_dir",
                PreStopParameters = "pre_stop_args",
                PreStopTimeoutSeconds = 15,
                PreStopLogAsError = true,
                PostStopExecutablePath = "post_stop.exe",
                PostStopStartupDirectory = "post_stop_dir",
                PostStopParameters = "post_stop_args"
            };

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
            Assert.Equal(expected.UserAccount, actual.UserAccount);
            Assert.Null(actual.Password);
        }

        [Fact]
        public void Deserialize_InvalidJson_ThrowsJsonException()
        {
            // Arrange
            string invalidJson = "{ \"Name\": \"BadJson\" "; // Missing closing brace

            // Act & Assert
            Assert.ThrowsAny<JsonException>(() => _serializer.Deserialize(invalidJson));
        }
    }
}
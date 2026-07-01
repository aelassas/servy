using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Core.Services;
using Xunit;

namespace Servy.Core.UnitTests.Services
{
    public class XmlServiceSerializerTests
    {
        private readonly XmlServiceSerializer _serializer = new XmlServiceSerializer();

        #region Deserialize Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Deserialize_NullOrWhitespace_ReturnsNull(string input)
        {
            // Arrange & Act
            var result = _serializer.Deserialize(input);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Deserialize_PartialXml_AppliesDefaults()
        {
            // Arrange: Minimal valid XML
            string xml = "<ServiceDto><Name>PartialXmlService</Name></ServiceDto>";

            // Act
            var result = _serializer.Deserialize(xml);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("PartialXmlService", result.Name);

            // Integration check: Verify hydration via ServiceDtoHelper.ApplyDefaults
            Assert.Equal(AppConfig.DefaultStartTimeout, result.StartTimeout);
            Assert.Equal(AppConfig.DefaultStopTimeout, result.StopTimeout);
            Assert.Equal(AppConfig.DefaultRunAsLocalSystem, result.RunAsLocalSystem);
        }

        [Fact]
        public void Deserialize_AllFields_MapsCorrectly()
        {
            // Arrange: Create a DTO with specific values for every single field
            var expected = new ServiceDto
            {
                Name = "FullXmlService",
                DisplayName = "Full Display",
                Description = "Xml Description",
                ExecutablePath = @"C:\App\bin\service.exe",
                StartupDirectory = @"C:\App\bin",
                Parameters = "/start --verbose",
                StartupType = 2,
                Priority = 32,
                StdoutPath = "C:\\logs\\out.log",
                StderrPath = "C:\\logs\\err.log",
                EnableSizeRotation = true,
                RotationSize = 25,
                EnableDateRotation = true,
                DateRotationType = 2,
                MaxRotations = 5,
                UseLocalTimeForRotation = true,
                EnableHealthMonitoring = true,
                HeartbeatInterval = 45,
                MaxFailedChecks = 10,
                RecoveryAction = 1,
                MaxRestartAttempts = 5,
                FailureProgramPath = "reboot.exe",
                FailureProgramStartupDirectory = "C:\\",
                FailureProgramParameters = "-f",
                EnvironmentVariables = "PORT=8080;NODE_ENV=prod",
                ServiceDependencies = "LanmanWorkstation;W32Time",
                RunAsLocalSystem = false,
                UserAccount = "DOMAIN\\ServiceAccount",
                Password = "EncryptedPasswordString",
                PreLaunchExecutablePath = "setup.exe",
                PreLaunchStartupDirectory = "C:\\Temp",
                PreLaunchParameters = "--quiet",
                PreLaunchEnvironmentVariables = "SETUP=1",
                PreLaunchStdoutPath = "setup_out.log",
                PreLaunchStderrPath = "setup_err.log",
                PreLaunchTimeoutSeconds = 120,
                PreLaunchRetryAttempts = 3,
                PreLaunchIgnoreFailure = true,
                PostLaunchExecutablePath = "notify.exe",
                PostLaunchStartupDirectory = "C:\\",
                PostLaunchParameters = "--started",
                EnableDebugLogs = true,
                StartTimeout = 45,
                StopTimeout = 60,
                PreStopExecutablePath = "cleanup.exe",
                PreStopStartupDirectory = "C:\\App",
                PreStopParameters = "--force",
                PreStopTimeoutSeconds = 30,
                PreStopLogAsError = true,
                PostStopExecutablePath = "final.exe",
                PostStopStartupDirectory = "C:\\",
                PostStopParameters = "--done"
            };

            // Convert to XML string using the standard Serializer
            var xml = ServiceDtoXml.Serialize(expected);

            // Act
            var actual = _serializer.Deserialize(xml);

            // Assert
            Assert.NotNull(actual);

            // Validate all major categories
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.ExecutablePath, actual.ExecutablePath);
            Assert.Equal(expected.StartupType, actual.StartupType);
            Assert.Equal(expected.RotationSize, actual.RotationSize);
            Assert.Equal(expected.MaxFailedChecks, actual.MaxFailedChecks);
            Assert.Equal(expected.RecoveryAction, actual.RecoveryAction);
            Assert.Equal(expected.PreLaunchTimeoutSeconds, actual.PreLaunchTimeoutSeconds);
            Assert.Equal(expected.PreStopLogAsError, actual.PreStopLogAsError);
            Assert.Equal(expected.PostStopParameters, actual.PostStopParameters);
            Assert.Equal(expected.EnvironmentVariables, actual.EnvironmentVariables);
            Assert.Equal(expected.ServiceDependencies, actual.ServiceDependencies);

            // Check that the Password/Account (Sensitive data) handled by UntrustedDataSettings
            Assert.Null(actual.UserAccount);
            Assert.Null(actual.Password);
            Assert.True(actual.RunAsLocalSystem);
        }

        [Fact]
        public void Deserialize_MalformedXml_ReturnsNull()
        {
            // Arrange: Invalid XML structure
            string malformedXml = "<ServiceDto><Name>UnclosedTag";

            // Act & Assert
            Assert.Null(_serializer.Deserialize(malformedXml));
        }

        [Fact]
        public void Deserialize_EmptyRoot_ReturnsHydratedDto()
        {
            // Arrange: Valid XML structure but NO properties set
            string emptyXml = "<ServiceDto />";

            // Act
            var result = _serializer.Deserialize(emptyXml);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(AppConfig.DefaultStopTimeout, result.StopTimeout);
            Assert.Equal(AppConfig.DefaultRotationSizeMB, result.RotationSize);
            Assert.Equal((int)AppConfig.DefaultStartupType, result.StartupType);
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
        public void Serialize_ValidDto_ReturnsFormattedXmlWithCorrectPreamble()
        {
            // Arrange
            var dto = new ServiceDto
            {
                Name = "XmlSerializationService",
                DisplayName = "Friendly Name",
                StartTimeout = 30
            };

            // Act
            var xmlResult = _serializer.Serialize(dto);

            // Assert
            Assert.NotNull(xmlResult);

            // Check that it contains indented properties and tags
            Assert.Contains("<Name>XmlSerializationService</Name>", xmlResult);
            Assert.Contains("<StartTimeout>30</StartTimeout>", xmlResult);

            // Verify Utf8StringWriter integration: ensures encoding reflects lowercase 'utf-8' without BOM corruptions
            Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", xmlResult);
        }

        [Fact]
        public void Serialize_InvalidDtoStateOrSerializationFailure_CatchesExceptionAndReturnsNull()
        {
            // Arrange
            // Passing an undeclared derived type through an XmlSerializer instantiated for the base type
            // natively forces an InvalidOperationException, exercising the internal try-catch fallback block.
            var invalidDto = new InvalidServiceDtoMock();

            // Act
            var result = _serializer.Serialize(invalidDto);

            // Assert
            Assert.Null(result);
        }

        #endregion
    }

    /// <summary>
    /// Derived class designed to simulate an unexpected serialization type.
    /// Serializing this runtime subtype through a standard base XmlSerializer(typeof(ServiceDto)) 
    /// throws an InvalidOperationException because it lacks explicit XmlInclude configuration declarations.
    /// </summary>
    public class InvalidServiceDtoMock : ServiceDto
    {
    }
}
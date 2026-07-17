using Servy.Core.DTOs;
using Servy.Core.Services;
using Servy.Core.UnitTests.Helpers;

namespace Servy.Core.UnitTests.Services
{
    public class ServiceExporterTests
    {
        [Fact]
        public void ExportXml_ShouldReturnValidXmlString()
        {
            // Arrange
            var service = ServiceDtoFactory.CreateSampleExport();

            // Act
            var xml = ServiceExporter.ExportXml(service);

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(xml));
            Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", xml);

            // Core Identity
            Assert.Contains("<Name>MyService</Name>", xml);

            // Priority & Startup
            Assert.Contains("<StartupType>2</StartupType>", xml);
            Assert.Contains("<Priority>1</Priority>", xml);

            // Logging & Size Rotation
            Assert.Contains("<EnableSizeRotation>true</EnableSizeRotation>", xml);
            Assert.Contains("<RotationSize>1024</RotationSize>", xml);

            // Health Monitoring
            Assert.Contains("<EnableHealthMonitoring>true</EnableHealthMonitoring>", xml);
            Assert.Contains("<HeartbeatInterval>10</HeartbeatInterval>", xml);

            // Environment & Dependencies
            Assert.Contains("<EnvironmentVariables>VAR1=VAL1;VAR2=VAL2</EnvironmentVariables>", xml);
            Assert.Contains("<ServiceDependencies>dep1;dep2</ServiceDependencies>", xml);

            // PreLaunch Block
            Assert.Contains("<PreLaunchExecutablePath>pre.exe</PreLaunchExecutablePath>", xml);
            Assert.Contains("<PreLaunchTimeoutSeconds>30</PreLaunchTimeoutSeconds>", xml);
        }

        [Fact]
        public void ExportXml_File_ShouldWriteFile()
        {
            // Arrange
            var service = ServiceDtoFactory.CreateSampleExport();
            var tempFile = Path.GetTempFileName();

            // Act & Assert
            try
            {
                ServiceExporter.ExportXml(service, tempFile);
                var content = File.ReadAllText(tempFile);

                Assert.False(string.IsNullOrWhiteSpace(content));
                Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", content);

                // Assert structural validation subsets
                Assert.Contains("<Name>MyService</Name>", content);
                Assert.Contains("<StartupType>2</StartupType>", content);
                Assert.Contains("<RotationSize>1024</RotationSize>", content);
                Assert.Contains("<PreLaunchExecutablePath>pre.exe</PreLaunchExecutablePath>", content);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void ExportJson_ShouldReturnValidJsonString()
        {
            // Arrange
            var service = ServiceDtoFactory.CreateSampleExport();

            // Act
            var json = ServiceExporter.ExportJson(service);

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(json));

            // Core Identity
            Assert.Contains("\"Name\": \"MyService\"", json);
            Assert.Contains("\"ExecutablePath\": \"C:\\\\service.exe\"", json);

            // Priority & Startup
            Assert.Contains("\"StartupType\": 2", json);
            Assert.Contains("\"Priority\": 1", json);

            // Logging & Size Rotation
            Assert.Contains("\"EnableSizeRotation\": true", json);
            Assert.Contains("\"RotationSize\": 1024", json);

            // Health Monitoring
            Assert.Contains("\"EnableHealthMonitoring\": true", json);
            Assert.Contains("\"HeartbeatInterval\": 10", json);

            // Environment & Dependencies
            Assert.Contains("\"EnvironmentVariables\": \"VAR1=VAL1;VAR2=VAL2\"", json);
            Assert.Contains("\"ServiceDependencies\": \"dep1;dep2\"", json);

            // PreLaunch Block
            Assert.Contains("\"PreLaunchExecutablePath\": \"pre.exe\"", json);
            Assert.Contains("\"PreLaunchTimeoutSeconds\": 30", json);
        }

        [Fact]
        public void ExportJson_File_ShouldWriteFile()
        {
            // Arrange
            var service = ServiceDtoFactory.CreateSampleExport();
            var tempFile = Path.GetTempFileName();

            // Act & Assert
            try
            {
                ServiceExporter.ExportJson(service, tempFile);
                var content = File.ReadAllText(tempFile);

                Assert.False(string.IsNullOrWhiteSpace(content));

                // Assert structural validation subsets
                Assert.Contains("\"Name\": \"MyService\"", content);
                Assert.Contains("\"StartupType\": 2", content);
                Assert.Contains("\"RotationSize\": 1024", content);
                Assert.Contains("\"PreLaunchExecutablePath\": \"pre.exe\"", content);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void ExportJson_ShouldIgnoreNullValues()
        {
            // Arrange
            var service = new ServiceDto
            {
                Name = "TestService",
                ExecutablePath = "C:\\service.exe"
                // All other optional reference and nullable value types are left null/empty
            };

            // Act
            var json = ServiceExporter.ExportJson(service);

            // Assert
            // 1. Verify populated properties are correctly rendered with explicit key-value structure
            Assert.Contains("\"Name\": \"TestService\"", json);
            Assert.Contains("\"ExecutablePath\": \"C:\\\\service.exe\"", json);

            // 2. Comprehensive validation: Ensure all properties matching dynamic conditional 
            // serialization rules (ShouldSerialize*) are completely omitted when null or unassigned.
            var keysToProveAbsent = new[]
            {
                "DisplayName",
                "Description",
                "StartupDirectory",
                "Parameters",
                "StartupType",
                "Priority",
                "EnableConsoleUI",
                "StdoutPath",
                "StderrPath",
                "EnableSizeRotation",
                "RotationSize",
                "EnableDateRotation",
                "DateRotationType",
                "MaxRotations",
                "UseLocalTimeForRotation",
                "EnableHealthMonitoring",
                "HeartbeatInterval",
                "MaxFailedChecks",
                "RecoveryAction",
                "RecoveryOnCleanExit",
                "MaxRestartAttempts",
                "FailureProgramPath",
                "FailureProgramStartupDirectory",
                "FailureProgramParameters",
                "EnvironmentVariables",
                "ServiceDependencies",
                "PreLaunchExecutablePath",
                "PreLaunchStartupDirectory",
                "PreLaunchParameters",
                "PreLaunchEnvironmentVariables",
                "PreLaunchStdoutPath",
                "PreLaunchStderrPath",
                "PreLaunchTimeoutSeconds",
                "PreLaunchRetryAttempts",
                "PreLaunchIgnoreFailure",
                "PostLaunchExecutablePath",
                "PostLaunchStartupDirectory",
                "PostLaunchParameters",
                "EnableDebugLogs",
                "StartTimeout",
                "StopTimeout",
                "PreStopExecutablePath",
                "PreStopStartupDirectory",
                "PreStopParameters",
                "PreStopTimeoutSeconds",
                "PreStopLogAsError",
                "PostStopExecutablePath",
                "PostStopStartupDirectory",
                "PostStopParameters",
                
                // Hardened Security Bounds: Validate explicit exclusion of unmanaged internal properties 
                // and sensitive credentials that are decorated with [JsonIgnore] or skipped natively.
                "Id",
                "Pid",
                "PreviousStopTimeout",
                "ActiveStdoutPath",
                "ActiveStderrPath",
                "RunAsLocalSystem",
                "UserAccount",
                "Password"
            };

            foreach (var key in keysToProveAbsent)
            {
                // Quote the keys to prevent false positive substring matching against string field values
                Assert.DoesNotContain($"\"{key}\"", json);
            }
        }

        #region Null-DTO Contract Fallback Tests

        [Fact]
        public void ExportXml_NullService_ReturnsNull()
        {
            // Act
            var result = ServiceExporter.ExportXml((ServiceDto?)null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ExportJson_NullService_ReturnsNull()
        {
            // Act
            var result = ServiceExporter.ExportJson((ServiceDto?)null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ExportXml_File_NullService_DoesNotWriteFile()
        {
            // Arrange
            var path = Path.Combine(Path.GetTempPath(), "ExportXml_Null_" + Path.GetRandomFileName());

            try
            {
                // Act
                ServiceExporter.ExportXml(null, path);

                // Assert
                // Verify the structural no-op contract: execution exits cleanly without touching disk storage.
                Assert.False(File.Exists(path), "ExportXml must not create a file placeholder when supplied a null DTO payload.");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void ExportJson_File_NullService_DoesNotWriteFile()
        {
            // Arrange
            var path = Path.Combine(Path.GetTempPath(), "ExportJson_Null_" + Path.GetRandomFileName());

            try
            {
                // Act
                ServiceExporter.ExportJson(null, path);

                // Assert
                // Verify the structural no-op contract: execution exits cleanly without touching disk storage.
                Assert.False(File.Exists(path), "ExportJson must not create a file placeholder when supplied a null DTO payload.");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        #endregion
    }
}
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
                // other properties left null
            };

            // Act
            var json = ServiceExporter.ExportJson(service);

            // Assert
            Assert.Contains("\"Name\": \"TestService\"", json);
            Assert.Contains("\"ExecutablePath\": \"C:\\\\service.exe\"", json);
            Assert.DoesNotContain("Description", json); // null properties should be ignored
        }
    }
}
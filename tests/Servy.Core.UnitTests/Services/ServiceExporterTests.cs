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
            Assert.Contains("<Name>MyService</Name>", xml);
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
                Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", content.Trim());
                Assert.Contains("<Name>MyService</Name>", content);
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
            Assert.Contains("\"Name\": \"MyService\"", json);
            Assert.Contains("\"ExecutablePath\": \"C:\\\\service.exe\"", json);
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
                Assert.Contains("\"Name\": \"MyService\"", content);
                Assert.Contains("\"ExecutablePath\": \"C:\\\\service.exe\"", content);
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
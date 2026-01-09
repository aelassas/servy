using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.Core.Data;
using Servy.Core.Helpers;
using Servy.Core.Services;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.CLI.UnitTests.Commands
{
    public class ImportServiceCommandTests
    {
        private readonly Mock<IServiceRepository> _serviceRepoMock;
        private readonly Mock<IXmlServiceSerializer> _xmlServiceSerializer;
        private readonly Mock<IServiceManager> _serviceManager;
        private readonly ImportServiceCommand _command;

        public ImportServiceCommandTests()
        {
            _serviceRepoMock = new Mock<IServiceRepository>();
            _xmlServiceSerializer = new Mock<IXmlServiceSerializer>();
            _serviceManager = new Mock<IServiceManager>();
            _command = new ImportServiceCommand(_serviceRepoMock.Object, _xmlServiceSerializer.Object, _serviceManager.Object);
        }

        [Fact]
        public async Task Execute_XmlFile_Valid_CallsImportAndReturnsOk()
        {
            // Arrange
            var path = "test.xml";
            var xmlContent = @"
            <ServiceDto>
              <Name>MyTestService</Name>
              <ExecutablePath>C:\Program Files\nodejs\node.exe</ExecutablePath>
            </ServiceDto>";

            File.WriteAllText(path, xmlContent);

            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = path };

            MockXmlValidator(true);

            _serviceRepoMock.Setup(r => r.ImportXML(xmlContent, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act
            var result = await _command.Execute(opts);

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("XML configuration saved successfully", result.Message);

            _serviceRepoMock.Verify(r => r.ImportXML(xmlContent, It.IsAny<CancellationToken>()), Times.Once);

            File.Delete(path);
        }

        [Fact]
        public async Task Execute_XmlFile_Invalid_ReturnsFail()
        {
            // Arrange
            var path = "test_invalid.xml";
            var xmlContent = "<service></service>";
            File.WriteAllText(path, xmlContent);

            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = path };

            MockXmlValidator(false, "error");

            // Act
            var result = await _command.Execute(opts);

            // Assert
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("XML file not valid", result.Message);

            File.Delete(path);
        }

        [Fact]
        public async Task Execute_JsonFile_Valid_CallsImportAndReturnsOk()
        {
            // Arrange
            var path = "test.json";
            var jsonContent = "{\"Name\":\"TestService\",\"ExecutablePath\":\"C:\\\\TestService\\\\app.exe\"}";
            File.WriteAllText(path, jsonContent);

            var opts = new ImportServiceOptions { ConfigFileType = "json", Path = path };

            MockJsonValidator(true);

            _serviceRepoMock.Setup(r => r.ImportJSON(jsonContent, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act
            var result = await _command.Execute(opts);

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("JSON configuration saved successfully", result.Message);

            _serviceRepoMock.Verify(r => r.ImportJSON(jsonContent, It.IsAny<CancellationToken>()), Times.Once);

            File.Delete(path);
        }

        [Fact]
        public async Task Execute_JsonFile_Invalid_ReturnsFail()
        {
            // Arrange
            var path = "test_invalid.json";
            var jsonContent = "{\"Name\":\"TestService\"}";
            File.WriteAllText(path, jsonContent);

            var opts = new ImportServiceOptions { ConfigFileType = "json", Path = path };

            MockJsonValidator(false, "error");

            // Act
            var result = await _command.Execute(opts);

            // Assert
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("JSON file not valid", result.Message);

            File.Delete(path);
        }

        [Fact]
        public async Task Execute_FileDoesNotExist_ReturnsFail()
        {
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = "nonexistent.xml" };

            var result = await _command.Execute(opts);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("File not found", result.Message);
        }

        [Fact]
        public async Task Execute_ConfigTypeInvalid_ReturnsFail()
        {
            var opts = new ImportServiceOptions { ConfigFileType = "invalid", Path = "file.xml" };

            var result = await _command.Execute(opts);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("Configuration output file type is required", result.Message);
        }

        // Helpers for mocking static validators
        private void MockXmlValidator(bool isValid, string errorMsg = null)
        {
            var validator = typeof(XmlServiceValidator);
            validator.GetMethod("TryValidate").Invoke(null, new object[] { "", null });
            // Use a library like Pose or replace XmlServiceValidator with an interface to mock in real project
        }

        private void MockJsonValidator(bool isValid, string errorMsg = null)
        {
            var validator = typeof(JsonServiceValidator);
            validator.GetMethod("TryValidate").Invoke(null, new object[] { "", null });
        }
    }
}

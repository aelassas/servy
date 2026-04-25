using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.Core.Data;
using Servy.Core.Services;

namespace Servy.CLI.UnitTests.Commands
{
    public class ImportServiceCommandTests
    {
        private readonly Mock<IServiceRepository> _serviceRepoMock;
        private readonly Mock<IXmlServiceSerializer> _xmlServiceSerializer;
        private readonly Mock<IJsonServiceSerializer> _jsonServiceSerializer;
        private readonly Mock<IServiceManager> _serviceManager;
        private readonly Mock<IXmlServiceValidator> _xmlValidatorMock;
        private readonly Mock<IJsonServiceValidator> _jsonValidatorMock;
        private readonly ImportServiceCommand _command;

        public ImportServiceCommandTests()
        {
            _serviceRepoMock = new Mock<IServiceRepository>();
            _xmlServiceSerializer = new Mock<IXmlServiceSerializer>();
            _jsonServiceSerializer = new Mock<IJsonServiceSerializer>();
            _serviceManager = new Mock<IServiceManager>();
            _xmlValidatorMock = new Mock<IXmlServiceValidator>();
            _jsonValidatorMock = new Mock<IJsonServiceValidator>();

            _command = new ImportServiceCommand(
                _serviceRepoMock.Object,
                _xmlServiceSerializer.Object,
                _jsonServiceSerializer.Object,
                _serviceManager.Object,
                _xmlValidatorMock.Object,
                _jsonValidatorMock.Object);
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

            _serviceRepoMock.Setup(r => r.ImportXmlAsync(xmlContent, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act
            var result = await _command.Execute(opts);

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("XML configuration imported successfully", result.Message);

            _serviceRepoMock.Verify(r => r.ImportXmlAsync(xmlContent, It.IsAny<CancellationToken>()), Times.Once);

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
            Assert.Contains("XML file is not valid", result.Message);

            File.Delete(path);
        }

        [Fact]
        public async Task Execute_JsonFile_Valid_CallsImportAndReturnsOk()
        {
            // Arrange
            var realPath = @"C:\Windows\System32\notepad.exe";
            var path = Path.GetTempFileName() + ".json";

            var jsonContent = "{\"Name\":\"TestService\",\"ExecutablePath\":\"" + realPath.Replace("\\", "\\\\") + "\"}";
            File.WriteAllText(path, jsonContent);

            var opts = new ImportServiceOptions { ConfigFileType = "json", Path = path };

            MockJsonValidator(true);

            _serviceRepoMock.Setup(r => r.ImportJsonAsync(jsonContent, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _command.Execute(opts);

            // Assert
            try
            {
                Assert.Equal(0, result.ExitCode);
                Assert.Contains("JSON configuration imported successfully", result.Message);

                _serviceRepoMock.Verify(r => r.ImportJsonAsync(jsonContent, It.IsAny<CancellationToken>()), Times.Once);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public async Task Execute_JsonFile_Invalid_ReturnsFail()
        {
            // Arrange
            var path = "test_invalid.json";
            var jsonContent = "{\"Name\":\"TestService\"}";
            File.WriteAllText(path, jsonContent);

            var opts = new ImportServiceOptions { ConfigFileType = "json", Path = path };

            // FIX: Pass the exact error message the test expects to prove the mock is working
            MockJsonValidator(false, "Executable path is required");

            // Act
            var result = await _command.Execute(opts);

            // Assert
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("JSON file is not valid: Executable path is required", result.Message);

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
            Assert.Contains("Configuration input file type is required", result.Message);
        }

        // Helpers using Moq to correctly simulate success/failure branches
        private void MockXmlValidator(bool isValid, string errorMsg = null!)
        {
            string? dummy = errorMsg;
            _xmlValidatorMock
                .Setup(v => v.TryValidate(It.IsAny<string>(), out dummy))
                .Returns(isValid);
        }

        private void MockJsonValidator(bool isValid, string errorMsg = null!)
        {
            string? dummy = errorMsg;
            _jsonValidatorMock
                .Setup(v => v.TryValidate(It.IsAny<string>(), out dummy))
                .Returns(isValid);
        }
    }
}
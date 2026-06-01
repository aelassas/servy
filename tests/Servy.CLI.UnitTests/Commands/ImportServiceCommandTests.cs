using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Services;
using System.Net.Http.Json;

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
        private readonly Mock<IProcessHelper> _processHelper;
        private readonly ImportServiceCommand _command;

        public ImportServiceCommandTests()
        {
            _serviceRepoMock = new Mock<IServiceRepository>();
            _xmlServiceSerializer = new Mock<IXmlServiceSerializer>();
            _jsonServiceSerializer = new Mock<IJsonServiceSerializer>();
            _serviceManager = new Mock<IServiceManager>();
            _xmlValidatorMock = new Mock<IXmlServiceValidator>();
            _jsonValidatorMock = new Mock<IJsonServiceValidator>();
            _processHelper = new Mock<IProcessHelper>();

            _command = new ImportServiceCommand(
                _serviceRepoMock.Object,
                _xmlServiceSerializer.Object,
                _jsonServiceSerializer.Object,
                _serviceManager.Object,
                _xmlValidatorMock.Object,
                _jsonValidatorMock.Object,
                _processHelper.Object);
        }

        [Fact]
        public async Task Execute_XmlFile_Valid_CallsImportAndReturnsOk()
        {
            // Arrange
            var realPath = @"C:\Windows\System32\notepad.exe";
            var path = "test.xml";
            var xmlContent = $@"
            <ServiceDto>
              <Name>TestService</Name>
              <ExecutablePath>{realPath}</ExecutablePath>
            </ServiceDto>";

            File.WriteAllText(path, xmlContent);

            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = path };

            MockXmlValidator(true);

            var dto = new ServiceDto { Name = "TestService", ExecutablePath = realPath };
            _xmlServiceSerializer.Setup(s => s.Deserialize(xmlContent))
                   .Returns(dto);
            _processHelper.Setup(ph => ph.ValidatePath(realPath, true)).Returns(true);
            _serviceRepoMock.Setup(r => r.UpsertAsync(dto, true, true, It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(string.Format(Strings.Msg_ImportSuccessNoInstall, "XML"), result.Message);

            _serviceRepoMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), true, true, It.IsAny<CancellationToken>()), Times.Once);

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
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, result.ExitCode);
            Assert.Equal(string.Format(Strings.Msg_ImportFormatInvalid, "XML", "error"), result.Message);

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

            var dto = new ServiceDto { Name = "TestService", ExecutablePath = realPath };
            _jsonServiceSerializer.Setup(s => s.Deserialize(jsonContent))
                .Returns(dto);
            _processHelper.Setup(ph => ph.ValidatePath(realPath, true)).Returns(true);
            _serviceRepoMock.Setup(r => r.UpsertAsync(dto, true, true, It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            try
            {
                Assert.Equal(0, result.ExitCode);
                Assert.Equal(string.Format(Strings.Msg_ImportSuccessNoInstall, "JSON"), result.Message);

                _serviceRepoMock.Verify(r => r.UpsertAsync(It.IsAny<ServiceDto>(), true, true, It.IsAny<CancellationToken>()), Times.Once);
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

            // Pass the exact error message the test expects to prove the mock is working
            MockJsonValidator(false, "Executable path is required");

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotEqual(0, result.ExitCode);
            Assert.Equal(string.Format(Strings.Msg_ImportFormatInvalid, "JSON", "Executable path is required"), result.Message);

            File.Delete(path);
        }

        [Fact]
        public async Task Execute_FileDoesNotExist_ReturnsFail()
        {
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = "nonexistent.xml" };

            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("File not found", result.Message);
        }

        [Fact]
        public async Task Execute_ConfigTypeInvalid_ReturnsFail()
        {
            var opts = new ImportServiceOptions { ConfigFileType = "invalid", Path = "file.xml" };

            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            Assert.NotEqual(0, result.ExitCode);
            Assert.Equal(Strings.Msg_InvalidConfigFileType, result.Message);
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

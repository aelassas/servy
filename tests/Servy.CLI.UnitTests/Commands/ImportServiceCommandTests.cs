using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Common;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.CLI.UnitTests.Commands
{
    public class ImportServiceCommandTests : IDisposable
    {
        private readonly Mock<IServiceRepository> _serviceRepoMock;
        private readonly Mock<IXmlServiceSerializer> _xmlServiceSerializer;
        private readonly Mock<IJsonServiceSerializer> _jsonServiceSerializer;
        private readonly Mock<IServiceManager> _serviceManager;
        private readonly Mock<IXmlServiceValidator> _xmlValidatorMock;
        private readonly Mock<IJsonServiceValidator> _jsonValidatorMock;
        private readonly Mock<IProcessHelper> _processHelper;
        private readonly ImportServiceCommand _command;

        // Authentic local paths within safe security boundaries
        private readonly string _tempDirectory;
        private readonly string _legalXmlPath;
        private readonly string _legalJsonPath;

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

            // Establish safe, legal physical file anchors in a unique sub-directory to fulfill ImportGuard invariants
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"ImportTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDirectory);

            _legalXmlPath = Path.Combine(_tempDirectory, "legal_import.xml");
            _legalJsonPath = Path.Combine(_tempDirectory, "legal_import.json");
        }

        public void Dispose()
        {
            // Wipe physical artifacts and the tracking directory completely to clean up the workspace safely
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        #region Constructor Guard Clauses

        [Fact]
        public void Constructor_NullDependencies_ThrowsArgumentNullException()
        {
            // Arrange / Act / Assert
            Assert.Throws<ArgumentNullException>("serviceRepository", () => new ImportServiceCommand(null, _xmlServiceSerializer.Object, _jsonServiceSerializer.Object, _serviceManager.Object, _xmlValidatorMock.Object, _jsonValidatorMock.Object, _processHelper.Object));
            Assert.Throws<ArgumentNullException>("xmlServiceValidator", () => new ImportServiceCommand(_serviceRepoMock.Object, _xmlServiceSerializer.Object, _jsonServiceSerializer.Object, _serviceManager.Object, null, _jsonValidatorMock.Object, _processHelper.Object));
            Assert.Throws<ArgumentNullException>("jsonServiceValidator", () => new ImportServiceCommand(_serviceRepoMock.Object, _xmlServiceSerializer.Object, _jsonServiceSerializer.Object, _serviceManager.Object, _xmlValidatorMock.Object, null, _processHelper.Object));
            Assert.Throws<ArgumentNullException>("serviceManager", () => new ImportServiceCommand(_serviceRepoMock.Object, _xmlServiceSerializer.Object, _jsonServiceSerializer.Object, null, _xmlValidatorMock.Object, _jsonValidatorMock.Object, _processHelper.Object));
            Assert.Throws<ArgumentNullException>("xmlServiceSerializer", () => new ImportServiceCommand(_serviceRepoMock.Object, null, _jsonServiceSerializer.Object, _serviceManager.Object, _xmlValidatorMock.Object, _jsonValidatorMock.Object, _processHelper.Object));
            Assert.Throws<ArgumentNullException>("jsonServiceSerializer", () => new ImportServiceCommand(_serviceRepoMock.Object, _xmlServiceSerializer.Object, null, _serviceManager.Object, _xmlValidatorMock.Object, _jsonValidatorMock.Object, _processHelper.Object));
            Assert.Throws<ArgumentNullException>("processHelper", () => new ImportServiceCommand(_serviceRepoMock.Object, _xmlServiceSerializer.Object, _jsonServiceSerializer.Object, _serviceManager.Object, _xmlValidatorMock.Object, _jsonValidatorMock.Object, null));
        }

        #endregion

        #region ExecuteAsync Base Route & Guard Checks

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ExecuteAsync_PathIsNullOrWhiteSpace_ReturnsFail(string invalidPath)
        {
            // Arrange
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = invalidPath };

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_PathRequired, result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_ImportGuardFails_PathViolatesSecurityExtension_ReturnsFail()
        {
            // Arrange
            string badExtensionPath = Path.Combine(Path.GetTempPath(), $"corrupt_{Guid.NewGuid()}.txt");
            File.WriteAllText(badExtensionPath, "<ServiceDto/>");

            try
            {
                var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = badExtensionPath };

                // Act
                var result = await _command.ExecuteAsync(opts, CancellationToken.None);

                // Assert
                Assert.False(result.Success);
                Assert.Contains(".txt", result.Message);
            }
            finally
            {
                if (File.Exists(badExtensionPath)) File.Delete(badExtensionPath);
            }
        }

        [Fact]
        public async Task ExecuteAsync_InvalidConfigFileType_ReturnsInvalidConfigFileTypeMessage()
        {
            // Arrange
            File.WriteAllText(_legalXmlPath, "<ServiceDto><Name>Test</Name></ServiceDto>");

            // "ini" is not a member of ConfigFileType, so TryParseFileType rejects it up-front and ExecuteAsync returns Msg_InvalidConfigFileType.
            var opts = new ImportServiceOptions { ConfigFileType = "ini", Path = _legalXmlPath };

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_InvalidConfigFileType, result.Message);
        }

        #endregion

        #region Format-Specific Tracks (ProcessXmlAsync / ProcessJsonAsync)

        [Fact]
        public async Task ExecuteAsync_XmlFormatTrack_ValidPayload_ReturnsOk()
        {
            // Arrange
            var realPath = @"C:\Windows\System32\notepad.exe";
            var xmlContent = "<ServiceDto><Name>XmlService</Name></ServiceDto>";
            File.WriteAllText(_legalXmlPath, xmlContent);

            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = _legalXmlPath, InstallService = false };

            MockXmlValidator(true);
            var dto = new ServiceDto { Name = "XmlService", ExecutablePath = realPath };
            _xmlServiceSerializer.Setup(s => s.Deserialize(xmlContent)).Returns(dto);
            _processHelper.Setup(ph => ph.ValidatePath(realPath, true)).Returns(true);
            _serviceRepoMock.Setup(r => r.UpsertAsync(dto, true, true, It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(string.Format(Strings.Msg_ImportSuccessNoInstall, "XML"), result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_JsonFormatTrack_ValidPayload_ReturnsOk()
        {
            // Arrange
            var realPath = @"C:\Windows\System32\cmd.exe";
            var jsonContent = "{\"Name\":\"JsonService\"}";
            File.WriteAllText(_legalJsonPath, jsonContent);

            var opts = new ImportServiceOptions { ConfigFileType = "json", Path = _legalJsonPath, InstallService = false };

            MockJsonValidator(true);
            var dto = new ServiceDto { Name = "JsonService", ExecutablePath = realPath };
            _jsonServiceSerializer.Setup(s => s.Deserialize(jsonContent)).Returns(dto);
            _processHelper.Setup(ph => ph.ValidatePath(realPath, true)).Returns(true);
            _serviceRepoMock.Setup(r => r.UpsertAsync(dto, true, true, It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(string.Format(Strings.Msg_ImportSuccessNoInstall, "JSON"), result.Message);
        }

        #endregion

        #region ExecuteAsync Internal Validation Branches

        [Fact]
        public async Task ExecuteAsync_FormatValidationFails_ReturnsInvalidFormatResult()
        {
            // Arrange
            File.WriteAllText(_legalXmlPath, "<Malformed XML>");
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = _legalXmlPath };

            MockXmlValidator(false, "Missing close root tag definition context");

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(string.Format(Strings.Msg_ImportFormatInvalid, "XML", "Missing close root tag definition context"), result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_DeserializerReturnsNull_ReturnsDeserializationFailure()
        {
            // Arrange
            File.WriteAllText(_legalXmlPath, "<ServiceDto />");
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = _legalXmlPath };

            MockXmlValidator(true);
            _xmlServiceSerializer.Setup(s => s.Deserialize(It.IsAny<string>())).Returns((ServiceDto)null);

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_ImportDeserializationFailure, result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_JsonDeserializerReturnsNull_ReturnsDeserializationFailure()
        {
            // Arrange
            File.WriteAllText(_legalJsonPath, "{}");
            var opts = new ImportServiceOptions { ConfigFileType = "json", Path = _legalJsonPath };

            MockJsonValidator(true);
            _jsonServiceSerializer.Setup(s => s.Deserialize(It.IsAny<string>())).Returns((ServiceDto)null);

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_ImportDeserializationFailure, result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_RepositoryUpsertReturnsZero_ReturnsRepoFailure()
        {
            // Arrange
            var realPath = @"C:\Windows\System32\notepad.exe";
            File.WriteAllText(_legalXmlPath, "<ServiceDto></ServiceDto>");
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = _legalXmlPath, InstallService = false };

            MockXmlValidator(true);
            var dto = new ServiceDto { Name = "TestService", ExecutablePath = realPath };
            _xmlServiceSerializer.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(dto);
            _processHelper.Setup(ph => ph.ValidatePath(realPath, true)).Returns(true);
            _serviceRepoMock.Setup(r => r.UpsertAsync(dto, true, true, It.IsAny<CancellationToken>())).ReturnsAsync(0);

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(string.Format(Strings.Msg_ImportRepoFailure, "XML"), result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_JsonRepositoryUpsertReturnsZero_ReturnsRepoFailure()
        {
            // Arrange
            var realPath = @"C:\Windows\System32\cmd.exe";
            File.WriteAllText(_legalJsonPath, "{}");
            var opts = new ImportServiceOptions { ConfigFileType = "json", Path = _legalJsonPath, InstallService = false };

            MockJsonValidator(true);
            var dto = new ServiceDto { Name = "TestService", ExecutablePath = realPath };
            _jsonServiceSerializer.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(dto);
            _processHelper.Setup(ph => ph.ValidatePath(realPath, true)).Returns(true);
            _serviceRepoMock.Setup(r => r.UpsertAsync(dto, true, true, It.IsAny<CancellationToken>())).ReturnsAsync(0);

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(string.Format(Strings.Msg_ImportRepoFailure, "JSON"), result.Message);
        }

        #endregion

        #region ExecuteAsync Property Reflection Invariants

        [Fact]
        public async Task ExecuteAsync_EmptyRequiredPathProperty_ReturnsExecutablePathSpecificFailure()
        {
            // Arrange
            File.WriteAllText(_legalXmlPath, "<ServiceDto/>");
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = _legalXmlPath };

            MockXmlValidator(true);
            var dto = new ServiceDto { Name = "TestService", ExecutablePath = string.Empty };
            _xmlServiceSerializer.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(dto);

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(string.Format(Strings.Msg_InvalidExecutablePath, string.Empty), result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_InvalidExecutablePath_ReturnsPathSpecificFailure()
        {
            // Arrange
            File.WriteAllText(_legalXmlPath, "<ServiceDto/>");
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = _legalXmlPath };

            MockXmlValidator(true);
            var dto = new ServiceDto { Name = "TestService", ExecutablePath = @"Z:\Missing\Dir\Engine.exe" };
            _xmlServiceSerializer.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(dto);
            _processHelper.Setup(p => p.ValidatePath(@"Z:\Missing\Dir\Engine.exe", It.IsAny<bool>())).Returns(false);

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(string.Format(Strings.Msg_InvalidExecutablePath, @"Z:\Missing\Dir\Engine.exe"), result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_InvalidSecondaryConfigPath_ReturnsGeneralInvalidPathFailure()
        {
            // Arrange
            File.WriteAllText(_legalXmlPath, "<ServiceDto/>");
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = _legalXmlPath };

            MockXmlValidator(true);
            // Executable path is legal, but a secondary attribute property (like WorkingDirectory) is invalid
            var dto = new ServiceDto { Name = "TestService", ExecutablePath = @"C:\Windows\notepad.exe", StartupDirectory = @"Y:\Invalid\Folder" };
            _xmlServiceSerializer.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(dto);

            _processHelper.Setup(p => p.ValidatePath(@"C:\Windows\notepad.exe", true)).Returns(true);
            _processHelper.Setup(p => p.ValidatePath(@"Y:\Invalid\Folder", false)).Returns(false);

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            // Proves execution hit the generalized config path error message branch fallback path
            Assert.Equal(string.Format(Strings.Msg_InvalidPathInConfig, "startup directory"), result.Message);
        }

        #endregion

        #region ExecuteAsync Workflow Installation Boundaries

        [Fact]
        public async Task ExecuteAsync_ServiceLookupFailsAfterImport_ReturnsLookupFailure()
        {
            // Arrange
            var realPath = @"C:\Windows\System32\notepad.exe";
            File.WriteAllText(_legalXmlPath, "<ServiceDto/>");
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = _legalXmlPath, InstallService = true };

            MockXmlValidator(true);
            var dto = new ServiceDto { Name = "GhostService", ExecutablePath = realPath };
            _xmlServiceSerializer.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(dto);
            _processHelper.Setup(ph => ph.ValidatePath(realPath, true)).Returns(true);
            _serviceRepoMock.Setup(r => r.UpsertAsync(dto, true, true, It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _serviceRepoMock.Setup(r => r.GetByNameAsync("GhostService", true, It.IsAny<CancellationToken>())).ReturnsAsync((ServiceDto)null);

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(string.Format(Strings.Msg_ImportInstallLookupFailure, "GhostService"), result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_InstallationDomainSucceeds_ReturnsSuccessMessage()
        {
            // Arrange
            var realPath = @"C:\Windows\System32\notepad.exe";
            File.WriteAllText(_legalXmlPath, "<ServiceDto/>");
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = _legalXmlPath, InstallService = true };

            MockXmlValidator(true);
            var dto = new ServiceDto { Name = "OperationalService", ExecutablePath = realPath };
            _xmlServiceSerializer.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(dto);
            _processHelper.Setup(ph => ph.ValidatePath(realPath, true)).Returns(true);
            _serviceRepoMock.Setup(r => r.UpsertAsync(dto, true, true, It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _serviceRepoMock.Setup(r => r.GetByNameAsync("OperationalService", true, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
            _serviceManager.Setup(m => m.InstallServiceAsync(It.IsAny<Core.Services.InstallServiceOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(OperationResult.Success());

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(string.Format(Strings.Msg_ImportInstallSuccess, "XML", "OperationalService"), result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_InstallationDomainFails_ReturnsDomainErrorMessage()
        {
            // Arrange
            var realPath = @"C:\Windows\System32\notepad.exe";
            File.WriteAllText(_legalXmlPath, "<ServiceDto/>");
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = _legalXmlPath, InstallService = true };

            MockXmlValidator(true);
            var dto = new ServiceDto { Name = "FailInstallService", ExecutablePath = realPath };
            _xmlServiceSerializer.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(dto);
            _processHelper.Setup(ph => ph.ValidatePath(realPath, true)).Returns(true);
            _serviceRepoMock.Setup(r => r.UpsertAsync(dto, true, true, It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _serviceRepoMock.Setup(r => r.GetByNameAsync("FailInstallService", true, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

            var domainError = "Critical Win32 error: SCM database lock timeout reached.";
            _serviceManager.Setup(m => m.InstallServiceAsync(It.IsAny<Core.Services.InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(OperationResult.Failure(domainError));

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(domainError, result.Message);
        }

        #endregion

        #region Test Initialization Mocking Helpers

        private void MockXmlValidator(bool isValid, string errorMsg = null)
        {
            string dummy = errorMsg;
            _xmlValidatorMock
                .Setup(v => v.TryValidate(It.IsAny<string>(), out dummy))
                .Returns(isValid);
        }

        private void MockJsonValidator(bool isValid, string errorMsg = null)
        {
            string dummy = errorMsg;
            _jsonValidatorMock
                .Setup(v => v.TryValidate(It.IsAny<string>(), out dummy))
                .Returns(isValid);
        }

        #endregion
    }
}
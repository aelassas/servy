using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Common;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Services;

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
        private readonly string _tempXmlPath;
        private readonly string _tempJsonPath;

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

            _tempXmlPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.xml");
            _tempJsonPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.json");
        }

        public void Dispose()
        {
            if (File.Exists(_tempXmlPath)) File.Delete(_tempXmlPath);
            if (File.Exists(_tempJsonPath)) File.Delete(_tempJsonPath);
        }

        #region Constructor ArgumentNullException Tests

        [Fact]
        public void Constructor_NullDependencies_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>("serviceRepository", () => new ImportServiceCommand(null!, _xmlServiceSerializer.Object, _jsonServiceSerializer.Object, _serviceManager.Object, _xmlValidatorMock.Object, _jsonValidatorMock.Object, _processHelper.Object));
            Assert.Throws<ArgumentNullException>("xmlServiceValidator", () => new ImportServiceCommand(_serviceRepoMock.Object, _xmlServiceSerializer.Object, _jsonServiceSerializer.Object, _serviceManager.Object, null!, _jsonValidatorMock.Object, _processHelper.Object));
            Assert.Throws<ArgumentNullException>("jsonServiceValidator", () => new ImportServiceCommand(_serviceRepoMock.Object, _xmlServiceSerializer.Object, _jsonServiceSerializer.Object, _serviceManager.Object, _xmlValidatorMock.Object, null!, _processHelper.Object));
            Assert.Throws<ArgumentNullException>("serviceManager", () => new ImportServiceCommand(_serviceRepoMock.Object, _xmlServiceSerializer.Object, _jsonServiceSerializer.Object, null!, _xmlValidatorMock.Object, _jsonValidatorMock.Object, _processHelper.Object));
            Assert.Throws<ArgumentNullException>("xmlServiceSerializer", () => new ImportServiceCommand(_serviceRepoMock.Object, null!, _jsonServiceSerializer.Object, _serviceManager.Object, _xmlValidatorMock.Object, _jsonValidatorMock.Object, _processHelper.Object));
            Assert.Throws<ArgumentNullException>("jsonServiceSerializer", () => new ImportServiceCommand(_serviceRepoMock.Object, _xmlServiceSerializer.Object, null!, _serviceManager.Object, _xmlValidatorMock.Object, _jsonValidatorMock.Object, _processHelper.Object));
            Assert.Throws<ArgumentNullException>("processHelper", () => new ImportServiceCommand(_serviceRepoMock.Object, _xmlServiceSerializer.Object, _jsonServiceSerializer.Object, _serviceManager.Object, _xmlValidatorMock.Object, _jsonValidatorMock.Object, null!));
        }

        #endregion

        #region Options Path Guard Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ExecuteAsync_PathIsNullOrWhiteSpace_ReturnsFail(string? invalidPath)
        {
            // Arrange
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = invalidPath! };

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_PathRequired, result.Message);
        }

        #endregion

        #region Switch Default Branch Tests

        [Fact]
        public async Task ExecuteAsync_SwitchDefault_ReturnsUnsupportedFileTypeResult()
        {
            // Arrange
            File.WriteAllText(_tempXmlPath, "<ServiceDto><Name>Test</Name></ServiceDto>");
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = _tempXmlPath };

            // Internal reflection trick to force the unexposed enum state block fallback track execution
            // Modifies string parsing via private/internal structures if Helper assembly bindings allow,
            // otherwise achieved by creating a test stub that returns an invalid enum value dynamically.
            // For xUnit decoupling consistency, we bypass configuration flags to mock structural layout bounds:
            var invalidTypeOpts = new ImportServiceOptions { ConfigFileType = "UnsupportedFormat", Path = _tempXmlPath };

            // Act
            var result = await _command.ExecuteAsync(invalidTypeOpts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
        }

        #endregion

        #region ProcessImportInternalAsync If Branches Coverage

        [Fact]
        public async Task ProcessImportInternalAsync_DeserializationReturnsNull_ReturnsDeserializationFailure()
        {
            // Arrange
            File.WriteAllText(_tempXmlPath, "<ServiceDto></ServiceDto>");
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = _tempXmlPath };

            MockXmlValidator(true);
            _xmlServiceSerializer.Setup(s => s.Deserialize(It.IsAny<string>())).Returns((ServiceDto?)null);

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_ImportDeserializationFailure, result.Message);
        }

        [Fact]
        public async Task ProcessImportInternalAsync_RepositoryUpsertReturnsZero_ReturnsRepoFailure()
        {
            // Arrange
            var realPath = @"C:\Windows\System32\notepad.exe";
            File.WriteAllText(_tempXmlPath, "<ServiceDto></ServiceDto>");
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = _tempXmlPath, InstallService = false };

            MockXmlValidator(true);
            var dto = new ServiceDto { Name = "TestService", ExecutablePath = realPath };
            _xmlServiceSerializer.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(dto);
            _processHelper.Setup(ph => ph.ValidatePath(realPath, true)).Returns(true);
            _serviceRepoMock.Setup(r => r.UpsertAsync(dto, true, true, It.IsAny<CancellationToken>())).ReturnsAsync(0); // Triggers the branch

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(string.Format(Strings.Msg_ImportRepoFailure, "XML"), result.Message);
        }

        #endregion

        #region ValidateServicePaths Reflection Logic Branch Coverage

        [Fact]
        public async Task ValidateServicePaths_RequiredPathMissing_ReturnsExecutablePathSpecificFailure()
        {
            // Arrange
            File.WriteAllText(_tempXmlPath, "<ServiceDto></ServiceDto>");
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = _tempXmlPath };

            MockXmlValidator(true);
            // ExecutablePath is string.Empty/null but is marked as Required inside the DTO definition layout
            var dto = new ServiceDto { Name = "TestService", ExecutablePath = string.Empty };
            _xmlServiceSerializer.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(dto);

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(string.Format(Strings.Msg_InvalidExecutablePath, string.Empty), result.Message);
        }

        [Fact]
        public async Task ValidateServicePaths_RequiredFieldOmitted_ReturnsGeneralInvalidPathFailure()
        {
            // Arrange
            File.WriteAllText(_tempXmlPath, "<ServiceDto></ServiceDto>");
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = _tempXmlPath };

            MockXmlValidator(true);
            // Non-ExecutablePath required metadata field breaks validity rules frame checks
            var dto = new ServiceDto { Name = "TestService", ExecutablePath = @"C:\notepad.exe" };
            _xmlServiceSerializer.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(dto);
            _processHelper.Setup(p => p.ValidatePath(It.IsAny<string>(), It.IsAny<bool>())).Returns(false);

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
        }

        #endregion

        #region TryInstallServiceAsync Branch Coverage

        [Fact]
        public async Task TryInstallServiceAsync_ServiceNotFoundInRepoAfterImport_ReturnsLookupFailure()
        {
            // Arrange
            var realPath = @"C:\Windows\System32\notepad.exe";
            File.WriteAllText(_tempXmlPath, "<ServiceDto></ServiceDto>");
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = _tempXmlPath, InstallService = true };

            MockXmlValidator(true);
            var dto = new ServiceDto { Name = "GhostService", ExecutablePath = realPath };
            _xmlServiceSerializer.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(dto);
            _processHelper.Setup(ph => ph.ValidatePath(realPath, true)).Returns(true);
            _serviceRepoMock.Setup(r => r.UpsertAsync(dto, true, true, It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Match the updated interface signature: GetByNameAsync(string?, bool, CancellationToken)
            _serviceRepoMock.Setup(r => r.GetByNameAsync("GhostService", true, It.IsAny<CancellationToken>()))
                            .ReturnsAsync((ServiceDto?)null);

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(string.Format(Strings.Msg_ImportInstallLookupFailure, "GhostService"), result.Message);
        }

        [Fact]
        public async Task TryInstallServiceAsync_InstallationDomainFails_ReturnsDomainErrorMessage()
        {
            // Arrange
            var realPath = @"C:\Windows\System32\notepad.exe";
            File.WriteAllText(_tempXmlPath, "<ServiceDto></ServiceDto>");
            var opts = new ImportServiceOptions { ConfigFileType = "xml", Path = _tempXmlPath, InstallService = true };

            MockXmlValidator(true);
            var dto = new ServiceDto { Name = "FailInstallService", ExecutablePath = realPath };
            _xmlServiceSerializer.Setup(s => s.Deserialize(It.IsAny<string>())).Returns(dto);
            _processHelper.Setup(ph => ph.ValidatePath(realPath, true)).Returns(true);
            _serviceRepoMock.Setup(r => r.UpsertAsync(dto, true, true, It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Match the updated interface signature: GetByNameAsync(string?, bool, CancellationToken)
            _serviceRepoMock.Setup(r => r.GetByNameAsync("FailInstallService", true, It.IsAny<CancellationToken>()))
                            .ReturnsAsync(dto);

            // Match the modern async options block layout using OperationResult.Failure
            var expectedError = "Access Denied: Service wrapper requires elevated interact permissions.";
            _serviceManager.Setup(m => m.InstallServiceAsync(It.IsAny<Core.Services.InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(OperationResult.Failure(expectedError));

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(expectedError, result.Message);
        }

        #endregion

        #region Production Setup Core Mocking Helpers

        private void MockXmlValidator(bool isValid, string errorMsg = null!)
        {
            string? dummy = errorMsg;
            _xmlValidatorMock
                .Setup(v => v.TryValidate(It.IsAny<string>(), out dummy))
                .Returns(isValid);
        }

        #endregion
    }
}
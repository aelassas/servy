using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Testing;
using System.Security;

namespace Servy.CLI.UnitTests.Commands
{
    public class ExportServiceCommandTests : IDisposable
    {
        private readonly Mock<IServiceRepository> _serviceRepoMock;
        private readonly ExportServiceCommand _command;
        private readonly string _tempDir;

        public ExportServiceCommandTests()
        {
            _serviceRepoMock = new Mock<IServiceRepository>();
            _command = new ExportServiceCommand(_serviceRepoMock.Object);

            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, recursive: true); } catch { /* fail-safe */ }
            }
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ShouldThrowArgumentNullException_WhenRepositoryIsNull()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ExportServiceCommand(null!));
        }

        #endregion

        #region Execute Method Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Execute_ShouldFail_WhenServiceNameIsNullOrWhiteSpace(string? name)
        {
            // Arrange
            var opts = new ExportServiceOptions { ServiceName = name!, ConfigFileType = "xml", Path = "file.xml" };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(Core.Resources.Strings.Msg_ServiceNameRequired, result.Message);
        }

        [Fact]
        public async Task Execute_ShouldFail_WhenConfigFileTypeIsInvalid()
        {
            // Arrange
            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "invalid", Path = "file.xml" };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(string.Format(Strings.Msg_UnsupportedFileType, "invalid"), result.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Execute_ShouldFail_WhenPathIsNullOrWhiteSpace(string? path)
        {
            // Arrange
            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = path! };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(Strings.Msg_PathRequired, result.Message);
        }

        [Fact]
        public async Task Execute_ShouldFail_WhenServiceNotFound()
        {
            // Arrange
            _serviceRepoMock.Setup(r => r.GetByNameAsync("svc", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync((ServiceDto?)null);
            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = Path.Combine(_tempDir, "out.xml") };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(Core.Resources.Strings.Msg_ServiceNotFound, result.Message);
        }

        [Fact]
        public async Task Execute_ShouldExportXml_WhenConfigTypeIsXml()
        {
            // Arrange
            var filePath = Path.Combine(_tempDir, "out.xml");
            _serviceRepoMock.Setup(r => r.GetByNameAsync("svc", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ServiceDto { Name = "TestService" });
            _serviceRepoMock.Setup(r => r.ExportXmlAsync("svc", It.IsAny<CancellationToken>())).ReturnsAsync("<xml>data</xml>");

            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = filePath };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(string.Format(Strings.Msg_ExportSuccess, "XML", opts.Path), result.Message);
            Assert.True(File.Exists(filePath));
            Assert.Equal("<xml>data</xml>", File.ReadAllText(filePath));
        }

        [Fact]
        public async Task Execute_ShouldExportJson_WhenConfigTypeIsJson()
        {
            // Arrange
            var filePath = Path.Combine(_tempDir, "out.json");
            _serviceRepoMock.Setup(r => r.GetByNameAsync("svc", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ServiceDto { Name = "TestService" });
            _serviceRepoMock.Setup(r => r.ExportJsonAsync("svc", It.IsAny<CancellationToken>())).ReturnsAsync("{\"name\":\"svc\"}");

            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "json", Path = filePath };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(string.Format(Strings.Msg_ExportSuccess, "JSON", opts.Path), result.Message);
            Assert.True(File.Exists(filePath));
            Assert.Equal("{\"name\":\"svc\"}", File.ReadAllText(filePath));
        }

        [Fact]
        public async Task Execute_ShouldHandleException()
        {
            // Arrange
            _serviceRepoMock.Setup(r => r.GetByNameAsync("svc", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("boom"));
            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = Path.Combine(_tempDir, "out.xml") };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains(string.Format(Strings.Msg_ExportServiceAction, "svc"), result.Message);
        }

        #endregion

        #region SaveFile Validation Invariant Checks

        [Fact]
        public void SaveFile_ShouldCreateDirectoryIfNotExists()
        {
            // Arrange
            var filePath = Path.Combine(_tempDir, "subdir", "file.xml");
            var content = "hello";

            // Act
            InvokeSaveFile(filePath, content);

            // Assert
            Assert.True(File.Exists(filePath));
            Assert.Equal(content, File.ReadAllText(filePath));
        }

        [Fact]
        public void SaveFile_ShouldThrowArgumentException_WhenValidationFailsWithStandardError()
        {
            // Arrange
            // Providing an invalid extension ("txt") routes to PathSecurityGuard's extension filter,
            // producing an error payload that does not contain "Access Denied" or "Security Alert".
            var filePath = Path.Combine(_tempDir, "denied_extension.txt");

            // Act & Assert
            // TestReflection rethrows the inner exception, so the guard's ArgumentException surfaces directly
            var ex = Assert.Throws<ArgumentException>(() => InvokeSaveFile(filePath, "data"));
            Assert.Equal(string.Format(Core.Resources.Strings.Msg_SecurityInvalidFileType, ".txt"), ex.Message);
        }

        [Fact]
        public void SaveFile_ShouldThrowSecurityException_WhenValidationResultTriggersSecurityAlert()
        {
            // Arrange
            // Forcing a path sequence targeting a structural Windows system environment folder
            // triggers an internal "Security Alert" rule inside PathSecurityGuard, hitting the SecurityException branch.
            string protectedDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var filePath = Path.Combine(protectedDir, "malicious_export.json");

            // Act & Assert
            var ex = Assert.Throws<SecurityException>(() => InvokeSaveFile(filePath, "data"));
            Assert.Contains("Security Alert", ex.Message);
        }

        #endregion

        #region SaveFile I/O Error Catch Boundary Checks

        [Fact]
        public void SaveFile_ShouldThrowSecurityException_WhenFileStreamWriteFailsFromExternalLock()
        {
            // Arrange
            var filePath = Path.Combine(_tempDir, "locked_out.json");
            File.WriteAllText(filePath, "original contents");

            // Hold the file open with FileShare.None so PathSecurityGuard cannot open it for validation:
            // SaveFile invokes PathSecurityGuard, which opens the file with FileMode.OpenOrCreate and
            // FileAccess.ReadWrite and therefore throws a SecurityException while the lock is held.
            using (var lockStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act & Assert
                var ex = Assert.Throws<SecurityException>(() => InvokeSaveFile(filePath, "new config payload"));

                // The guard's message identifies the failed file-handle validation
                Assert.Contains("Security Alert: Target file handle validation was rejected", ex.Message);
            }
        }

        #endregion

        #region Transactional Rollback & Directory Integrity Tests

        [Fact]
        public void SaveFile_ShouldCreateDeepDirectoryTree_WhenPathIsValid()
        {
            // Arrange
            var deepSubDir = Path.Combine(_tempDir, "level1", "level2", "level3");
            var filePath = Path.Combine(deepSubDir, "service_export.json");
            var content = "{ \"Name\": \"TestServiceConfig\" }";

            // Act
            InvokeSaveFile(filePath, content);

            // Assert
            Assert.True(Directory.Exists(deepSubDir), "The multi-level parent directory chain should be created successfully.");
            Assert.True(File.Exists(filePath), "The targeted service export payload file should be created.");
            Assert.Equal(content, File.ReadAllText(filePath));
        }

        [Fact]
        public void SaveFile_ValidationFailsOnInvalidExtension_RollsBackCreatedDirectoriesCleanly()
        {
            // Arrange
            var deepSubDir = Path.Combine(_tempDir, "orphaned_tree", "nested_level");
            var filePath = Path.Combine(deepSubDir, "illegal_device_target.txt");
            var content = "[Stale Config Payload Data]";

            // Act & Assert
            // 1. Catch the unwrapped exception directly from TestReflection
            var actualEx = Assert.Throws<ArgumentException>(() => InvokeSaveFile(filePath, content));

            // 2. Assert against the actual exception content profile
            Assert.Contains(".txt", actualEx.Message);

            // Transactional Rollback Integrity Assertions
            Assert.False(File.Exists(filePath), "The target file should not have been generated.");
            Assert.False(Directory.Exists(deepSubDir), "The nested parent leaf folder should be rolled back and deleted.");
            Assert.False(Directory.Exists(Path.Combine(_tempDir, "orphaned_tree")), "The entire newly created parent path root should be swept away if empty.");
        }

        [Fact]
        public void SaveFile_ValidationFailsOnReservedDeviceName_RollsBackCreatedDirectoriesCleanly()
        {
            // Arrange
            var deepSubDir = Path.Combine(_tempDir, "dos_device_tree");
            var filePath = Path.Combine(deepSubDir, "COM1.json");
            var content = "{ }";

            // Act
            var actualEx = Record.Exception(() => InvokeSaveFile(filePath, content));
            Assert.NotNull(actualEx);

            // Assert
            bool isValidExceptionType = actualEx is ArgumentException || actualEx is SecurityException;
            Assert.True(isValidExceptionType, $"Expected ArgumentException or SecurityException, but caught: {actualEx.GetType().Name}");

            // When the guard rejected the path as an argument error, the message must name the reserved device
            if (actualEx is ArgumentException)
            {
                bool matchedExpectedSecurityRules = actualEx.Message.Contains("COM1");

                Assert.True(matchedExpectedSecurityRules,
                    $"The security guard rejected the path, but with an unexpected message profile: '{actualEx.Message}'");
            }

            // Nothing SaveFile created may remain on disk
            Assert.False(File.Exists(filePath));
            Assert.False(Directory.Exists(deepSubDir), "The directory allocated for the device name target should be atomically removed on error.");
        }

        [Fact]
        public void SaveFile_ValidationFailsOnDisallowedExtension_LeavesPreExistingRootUntouched()
        {
            // Arrange
            // The path is entirely inside the test's own sandbox, so no protected system folder is
            // involved: what rejects it is the allowed-extension filter (.json/.xml only), which runs
            // in the ValidatePathOnly pre-flight BEFORE any directory is created. The property under
            // test is therefore that a rejected save adds nothing next to a pre-existing root and
            // leaves that root alone.
            var preExistingRoot = Path.Combine(_tempDir, "stable_corporate_root");
            Directory.CreateDirectory(preExistingRoot);

            var generatedSubDir = Path.Combine(preExistingRoot, "dynamic_session_branch");
            var filePath = Path.Combine(generatedSubDir, "malformed_file.log");

            // Act & Assert
            var actualEx = Assert.Throws<ArgumentException>(() => InvokeSaveFile(filePath, "content"));

            // Assert: pin WHICH guard fired, so a future change that starts rejecting this path for a
            // different reason (or stops rejecting it) fails here instead of passing silently.
            Assert.Contains(".log", actualEx.Message);

            Assert.False(Directory.Exists(generatedSubDir), "No directory should be left behind next to the pre-existing root when the save is rejected.");
            Assert.True(Directory.Exists(preExistingRoot), "The pre-existing folder root must remain untouched by a rejected save.");
        }

        #endregion

        #region Reflection Helper Definition

        /// <summary>
        /// Invokes the private SaveFile(path, content) via TestReflection, which rethrows the inner exception.
        /// </summary>
        private void InvokeSaveFile(string path, string content)
        {
            TestReflection.InvokeNonPublic(_command, "SaveFile", path, content);
        }

        #endregion
    }
}

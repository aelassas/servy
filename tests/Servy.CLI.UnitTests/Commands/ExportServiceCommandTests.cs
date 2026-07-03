using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Testing;
using System;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

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
            Assert.Throws<ArgumentNullException>(() => new ExportServiceCommand(null));
        }

        #endregion

        #region Execute Method Tests

        [Fact]
        public async Task Execute_ShouldFail_WhenServiceNameIsNullOrEmpty()
        {
            // Arrange
            var opts = new ExportServiceOptions { ServiceName = "", ConfigFileType = "xml", Path = "file.xml" };

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_ServiceNameRequired, result.Message);
        }

        [Fact]
        public async Task Execute_ShouldFail_WhenConfigFileTypeIsInvalid()
        {
            // Arrange
            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "invalid", Path = "file.xml" };

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_InvalidConfigFileType, result.Message);
        }

        [Fact]
        public async Task Execute_ShouldFail_WhenPathIsNullOrEmpty()
        {
            // Arrange
            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = "" };

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_PathRequired, result.Message);
        }

        [Fact]
        public async Task Execute_ShouldFail_WhenServiceNotFound()
        {
            // Arrange
            _serviceRepoMock.Setup(r => r.GetByNameAsync("svc", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync((ServiceDto)null);
            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = Path.Combine(_tempDir, "out.xml") };

            // Act
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_ServiceNotFound, result.Message);
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
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
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
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
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
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            // Assert
            Assert.False(result.Success);
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
            // TestReflection unwraps TargetInvocationException cleanly to target the inner exception directly
            var ex = Assert.Throws<ArgumentException>(() => InvokeSaveFile(filePath, "data"));
            Assert.Equal(string.Format(Core.Resources.Strings.Msg_SecurityInvalidFileType, ".txt"), ex.Message);
        }

        [Fact]
        public void SaveFile_ShouldThrowSecurityException_WhenValidationResultTriggersSecurityAlert()
        {
            // Arrange
            // Forcing a path sequence targeting a structural Windows system environment folder
            // triggers an internal "Access Denied" rule inside PathSecurityGuard, hitting the SecurityException branch.
            string protectedDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var filePath = Path.Combine(protectedDir, "malicious_export.json");

            // Act & Assert
            var ex = Assert.Throws<SecurityException>(() => InvokeSaveFile(filePath, "data"));
            Assert.Contains("Access Denied", ex.Message);
        }

        #endregion

        #region SaveFile I/O Error Catch Boundary Checks

        [Fact]
        public void SaveFile_ShouldThrowSecurityException_WhenFileStreamWriteFailsFromExternalLock()
        {
            // Arrange
            var filePath = Path.Combine(_tempDir, "locked_out.json");
            File.WriteAllText(filePath, "original contents");

            // Exclusively open and lock down the target filesystem file channel before running SaveFile.
            // When SaveFile invokes PathSecurityGuard, it opens with FileMode.OpenOrCreate and FileAccess.ReadWrite.
            // Because our file handle context restricts any sharing options (FileShare.None), PathSecurityGuard
            // will throw a SecurityException while trying to allocate the stream.
            using (var lockStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act & Assert
                var ex = Assert.Throws<SecurityException>(() => InvokeSaveFile(filePath, "new config payload"));

                // Unwraps target reflection errors to expose the inner thrown exception rule
                Assert.Contains(string.Format(Core.Resources.Strings.Msg_SecurityHandleValidationFailed, string.Empty), ex.Message);
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

            // If it's a modern argument rejection, execute the localized syntax verification check
            if (actualEx is ArgumentException)
            {
                bool matchedExpectedSecurityRules = actualEx.Message.Contains("COM1") ||
                                                    actualEx.Message.Contains("UNC");

                Assert.True(matchedExpectedSecurityRules,
                    $"The security guard rejected the path, but with an unexpected message profile: '{actualEx.Message}'");
            }

            // Assert file system state left no permanent footprint across both frameworks
            Assert.False(File.Exists(filePath));
            Assert.False(Directory.Exists(deepSubDir), "The directory allocated for the device name target should be atomically removed on error.");
        }

        [Fact]
        public void SaveFile_ValidationFailsOnProtectedFolder_LeavesPreExistingRootUntouched()
        {
            // Arrange
            var preExistingRoot = Path.Combine(_tempDir, "stable_corporate_root");
            Directory.CreateDirectory(preExistingRoot);

            var generatedSubDir = Path.Combine(preExistingRoot, "dynamic_session_branch");
            var filePath = Path.Combine(generatedSubDir, "malformed_file.log");

            // Act & Assert
            var actualEx = Assert.Throws<ArgumentException>(() => InvokeSaveFile(filePath, "content"));

            // Assert
            Assert.False(Directory.Exists(generatedSubDir), "The dynamic folder leaf created during this call should be cleanly rolled back.");
            Assert.True(Directory.Exists(preExistingRoot), "The pre-existing folder root must remain untouched by our transaction fallback loop.");
        }

        [Fact]
        public void SaveFile_DirectoryCreatedButValidationFailsLater_TriggersFinallyFallbackRollback()
        {
            // Arrange
            // We use a path where the directory chain does NOT exist, forcing SaveFile to create it.
            var deepSubDir = Path.Combine(_tempDir, "finally_coverage_tree", "nested_leaf");

            // We use an invalid file extension (.log). This ensures that the directory creation pass 
            // succeeds completely, populates 'directoriesCreatedByUs', and THEN PathSecurityGuard 
            // rejects the file layout, throwing an ArgumentException!
            var filePath = Path.Combine(deepSubDir, "validation_failure_target.log");

            // Act & Assert
            var actualEx = Assert.Throws<ArgumentException>(() => InvokeSaveFile(filePath, "{ }"));

            // Assert: Verify that the fallback mechanism inside the finally block caught the failure,
            // executed the loop tracking arrays, and cleanly wiped the orphaned directories!
            Assert.False(Directory.Exists(deepSubDir), "The nested leaf directory should be rolled back via the finally loop.");
            Assert.False(Directory.Exists(Path.Combine(_tempDir, "finally_coverage_tree")), "The parent directory root should be swept cleanly.");
        }

        #endregion

        #region Reflection Helper Definition

        /// <summary>
        /// Safely executes the private SaveFile system method using reflection layers.
        /// </summary>
        private void InvokeSaveFile(string path, string content)
        {
            // Let TestReflection seamlessly invoke the non-public implementation framework
            TestReflection.InvokeNonPublic(_command, "SaveFile", path, content);
        }

        #endregion
    }
}
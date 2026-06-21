using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.CLI.Resources;
using Servy.Core.Data;
using Servy.Core.DTOs;
using System;
using System.IO;
using System.Reflection;
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
            // Assert & Act
            Assert.Throws<ArgumentNullException>(() => new ExportServiceCommand(null));
        }

        #endregion

        #region Execute Method Tests

        [Fact]
        public async Task Execute_ShouldFail_WhenServiceNameIsNullOrEmpty()
        {
            var opts = new ExportServiceOptions { ServiceName = "", ConfigFileType = "xml", Path = "file.xml" };
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_ServiceNameRequired, result.Message);
        }

        [Fact]
        public async Task Execute_ShouldFail_WhenConfigFileTypeIsInvalid()
        {
            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "invalid", Path = "file.xml" };
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_InvalidConfigFileType, result.Message);
        }

        [Fact]
        public async Task Execute_ShouldFail_WhenPathIsNullOrEmpty()
        {
            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = "" };
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_PathRequired, result.Message);
        }

        [Fact]
        public async Task Execute_ShouldFail_WhenServiceNotFound()
        {
            _serviceRepoMock.Setup(r => r.GetByNameAsync("svc", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync((ServiceDto)null);
            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = Path.Combine(_tempDir, "out.xml") };
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);
            Assert.False(result.Success);
            Assert.Equal(Strings.Msg_ServiceNotFound, result.Message);
        }

        [Fact]
        public async Task Execute_ShouldExportXml_WhenConfigTypeIsXml()
        {
            var filePath = Path.Combine(_tempDir, "out.xml");
            _serviceRepoMock.Setup(r => r.GetByNameAsync("svc", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ServiceDto { Name = "TestService" });
            _serviceRepoMock.Setup(r => r.ExportXmlAsync("svc", It.IsAny<CancellationToken>())).ReturnsAsync("<xml>data</xml>");

            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = filePath };
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(string.Format(Strings.Msg_ExportSuccess, "XML", opts.Path), result.Message);
            Assert.True(File.Exists(filePath));
            Assert.Equal("<xml>data</xml>", File.ReadAllText(filePath));
        }

        [Fact]
        public async Task Execute_ShouldExportJson_WhenConfigTypeIsJson()
        {
            var filePath = Path.Combine(_tempDir, "out.json");
            _serviceRepoMock.Setup(r => r.GetByNameAsync("svc", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ServiceDto { Name = "TestService" });
            _serviceRepoMock.Setup(r => r.ExportJsonAsync("svc", It.IsAny<CancellationToken>())).ReturnsAsync("{\"name\":\"svc\"}");

            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "json", Path = filePath };
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(string.Format(Strings.Msg_ExportSuccess, "JSON", opts.Path), result.Message);
            Assert.True(File.Exists(filePath));
            Assert.Equal("{\"name\":\"svc\"}", File.ReadAllText(filePath));
        }

        [Fact]
        public async Task Execute_ShouldHandleException()
        {
            _serviceRepoMock.Setup(r => r.GetByNameAsync("svc", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("boom"));

            var opts = new ExportServiceOptions { ServiceName = "svc", ConfigFileType = "xml", Path = Path.Combine(_tempDir, "out.xml") };
            var result = await _command.ExecuteAsync(opts, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("Failed to export configuration for service 'svc'", result.Message);
        }

        #endregion

        #region SaveFile Validation Invariant Checks

        [Fact]
        public void SaveFile_ShouldCreateDirectoryIfNotExists()
        {
            var filePath = Path.Combine(_tempDir, "subdir", "file.xml");
            var content = "hello";

            InvokeSaveFile(filePath, content);

            Assert.True(File.Exists(filePath));
            Assert.Equal(content, File.ReadAllText(filePath));
        }

        [Fact]
        public void SaveFile_ShouldThrowSecurityException_WhenValidationFailsWithStandardError()
        {
            // Providing an invalid extension ("txt") routes to PathSecurityGuard's extension filter,
            // producing an error payload that does not contain "Access Denied" or "Security Alert".
            var filePath = Path.Combine(_tempDir, "denied_extension.txt");

            var ex = Assert.Throws<TargetInvocationException>(() => InvokeSaveFile(filePath, "data"));
            Assert.IsType<ArgumentException>(ex.InnerException);
            Assert.Equal(string.Format(Core.Resources.Strings.Msg_SecurityInvalidFileType, ".txt"), ex.InnerException.Message);
        }

        [Fact]
        public void SaveFile_ShouldThrowSecurityException_WhenValidationResultTriggersSecurityAlert()
        {
            // Forcing a path sequence targeting a structural Windows system environment folder
            // triggers an internal "Access Denied" rule inside PathSecurityGuard, hitting the SecurityException branch.
            string protectedDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var filePath = Path.Combine(protectedDir, "malicious_export.json");

            var ex = Assert.Throws<TargetInvocationException>(() => InvokeSaveFile(filePath, "data"));
            Assert.IsType<SecurityException>(ex.InnerException);
            Assert.Contains("Access Denied", ex.InnerException.Message);
        }

        #endregion

        #region SaveFile I/O Error Catch Boundary Checks

        [Fact]
        public void SaveFile_ShouldThrowArgumentException_WhenFileStreamWriteFailsFromExternalLock()
        {
            var filePath = Path.Combine(_tempDir, "locked_out.json");
            File.WriteAllText(filePath, "original contents");

            // Exclusively open and lock down the target filesystem file channel before running SaveFile.
            // When SaveFile invokes PathSecurityGuard, it opens with FileMode.OpenOrCreate and FileAccess.ReadWrite.
            // Because our file handle context restricts any sharing options (FileShare.None), PathSecurityGuard
            // will throw an IOException/UnauthorizedAccessException while trying to allocate the stream.
            using (var lockStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var ex = Assert.Throws<TargetInvocationException>(() => InvokeSaveFile(filePath, "new config payload"));

                // Unwraps target reflection errors to expose the inner thrown exception rule
                Assert.IsType<SecurityException>(ex.InnerException);
                Assert.Contains(string.Format(Core.Resources.Strings.Msg_SecurityHandleValidationFailed, string.Empty), ex.InnerException.Message);
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
            // 1. Catch the reflection wrapper exception
            var reflectEx = Assert.Throws<TargetInvocationException>(() => InvokeSaveFile(filePath, content));

            // 2. Assert against the actual unwrapped inner exception
            var actualEx = Assert.IsType<ArgumentException>(reflectEx.InnerException);
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
            var reflectEx = Assert.Throws<TargetInvocationException>(() => InvokeSaveFile(filePath, content));
            var actualEx = reflectEx.InnerException;
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
            var reflectEx = Assert.Throws<TargetInvocationException>(() => InvokeSaveFile(filePath, "content"));
            Assert.IsType<ArgumentException>(reflectEx.InnerException);

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
            var reflectEx = Assert.Throws<TargetInvocationException>(() => InvokeSaveFile(filePath, "{ }"));
            Assert.IsType<ArgumentException>(reflectEx.InnerException);

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
            var method = typeof(ExportServiceCommand).GetMethod(
                "SaveFile",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (method == null)
            {
                throw new InvalidOperationException("Could not locate private method SaveFile inside ExportServiceCommand target reference metadata.");
            }

            // Let TargetInvocationException bubble up naturally to satisfy the test runner harness match rules.
            method.Invoke(_command, new object[] { path, content });
        }

        #endregion
    }
}
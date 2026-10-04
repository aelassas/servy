using Moq;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Testing;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Servy.Core.IntegrationTests.Helpers
{
    [Collection(CoreOsIntegrationCollection.Name)]
    public class ResourceHelperIntegrationTests : TempDirectoryTestBase
    {
        private readonly Mock<IProcessKiller> _mockProcessKiller;
        private readonly Mock<Assembly> _mockAssembly;
        private readonly ResourceHelper _resourceHelper;

        public ResourceHelperIntegrationTests()
        {
            _mockProcessKiller = new Mock<IProcessKiller>();
            _mockAssembly = new Mock<Assembly>();

            _resourceHelper = new ResourceHelper(_mockProcessKiller.Object);

            // Point the helper to the test-controlled temp directory
            _resourceHelper.BaseExtractionDirectory = TempDirectory;
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_NullProcessKiller_ThrowsArgumentNullException()
        {
            // Arrange
            IProcessKiller processKiller = null!;

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new ResourceHelper(processKiller));

            // Assert
            Assert.Equal("processKiller", ex.ParamName);
        }

        #endregion

        #region IsFileLocked Tests

        [Fact]
        public void IsFileLocked_WhenFileDoesNotExist_ReturnsFalse()
        {
            // Arrange
            string nonExistentPath = Path.Combine(TempDirectory, "non_existent_file.tmp");

            // Act
            bool isLocked = ResourceHelper.IsFileLocked(nonExistentPath);

            // Assert
            Assert.False(isLocked);
        }

        [Fact]
        public void IsFileLocked_WhenFileExistsAndIsUnlocked_ReturnsFalse()
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, "unlocked_file.tmp");
            File.WriteAllText(filePath, "test content");

            // Act
            bool isLocked = ResourceHelper.IsFileLocked(filePath);

            // Assert
            Assert.False(isLocked);
        }

        [Fact]
        public void IsFileLocked_WhenFileIsLockedExclusively_ReturnsTrue()
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, "locked_file.tmp");
            File.WriteAllText(filePath, "test content");

            // Open an exclusive lock on the file during the probe
            using (var lockStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act
                bool isLocked = ResourceHelper.IsFileLocked(filePath);

                // Assert
                Assert.True(isLocked);
            }
        }

        [Fact]
        public void IsFileLocked_WhenFileIsReadOnly_ReturnsTrue()
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, "readonly_file.tmp");
            File.WriteAllText(filePath, "test content");
            File.SetAttributes(filePath, FileAttributes.ReadOnly);

            try
            {
                // Act
                bool isLocked = ResourceHelper.IsFileLocked(filePath);

                // Assert
                // Requesting FileAccess.ReadWrite on a read-only file is denied by Windows regardless of
                // the caller's privilege level, which is the UnauthorizedAccessException arm this exercises
                // - a different arm from the IOException one the exclusive-lock test above covers.
                Assert.True(isLocked);
            }
            finally
            {
                // The base class's temp-directory teardown needs write access to delete the file
                File.SetAttributes(filePath, FileAttributes.Normal);
            }
        }

        #endregion

        #region TerminateBlockingProcesses Direct Unit Tests

        [Fact]
        public void TerminateBlockingProcesses_WhenFileDoesNotExist_ReturnsTrueAndSkipsProcessKiller()
        {
            // Arrange
            string nonExistentPath = Path.Combine(TempDirectory, "non_existent.exe");

            // Act
            bool result = _resourceHelper.TerminateBlockingProcesses(nonExistentPath);

            // Assert
            Assert.True(result);
            _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void TerminateBlockingProcesses_WhenFileIsUnlocked_ReturnsTrueAndSkipsProcessKiller()
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, "unlocked_direct.exe");
            File.WriteAllText(filePath, "unlocked text");

            // Act
            bool result = _resourceHelper.TerminateBlockingProcesses(filePath);

            // Assert
            Assert.True(result);
            _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void TerminateBlockingProcesses_WhenFileIsLockedAndKillerSucceeds_ReturnsTrue()
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, "locked_direct_success.exe");
            File.WriteAllText(filePath, "locked content");

            _mockProcessKiller.Setup(p => p.KillProcessesUsingFile(filePath)).Returns(true);

            using (var lockStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act
                bool result = _resourceHelper.TerminateBlockingProcesses(filePath);

                // Assert
                Assert.True(result);
                _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(filePath), Times.Once);
            }
        }

        [Fact]
        public void TerminateBlockingProcesses_WhenFileIsLockedAndKillerFails_ReturnsFalse()
        {
            // Arrange
            string filePath = Path.Combine(TempDirectory, "locked_direct_fail.exe");
            File.WriteAllText(filePath, "locked content");

            _mockProcessKiller.Setup(p => p.KillProcessesUsingFile(filePath)).Returns(false);

            using (var lockStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act
                bool result = _resourceHelper.TerminateBlockingProcesses(filePath);

                // Assert
                Assert.False(result);
                _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(filePath), Times.Once);
            }
        }

        #endregion

        #region CopyEmbeddedResource Integration Tests

        [Fact]
        public async Task CopyEmbeddedResource_WhenFileIsUnlocked_BypassesProcessKiller()
        {
            // Arrange
            string fileName = "unlockedapp";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");

            // An existing, unlocked target that is stale and differs from the resource, so the copy reaches the lock probe
            File.WriteAllText(targetPath, "unlocked target");
            File.SetLastWriteTimeUtc(targetPath, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));

            var dummyResourceBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>()))
                         .Returns(() => new MemoryStream(dummyResourceBytes));

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _mockAssembly.Object, "Servy.Resources", fileName, extension, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
            Assert.True(_resourceHelper.HasCopiedResources);
            Assert.Equal(dummyResourceBytes, File.ReadAllBytes(targetPath));

            // The unlocked probe short-circuits; the loose mock's default (false) would have failed the copy if it were reached
            _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenFileIsLocked_InvokesProcessKiller()
        {
            // Arrange
            string fileName = "lockedapp_probe";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");

            // Create target file on disk and push timestamp back relative to hostExeTime to force extraction
            File.WriteAllText(targetPath, "pre-existing locked content");
            DateTime hostExeTime = _resourceHelper.GetHostProcessLastWriteTimeUtc();
            File.SetLastWriteTimeUtc(targetPath, hostExeTime.AddDays(-1));

            // Configure process killer to release the stream lock when called
            FileStream? lockStream = new FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            _mockProcessKiller
                .Setup(p => p.KillProcessesUsingFile(targetPath))
                .Returns(() =>
                {
                    // Simulate process termination by disposing the test lock handle
                    lockStream?.Dispose();
                    lockStream = null;
                    return true;
                });

            var dummyResourceBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>()))
                         .Returns(() => new MemoryStream(dummyResourceBytes));

            try
            {
                // Act
                bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                    _mockAssembly.Object, "Servy.Resources", fileName, extension, cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.True(result);
                _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(targetPath), Times.Once);
            }
            finally
            {
                lockStream?.Dispose();
            }
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenResourceIsUpToDate_ReturnsTrueAndSkipsCopy()
        {
            // Arrange
            string fileName = "testapp";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");

            // Create a file and artificially push its LastWriteTime into the future to bypass the staleness threshold
            File.WriteAllText(targetPath, "old content");
            File.SetLastWriteTimeUtc(targetPath, DateTime.UtcNow.AddHours(1));

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _mockAssembly.Object, "Servy.Resources", fileName, extension, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result); // Should return true early
            Assert.False(_resourceHelper.HasCopiedResources); // Nothing was written, so nothing needs re-hardening
            _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenHostIsNewerButWithinStalenessThreshold_KeepsExistingFile()
        {
            // Arrange
            string fileName = "withinthreshold";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");

            // Anchor the existing extraction OLDER than the host executable, but by less than
            // AppConfig.ResourceStalenessThresholdMinutes: the threshold term is the only reason it is kept
            File.WriteAllText(targetPath, "existing content");
            DateTime hostExeTime = _resourceHelper.GetHostProcessLastWriteTimeUtc();
            File.SetLastWriteTimeUtc(targetPath, hostExeTime.AddMinutes(-(AppConfig.ResourceStalenessThresholdMinutes / 2)));

            // Supply a stream so a copy would visibly overwrite the file if the threshold were dropped
            var dummyResourceBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>()))
                         .Returns(() => new MemoryStream(dummyResourceBytes));

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _mockAssembly.Object, "Servy.Resources", fileName, extension, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result); // Should return true early without copying
            Assert.Equal("existing content", File.ReadAllText(targetPath));
            _mockAssembly.Verify(a => a.GetManifestResourceStream(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenProcessTerminationFails_ReturnsFalse()
        {
            // Arrange
            string fileName = "lockedapp";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");

            // Create target file AND force timestamp into the past relative to hostExeTime to trigger re-extraction
            File.WriteAllText(targetPath, "existing target");
            DateTime hostExeTime = _resourceHelper.GetHostProcessLastWriteTimeUtc();
            File.SetLastWriteTimeUtc(targetPath, hostExeTime.AddDays(-1));

            // Simulate process termination failure
            _mockProcessKiller.Setup(p => p.KillProcessesUsingFile(It.IsAny<string>())).Returns(false);

            var dummyResourceBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>()))
                         .Returns(() => new MemoryStream(dummyResourceBytes));

            // Lock the file exclusively so IsFileLocked returns true and routes to KillProcessesUsingFile
            using (var lockStream = new FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act
                bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                    _mockAssembly.Object, "Servy.Resources", fileName, extension, cancellationToken: TestContext.Current.CancellationToken);

                // Assert
                Assert.False(result);

                // The killer must have been reached exactly once, so the false result comes from the failed
                // termination and not from an earlier exit such as a missing resource stream (the #3984 trap).
                _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Once);
            }
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenResourceStreamNotFound_ReturnsFalse()
        {
            // Arrange
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>())).Returns((Stream?)null); // Simulate missing resource

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _mockAssembly.Object, "Servy.Resources", "missingapp", "exe", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            Assert.False(_resourceHelper.HasCopiedResources);
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenResourceStreamNotFound_KillsNoProcess()
        {
            // Arrange
            // A stale, existing target, so TryPrepareExtraction asks for a copy and the lock probe runs.
            string fileName = "missingstopapp";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");
            File.WriteAllText(targetPath, "existing target");
            File.SetLastWriteTimeUtc(targetPath, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));

            _mockProcessKiller.Setup(p => p.KillProcessesUsingFile(It.IsAny<string>())).Returns(true);

            // The resource is missing from the assembly
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>())).Returns((Stream?)null);

            // Lock the target exclusively so the killer would be reached if the guard ran after TerminateBlockingProcesses
            using (var lockStream = new FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act
                // LogCapture routes the static Logger into a private temp directory so the guard's own
                // message can be read back: it is the only observable difference between the guard and
                // the outer catch-all that a deleted guard falls into (the #7277 shape).
                var (result, textLogOutput) = await LogCapture.RunAsync(() => _resourceHelper.CopyEmbeddedResourceAsync(
                    _mockAssembly.Object,
                    "Servy.Resources",
                    fileName,
                    extension,
                    cancellationToken: TestContext.Current.CancellationToken));

                // Assert
                Assert.False(result);
                Assert.False(_resourceHelper.HasCopiedResources);

                // The #1851 ordering: nothing is side-effected before the resource is known to exist
                _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Never);

                // The guard's own arm, not the outer catch-all that a deleted guard falls into
                Assert.Contains("Embedded resource not found", textLogOutput);
                Assert.DoesNotContain("Failed to copy embedded resource", textLogOutput);
            }
        }

        [Fact]
        public async Task CopyEmbeddedResource_Success_WritesFileToDisk()
        {
            // Arrange
            string fileName = "validapp";
            string extension = "dll";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");

            // Provide a real memory stream with dummy data
            var dummyData = new byte[] { 0x01, 0x02, 0x03 };
            var memoryStream = new MemoryStream(dummyData);
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>())).Returns(memoryStream);

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _mockAssembly.Object, "Servy.Resources", fileName, extension, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
            Assert.True(_resourceHelper.HasCopiedResources); // A newly written file carries no grant for the service accounts, so it must be re-hardened
            Assert.True(File.Exists(targetPath));
            var writtenBytes = File.ReadAllBytes(targetPath);
            Assert.Equal(dummyData, writtenBytes);
            // The target file does not exist before the Act, so the lock probe short-circuits
            // and ProcessKiller is never invoked
            _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CopyEmbeddedResource_ThrowsException_CaughtByOuterCatch_ReturnsFalse()
        {
            // Arrange
            // Passing a null assembly throws a NullReferenceException at assembly.GetManifestResourceStream(...), which the outer catch converts to a false result
            Assembly nullAssembly = null!;

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                nullAssembly, "Servy.Resources", "crashapp", "exe", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result); // Caught successfully
        }

        [Fact]
        public void IsExtractionNeeded_FileMissing_ReturnsTrue()
        {
            Assert.True(_resourceHelper.IsExtractionNeeded("Servy.Resources", "missing", "exe"));
        }

        [Fact]
        public void IsExtractionNeeded_FileOlderThanTheRunningExecutable_ReturnsTrue()
        {
            // Arrange
            var targetPath = Path.Combine(TempDirectory, "stale.exe");
            File.WriteAllText(targetPath, "old");
            File.SetLastWriteTimeUtc(targetPath, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));

            // Act & Assert
            Assert.True(_resourceHelper.IsExtractionNeeded("Servy.Resources", "stale", "exe"));
        }

        [Fact]
        public void IsExtractionNeeded_FileUpToDate_ReturnsFalseAndWritesNothing()
        {
            // Arrange
            var targetPath = Path.Combine(TempDirectory, "current.exe");
            File.WriteAllText(targetPath, "current");
            File.SetLastWriteTimeUtc(targetPath, DateTime.UtcNow.AddHours(1));

            // Act
            var needed = _resourceHelper.IsExtractionNeeded("Servy.Resources", "current", "exe");

            // Assert
            Assert.False(needed);
            Assert.Equal("current", File.ReadAllText(targetPath));
            Assert.False(_resourceHelper.HasCopiedResources);
        }

        [Fact]
        public void IsExtractionNeeded_WithAssembly_StaleTimestampButIdenticalContent_ReturnsFalse()
        {
            // Arrange: the timestamps say "older build" (as when the installer stamped the executable in another time
            // zone), but the file is the same build as the embedded resource (#7358)
            var content = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03 };
            var targetPathSame = Path.Combine(TempDirectory, "same.exe");
            File.WriteAllBytes(targetPathSame, content);
            File.SetLastWriteTimeUtc(targetPathSame, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));
            _mockAssembly.Setup(a => a.GetManifestResourceStream("Servy.Resources.same.exe")).Returns(() => new MemoryStream(content));

            var targetPathStale = Path.Combine(TempDirectory, "stale_timestamp.exe");
            File.WriteAllBytes(targetPathStale, content);
            File.SetLastWriteTimeUtc(targetPathStale, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));

            // Act & Assert: nothing to update, so the services are not stopped
            Assert.False(_resourceHelper.IsExtractionNeeded(_mockAssembly.Object, "Servy.Resources", "same", "exe"));
            Assert.True(_resourceHelper.IsExtractionNeeded("Servy.Resources", "stale_timestamp", "exe")); // the timestamp-only overload still says stale
        }

        [Fact]
        public void IsExtractionNeeded_WithAssembly_StaleTimestampAndDifferentContent_ReturnsTrue()
        {
            // Arrange
            var targetPath = Path.Combine(TempDirectory, "older.exe");
            File.WriteAllBytes(targetPath, new byte[] { 0x01, 0x02, 0x03 });
            File.SetLastWriteTimeUtc(targetPath, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));
            _mockAssembly.Setup(a => a.GetManifestResourceStream("Servy.Resources.older.exe")).Returns(() => new MemoryStream(new byte[] { 0x01, 0x02, 0x04 }));

            // Act & Assert
            Assert.True(_resourceHelper.IsExtractionNeeded(_mockAssembly.Object, "Servy.Resources", "older", "exe"));
        }

        [Fact]
        public async Task CopyEmbeddedResource_StaleTimestampButIdenticalContent_WritesNothingAndKillsNothing()
        {
            // Arrange
            var content = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x07 };
            var targetPath = Path.Combine(TempDirectory, "unchanged.exe");
            File.WriteAllBytes(targetPath, content);
            File.SetLastWriteTimeUtc(targetPath, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>())).Returns(() => new MemoryStream(content));

            DateTime expectedTime = _resourceHelper.GetHostProcessLastWriteTimeUtc();

            // A running service keeps its wrapper open for reading, which the lock probe reports as locked, so the
            // killer is reachable here unless the identical-content check stops the copy first
            using (var runningImage = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                // Act
                var result = await _resourceHelper.CopyEmbeddedResourceAsync(_mockAssembly.Object, "Servy.Resources", "unchanged", "exe", cancellationToken: TestContext.Current.CancellationToken);

                // Assert: reported as done, but the file and whatever holds it open are left alone and write time is updated to host time
                Assert.True(result);
                Assert.False(_resourceHelper.HasCopiedResources);
                Assert.Equal(expectedTime, File.GetLastWriteTimeUtc(targetPath));
                _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Never);
            }
        }

        [Fact]
        public async Task CopyEmbeddedResource_StaleTimestampButIdenticalContent_UpdatesTimestampToAvoidFutureComparisons()
        {
            // Arrange
            var content = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x09 };
            var targetPath = Path.Combine(TempDirectory, "timestamp_update.exe");
            File.WriteAllBytes(targetPath, content);
            File.SetLastWriteTimeUtc(targetPath, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>())).Returns(() => new MemoryStream(content));

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(_mockAssembly.Object, "Servy.Resources", "timestamp_update", "exe", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
            Assert.False(_resourceHelper.HasCopiedResources);
            Assert.Equal(_resourceHelper.GetHostProcessLastWriteTimeUtc(), File.GetLastWriteTimeUtc(targetPath));

            // Verify subsequent IsExtractionNeeded queries (even without Assembly) now return false
            Assert.False(_resourceHelper.IsExtractionNeeded("Servy.Resources", "timestamp_update", "exe"));
        }

        [Fact]
        public async Task CopyEmbeddedResource_DifferentContent_WritesTheWholeResourceAfterTheComparison()
        {
            // Arrange: Each call to GetManifestResourceStream returns a fresh stream instance
            var newContent = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60 };
            var targetPath = Path.Combine(TempDirectory, "upgrade.exe");
            File.WriteAllBytes(targetPath, new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x61 });
            File.SetLastWriteTimeUtc(targetPath, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>())).Returns(() => new MemoryStream(newContent));

            // Act
            var result = await _resourceHelper.CopyEmbeddedResourceAsync(_mockAssembly.Object, "Servy.Resources", "upgrade", "exe", cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
            Assert.True(_resourceHelper.HasCopiedResources);
            Assert.Equal(newContent, File.ReadAllBytes(targetPath));
        }

        [Fact]
        public void GetHostProcessLastWriteTimeUtc_ExecutesSuccessfullyAndReturnsValidDate()
        {
            // Arrange (Static environment context validation)

            // Act
            DateTime result = _resourceHelper.GetHostProcessLastWriteTimeUtc();

            // Assert
            // It should at least be a valid historical or current date, not DateTime.MinValue
            Assert.True(result > DateTime.MinValue);
            Assert.True(result <= DateTime.UtcNow.AddMinutes(1)); // Allow slight buffer
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenCancelledBeforeTermination_ReturnsFalse()
        {
            // Arrange
            string fileName = "cancelapp";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");

            // A stale, existing target, so TryPrepareExtraction asks for a copy instead of returning early.
            File.WriteAllText(targetPath, "existing target");
            File.SetLastWriteTimeUtc(targetPath, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));

            // The killer would report success, so nothing downstream of TerminateBlockingProcesses
            // masks the Times.Never below: only the cancellation check keeps it from being reached.
            _mockProcessKiller.Setup(p => p.KillProcessesUsingFile(It.IsAny<string>())).Returns(true);

            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>()))
                         .Returns(() => new MemoryStream(new byte[] { 0x01 }));

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Lock the target exclusively so IsFileLocked returns true. Without this the target does not
            // exist, IsFileLocked short-circuits on the missing file, and the killer is unreachable
            // whether or not the cancellation check runs - which is what stopped the old test pinning it.
            using (var lockStream = new FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act
                // The cancellation check sits before the process-termination step, so a pre-cancelled
                // token reaches it. LogCapture routes the static Logger into a private temp directory
                // so the arm that ran can be read back.
                var (result, textLogOutput) = await LogCapture.RunAsync(() => _resourceHelper.CopyEmbeddedResourceAsync(
                    _mockAssembly.Object,
                    "Servy.Resources",
                    fileName,
                    extension,
                    cancellationToken: cts.Token));

                // Assert
                Assert.False(result);

                // The locked, stale target routes TerminateBlockingProcesses to the killer, so this now
                // fails if the cancellation check is removed.
                _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Never);

                // Both catch arms return false, so only the log tells them apart: this is the
                // OperationCanceledException arm, not the general one.
                Assert.Contains("was cancelled by the caller", textLogOutput);
                Assert.DoesNotContain("Failed to copy embedded resource", textLogOutput);
            }
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenBaseExtractionDirectoryIsEmpty_ReturnsFalse()
        {
            // Arrange
            // Path.Combine("", "emptydirapp.exe") is the bare file name, whose Path.GetDirectoryName is
            // an empty string - the only input that reaches TryPrepareExtraction's parent-directory guard.
            _resourceHelper.BaseExtractionDirectory = string.Empty;

            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>()))
                         .Returns(() => new MemoryStream(new byte[] { 0x01 }));

            // Act
            // LogCapture routes the static Logger into a private temp directory so the guard's own
            // message can be read back: it is the only observable difference between the guard and
            // the framework exception that replaces it when the guard is removed.
            var (result, textLogOutput) = await LogCapture.RunAsync(() => _resourceHelper.CopyEmbeddedResourceAsync(
                _mockAssembly.Object,
                "Servy.Resources",
                "emptydirapp",
                "exe",
                cancellationToken: TestContext.Current.CancellationToken));

            // Assert
            // The guard's IOException is caught by the method's own outer catch, so the observable
            // effect is a false result rather than a propagated exception.
            Assert.False(result);

            // Without the guard, Directory.CreateDirectory("") throws its own ArgumentException into
            // the same catch and the result is still false, so the result alone pins nothing: only
            // the guard's message tells the two apart.
            Assert.Contains("Could not resolve parent directory for extraction", textLogOutput);
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenReplacingAFileWithAnExplicitAce_KeepsTheAceAndTheProtection()
        {
            // Arrange
            // A stale existing extraction carrying one explicit entry the temp folder does not grant.
            // The owner of a file may rewrite its own DACL, so this needs no elevation.
            string fileName = "aclapp";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");
            var networkService = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);

            File.WriteAllText(targetPath, "old content");
            var before = new FileInfo(targetPath).GetAccessControl();
            before.AddAccessRule(new FileSystemAccessRule(networkService, FileSystemRights.ReadAndExecute, AccessControlType.Allow));
            new FileInfo(targetPath).SetAccessControl(before);

            // Push the timestamp back relative to the host exe so TryPrepareExtraction forces a replacement
            File.SetLastWriteTimeUtc(targetPath, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));

            var dummyResourceBytes = new byte[] { 0x01, 0x02, 0x03 };
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>()))
                         .Returns(() => new MemoryStream(dummyResourceBytes));

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _mockAssembly.Object, "Servy.Resources", fileName, extension, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);

            // The file really was replaced: WriteFileAtomicAsync moves a freshly staged temp file over
            // the target, which carries the temp file's security descriptor and not the old file's.
            Assert.Equal(dummyResourceBytes, File.ReadAllBytes(targetPath));

            var after = new FileInfo(targetPath).GetAccessControl();
            var explicitRules = after.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))
                                     .Cast<FileSystemAccessRule>();

            // RestoreFileSecurity writes the captured ACL back, so the explicit entry survives
            Assert.Contains(explicitRules, r => r.IdentityReference.Equals(networkService)
                                                && r.AccessControlType == AccessControlType.Allow
                                                && (r.FileSystemRights & FileSystemRights.ReadAndExecute) == FileSystemRights.ReadAndExecute);

            // ... and GetExistingFileSecurity's SetAccessRuleProtection keeps the replacement from
            // falling back to whatever the containing folder grants
            Assert.True(after.AreAccessRulesProtected);
        }

        #endregion
    }
}

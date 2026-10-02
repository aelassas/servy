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
        private readonly Mock<IServiceHelper> _mockServiceHelper;
        private readonly Mock<IProcessKiller> _mockProcessKiller;
        private readonly Mock<Assembly> _mockAssembly;
        private readonly ResourceHelper _resourceHelper;

        public ResourceHelperIntegrationTests()
        {
            _mockServiceHelper = new Mock<IServiceHelper>();
            _mockProcessKiller = new Mock<IProcessKiller>();
            _mockAssembly = new Mock<Assembly>();

            _resourceHelper = new ResourceHelper(_mockServiceHelper.Object, _mockProcessKiller.Object);

            // Point the helper to the test-controlled temp directory
            _resourceHelper.BaseExtractionDirectory = TempDirectory;
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_NullServiceHelper_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceHelper serviceHelper = null!;

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new ResourceHelper(serviceHelper, _mockProcessKiller.Object));

            // Assert
            Assert.Equal("serviceHelper", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullProcessKiller_ThrowsArgumentNullException()
        {
            // Arrange
            IProcessKiller processKiller = null!;

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new ResourceHelper(_mockServiceHelper.Object, processKiller));

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

            // Create an existing unlocked target file on disk
            File.WriteAllText(targetPath, "unlocked target");

            // Ensure the manifest resource stream is provided
            var dummyResourceBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>()))
                         .Returns(() => new MemoryStream(dummyResourceBytes));

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _mockAssembly.Object, "Servy.Resources", fileName, extension, stopServices: false, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
            // Lock probe should return false (unlocked), so ProcessKiller is never invoked
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
                    _mockAssembly.Object, "Servy.Resources", fileName, extension, stopServices: false, cancellationToken: TestContext.Current.CancellationToken);

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
                _mockAssembly.Object, "Servy.Resources", fileName, extension, stopServices: false, cancellationToken: TestContext.Current.CancellationToken);

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
                _mockAssembly.Object, "Servy.Resources", fileName, extension, stopServices: false, cancellationToken: TestContext.Current.CancellationToken);

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
                    _mockAssembly.Object, "Servy.Resources", fileName, extension, stopServices: false, cancellationToken: TestContext.Current.CancellationToken);

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
                _mockAssembly.Object, "Servy.Resources", "missingapp", "exe", stopServices: false, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
            Assert.False(_resourceHelper.HasCopiedResources);
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenResourceStreamNotFound_StopsNoServiceAndKillsNoProcess()
        {
            // Arrange
            // A stale, existing target, so TryPrepareExtraction asks for a copy and the lock probe runs.
            string fileName = "missingstopapp";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");
            File.WriteAllText(targetPath, "existing target");
            File.SetLastWriteTimeUtc(targetPath, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));

            var testServices = new List<string> { "Servy_Service_A" };
            _mockServiceHelper.Setup(s => s.GetRunningServyUIServices()).Returns(testServices);
            _mockServiceHelper.Setup(s => s.StopServicesAsync(testServices, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockServiceHelper.Setup(s => s.StartServicesAsync(testServices, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
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
                    stopServices: true,
                    cancellationToken: TestContext.Current.CancellationToken));

                // Assert
                Assert.False(result);
                Assert.False(_resourceHelper.HasCopiedResources);

                // The #1851 ordering: nothing is side-effected before the resource is known to exist
                _mockServiceHelper.Verify(s => s.StopServicesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
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
                _mockAssembly.Object, "Servy.Resources", fileName, extension, stopServices: false, cancellationToken: TestContext.Current.CancellationToken);

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

        [Theory]
        [InlineData(false)] // Tests the UI service routing path (isCli: false)
        [InlineData(true)]  // Tests the CLI service routing path (isCli: true)
        public async Task CopyEmbeddedResource_WhenStopServicesIsTrue_StopsAndRestartsDependentServices(bool isCli)
        {
            // Arrange
            string fileName = "serviceapp";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");
            var testServices = new List<string> { "Servy_Service_A", "Servy_Service_B" };

            // Mock the assembly to return a valid manifest stream so execution passes the initial safeguards
            var dummyResourceBytes = new byte[] { 0xAA, 0xBB, 0xCC };
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>()))
                         .Returns(() => new MemoryStream(dummyResourceBytes));

            // Setup the service helper to discover our running mock services based on the CLI layout flag
            if (isCli)
            {
                _mockServiceHelper.Setup(s => s.GetRunningServyCLIServices()).Returns(testServices);
            }
            else
            {
                _mockServiceHelper.Setup(s => s.GetRunningServyUIServices()).Returns(testServices);
            }

            // Mock the lifecycle control methods to return successful completed tasks
            _mockServiceHelper.Setup(s => s.StopServicesAsync(testServices, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockServiceHelper.Setup(s => s.StartServicesAsync(testServices, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _mockAssembly.Object,
                "Servy.Resources",
                fileName,
                extension,
                stopServices: true,
                isCli: isCli,
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            // 1. Verify the core copy transaction reported a success state
            Assert.True(result);
            Assert.True(File.Exists(targetPath));
            // The target file does not exist before the Act, so the lock probe short-circuits
            // and ProcessKiller is never invoked
            _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Never);

            // 2. Confirm the discovery call matched the isCli routing flag, and the other one was never made
            if (isCli)
            {
                _mockServiceHelper.Verify(s => s.GetRunningServyCLIServices(), Times.Once);
                _mockServiceHelper.Verify(s => s.GetRunningServyUIServices(), Times.Never);
            }
            else
            {
                _mockServiceHelper.Verify(s => s.GetRunningServyUIServices(), Times.Once);
                _mockServiceHelper.Verify(s => s.GetRunningServyCLIServices(), Times.Never);
            }

            // 3. Confirm that the targeted services were both cleanly stopped and subsequently revived
            _mockServiceHelper.Verify(s => s.StopServicesAsync(testServices, TestContext.Current.CancellationToken), Times.Once);

            // The asymmetry below is deliberate, not a site the #5362 / #5385 CancellationToken.None sweeps
            // missed: ResourceHelper restarts with CancellationToken.None on purpose, so an upfront pipeline
            // cancellation cannot leave the services it stopped orphaned. Do not "fix" this to the test's token.
            _mockServiceHelper.Verify(s => s.StartServicesAsync(testServices, CancellationToken.None), Times.Once,
                "StartServicesAsync must be called with CancellationToken.None so a cancelled copy still restarts the services it stopped.");
        }

        [Fact]
        public async Task CopyEmbeddedResource_ThrowsException_CaughtByOuterCatch_ReturnsFalse()
        {
            // Arrange
            // Passing a null assembly throws a NullReferenceException at assembly.GetManifestResourceStream(...), which the outer catch converts to a false result
            Assembly nullAssembly = null!;

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                nullAssembly, "Servy.Resources", "crashapp", "exe", stopServices: false, cancellationToken: TestContext.Current.CancellationToken);

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
        public async Task CopyEmbeddedResource_WhenStartServicesAsyncThrows_LogsAndStillReturnsCopyResult()
        {
            // Arrange
            string fileName = "restartfailapp";
            string extension = "exe";
            var testServices = new List<string> { "Servy_Service_A" };

            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>()))
                         .Returns(() => new MemoryStream(new byte[] { 0x01 }));
            _mockServiceHelper.Setup(s => s.GetRunningServyUIServices()).Returns(testServices);
            _mockServiceHelper.Setup(s => s.StopServicesAsync(testServices, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockServiceHelper.Setup(s => s.StartServicesAsync(testServices, It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new InvalidOperationException("restart boom"));

            // Act
            // LogCapture routes the static Logger into a private temp directory (the logDirectory
            // seam) so the restart-failure entry written inside the finally block can be read back,
            // without touching the product's own logs directory.
            var (result, textLogOutput) = await LogCapture.RunAsync(() => _resourceHelper.CopyEmbeddedResourceAsync(
                _mockAssembly.Object,
                "Servy.Resources",
                fileName,
                extension,
                stopServices: true,
                cancellationToken: TestContext.Current.CancellationToken));

            // Assert
            // The restart failure is logged inside the finally block, never rethrown, so the copy's own
            // outcome is what the method returns.
            Assert.True(result);
            Assert.True(File.Exists(Path.Combine(TempDirectory, $"{fileName}.{extension}")));
            _mockServiceHelper.Verify(s => s.StartServicesAsync(testServices, CancellationToken.None), Times.Once);

            // ... and the "Logs" half of the name: the failure is reported, with its exception
            // passed through rather than swallowed.
            Assert.Contains("previously-running services failed to restart", textLogOutput);
            Assert.Contains("restart boom", textLogOutput);
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenCopyAbortedAndRestartFails_LogsNotCopied()
        {
            // Arrange
            string fileName = "abortrestartfailapp";
            string extension = "exe";
            var testServices = new List<string> { "Servy_Service_A" };
            using var cts = new CancellationTokenSource();

            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>()))
                         .Returns(() => new MemoryStream(new byte[] { 0x01 }));
            _mockServiceHelper.Setup(s => s.GetRunningServyUIServices()).Returns(testServices);
            // The caller cancels while the services are being stopped, so the cancellation boundary
            // right before the copy fires and copyDone is never set.
            _mockServiceHelper.Setup(s => s.StopServicesAsync(testServices, It.IsAny<CancellationToken>()))
                              .Returns(() => { cts.Cancel(); return Task.CompletedTask; });
            _mockServiceHelper.Setup(s => s.StartServicesAsync(testServices, It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new InvalidOperationException("restart boom"));

            // Act
            var (result, textLogOutput) = await LogCapture.RunAsync(() => _resourceHelper.CopyEmbeddedResourceAsync(
                _mockAssembly.Object,
                "Servy.Resources",
                fileName,
                extension,
                stopServices: true,
                cancellationToken: cts.Token));

            // Assert
            Assert.False(result);
            Assert.False(File.Exists(Path.Combine(TempDirectory, $"{fileName}.{extension}")));

            // The #1817 wording: an aborted copy must never be reported as copied.
            Assert.Contains("was NOT copied to", textLogOutput);
            Assert.DoesNotContain("was successfully copied to", textLogOutput);
            Assert.Contains("restart boom", textLogOutput);
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
                // The cancellation check before the process-termination step is not gated on stopServices,
                // so a pre-cancelled token reaches it even with stopServices: false. LogCapture routes the
                // static Logger into a private temp directory so the arm that ran can be read back.
                var (result, textLogOutput) = await LogCapture.RunAsync(() => _resourceHelper.CopyEmbeddedResourceAsync(
                    _mockAssembly.Object,
                    "Servy.Resources",
                    fileName,
                    extension,
                    stopServices: false,
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
                stopServices: false,
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

            // Push the timestamp back relative to the host exe so ShouldCopyResource forces a replacement
            File.SetLastWriteTimeUtc(targetPath, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));

            var dummyResourceBytes = new byte[] { 0x01, 0x02, 0x03 };
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>()))
                         .Returns(() => new MemoryStream(dummyResourceBytes));

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _mockAssembly.Object, "Servy.Resources", fileName, extension, stopServices: false, cancellationToken: TestContext.Current.CancellationToken);

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

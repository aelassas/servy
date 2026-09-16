using Moq;
using Servy.Core.Helpers;
using Servy.Testing;
using System.Reflection;

namespace Servy.Core.IntegrationTests.Helpers
{
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
            _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Never);
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

                // VERIFICATION GUARD: Lock in proof that the underlying mock process killer was explicitly evaluated
                // before the helper marked the copy pass execution as a failure state.
                _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Once);
            }
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenResourceStreamNotFound_ReturnsFalse()
        {
            // Arrange
            _mockProcessKiller.Setup(p => p.KillProcessesUsingFile(It.IsAny<string>())).Returns(true);
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>())).Returns((Stream?)null); // Simulate missing resource

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _mockAssembly.Object, "Servy.Resources", "missingapp", "exe", stopServices: false, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CopyEmbeddedResource_Success_WritesFileToDisk()
        {
            // Arrange
            string fileName = "validapp";
            string extension = "dll";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");

            _mockProcessKiller.Setup(p => p.KillProcessesUsingFile(It.IsAny<string>())).Returns(true);

            // Provide a real memory stream with dummy data
            var dummyData = new byte[] { 0x01, 0x02, 0x03 };
            var memoryStream = new MemoryStream(dummyData);
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>())).Returns(memoryStream);

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _mockAssembly.Object, "Servy.Resources", fileName, extension, stopServices: false, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(targetPath));
            var writtenBytes = File.ReadAllBytes(targetPath);
            Assert.Equal(dummyData, writtenBytes);
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

            // Configure the process killer mock to return true for file handle clearing
            _mockProcessKiller.Setup(p => p.KillProcessesUsingFile(targetPath)).Returns(true);

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

            // 2. VERIFICATION LOOP: Confirm the service management pipeline executed gracefully in order
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
        public void CopyEmbeddedResourceForceSync_WhenResourceIsUpToDate_ReturnsTrueAndSkipsCopy()
        {
            // Arrange
            string fileName = "sync_up_to_date";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");

            // Create a file and artificially push its LastWriteTime into the future to bypass the staleness threshold
            File.WriteAllText(targetPath, "up to date sync content");
            File.SetLastWriteTimeUtc(targetPath, DateTime.UtcNow.AddHours(1));

            // Act
            bool result = _resourceHelper.CopyEmbeddedResourceForceSync(
                _mockAssembly.Object, "Servy.Resources", fileName, extension);

            // Assert
            Assert.True(result); // Should return true early
            _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void CopyEmbeddedResourceForceSync_WhenProcessTerminationFails_ReturnsFalse()
        {
            // Arrange
            string fileName = "sync_lockedapp";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");

            File.WriteAllText(targetPath, "existing target");
            DateTime hostExeTime = _resourceHelper.GetHostProcessLastWriteTimeUtc();
            File.SetLastWriteTimeUtc(targetPath, hostExeTime.AddDays(-1));

            _mockProcessKiller.Setup(p => p.KillProcessesUsingFile(It.IsAny<string>())).Returns(false);

            var dummyResourceBytes = new byte[] { 0x05, 0x06, 0x07 };
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>()))
                         .Returns(() => new MemoryStream(dummyResourceBytes));

            using (var lockStream = new FileStream(targetPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act
                bool result = _resourceHelper.CopyEmbeddedResourceForceSync(
                    _mockAssembly.Object, "Servy.Resources", fileName, extension);

                // Assert
                Assert.False(result);
                _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Once);
            }
        }

        [Fact]
        public void CopyEmbeddedResourceForceSync_WhenResourceStreamNotFound_ReturnsFalse()
        {
            // Arrange
            _mockProcessKiller.Setup(p => p.KillProcessesUsingFile(It.IsAny<string>())).Returns(true);
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>())).Returns((Stream?)null); // Simulate missing resource

            // Act
            bool result = _resourceHelper.CopyEmbeddedResourceForceSync(
                _mockAssembly.Object, "Servy.Resources", "sync_missingapp", "exe");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CopyEmbeddedResourceForceSync_Success_WritesFileToDisk()
        {
            // Arrange
            string fileName = "syncapp";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");

            _mockProcessKiller.Setup(p => p.KillProcessesUsingFile(It.IsAny<string>())).Returns(true);

            var dummyData = new byte[] { 0x0A, 0x0B, 0x0C };
            var memoryStream = new MemoryStream(dummyData);
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>())).Returns(memoryStream);

            // Act
            bool result = _resourceHelper.CopyEmbeddedResourceForceSync(
                _mockAssembly.Object, "Servy.Resources", fileName, extension);

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(targetPath));
            Assert.Equal(dummyData, File.ReadAllBytes(targetPath));
        }

        [Fact]
        public void CopyEmbeddedResourceForceSync_ThrowsException_CaughtByOuterCatch_ReturnsFalse()
        {
            // Arrange - Force null ref to hit the catch block
            Assembly nullAssembly = null!;

            // Act
            bool result = _resourceHelper.CopyEmbeddedResourceForceSync(
                nullAssembly, "Servy.Resources", "crashapp", "exe");

            // Assert
            Assert.False(result);
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

            _mockProcessKiller.Setup(p => p.KillProcessesUsingFile(It.IsAny<string>())).Returns(true);
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>()))
                         .Returns(() => new MemoryStream(new byte[] { 0x01 }));
            _mockServiceHelper.Setup(s => s.GetRunningServyUIServices()).Returns(testServices);
            _mockServiceHelper.Setup(s => s.StopServicesAsync(testServices, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockServiceHelper.Setup(s => s.StartServicesAsync(testServices, It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new InvalidOperationException("restart boom"));

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _mockAssembly.Object,
                "Servy.Resources",
                fileName,
                extension,
                stopServices: true,
                cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            // The restart failure is logged inside the finally block, never rethrown, so the copy's own
            // outcome is what the method returns.
            Assert.True(result);
            Assert.True(File.Exists(Path.Combine(TempDirectory, $"{fileName}.{extension}")));
            _mockServiceHelper.Verify(s => s.StartServicesAsync(testServices, CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenCancelledBeforeTermination_ReturnsFalse()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>()))
                         .Returns(() => new MemoryStream(new byte[] { 0x01 }));

            // Act
            // The cancellation check before the process-termination step is not gated on stopServices,
            // so a pre-cancelled token reaches it even with stopServices: false.
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _mockAssembly.Object,
                "Servy.Resources",
                "cancelapp",
                "exe",
                stopServices: false,
                cancellationToken: cts.Token);

            // Assert: the OperationCanceledException arm of the outer catch, not the general one
            Assert.False(result);
            _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Never);
        }

        #endregion
    }
}

using Moq;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Core.IntegrationTests.Helpers
{
    public class ResourceHelperIntegrationTests : TempDirectoryTestBase
    {
        private readonly Mock<IServiceHelper> _mockServiceHelper;
        private readonly Mock<IProcessKiller> _mockProcessKiller;
        private readonly FakeAssembly _fakeAssembly;
        private readonly ResourceHelper _resourceHelper;

        /// <summary>
        /// A custom implementation of Assembly to bypass Moq's ISerializable limitation in .NET 4.8.
        /// This allows us to control the embedded streams without Castle.Core proxy errors.
        /// </summary>
        private class FakeAssembly : Assembly
        {
            public Func<string, Stream> OnGetManifestResourceStream { get; set; }

            public override Stream GetManifestResourceStream(string name)
            {
                return OnGetManifestResourceStream?.Invoke(name);
            }
        }

        public ResourceHelperIntegrationTests()
        {
            _mockServiceHelper = new Mock<IServiceHelper>();
            _mockProcessKiller = new Mock<IProcessKiller>();

            _fakeAssembly = new FakeAssembly();

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

        #region TerminateBlockingProcesses Unit Tests

        [Fact]
        public void TerminateBlockingProcesses_ExeExtension_InvokesKillProcessTreeAndParents()
        {
            // Arrange
            string fileName = "app.exe";
            string filePath = Path.Combine(TempDirectory, fileName);
            _mockProcessKiller.Setup(p => p.KillProcessTreeAndParents(fileName, It.IsAny<bool>())).Returns(true);

            // Act
            bool result = _resourceHelper.TerminateBlockingProcesses("exe", fileName, filePath);

            // Assert
            Assert.True(result);
            _mockProcessKiller.Verify(p => p.KillProcessTreeAndParents(fileName, It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public void TerminateBlockingProcesses_DllExtension_WhenUnlocked_SkipsProcessKiller()
        {
            // Arrange
            string fileName = "library.dll";
            string filePath = Path.Combine(TempDirectory, fileName);
            File.WriteAllText(filePath, "unlocked dll");

            // Act
            bool result = _resourceHelper.TerminateBlockingProcesses("dll", fileName, filePath);

            // Assert
            Assert.True(result);
            _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void TerminateBlockingProcesses_DllExtension_WhenLocked_InvokesKillProcessesUsingFile()
        {
            // Arrange
            string fileName = "locked_library.dll";
            string filePath = Path.Combine(TempDirectory, fileName);
            File.WriteAllText(filePath, "locked dll");

            _mockProcessKiller.Setup(p => p.KillProcessesUsingFile(filePath)).Returns(true);

            using (var lockStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act
                bool result = _resourceHelper.TerminateBlockingProcesses("dll", fileName, filePath);

                // Assert
                Assert.True(result);
                _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(filePath), Times.Once);
            }
        }

        [Fact]
        public void TerminateBlockingProcesses_DllExtension_WhenSkipDllIsTrue_SkipsProcessKillerEvenIfLocked()
        {
            // Arrange
            string fileName = "skipped_library.dll";
            string filePath = Path.Combine(TempDirectory, fileName);
            File.WriteAllText(filePath, "locked dll");

            using (var lockStream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act
                bool result = _resourceHelper.TerminateBlockingProcesses("dll", fileName, filePath, skipDll: true);

                // Assert
                Assert.True(result);
                _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Never);
            }
        }

        #endregion

        #region Single Resource Copy Tests (Async & Sync)

        [Fact]
        public async Task CopyEmbeddedResource_WhenResourceIsUpToDate_ReturnsTrueAndSkipsCopy()
        {
            // Arrange
            string fileName = "testapp";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");

            // Create a file and anchor timestamp to hostExeTime + 5 minutes so it is within up-to-date window without triggering downgrade warning
            File.WriteAllText(targetPath, "old content");
            DateTime hostExeTime = _resourceHelper.GetHostProcessLastWriteTimeUtc();
            File.SetLastWriteTimeUtc(targetPath, hostExeTime.AddMinutes(5));

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _fakeAssembly, "Servy.Resources", fileName, extension, stopServices: false);

            // Assert
            Assert.True(result); // Should return true early without copying
            _mockProcessKiller.Verify(p => p.KillProcessTreeAndParents(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task CopyEmbeddedResource_WithSubfolder_CreatesSubfolderAndWritesFile()
        {
            // Arrange
            // Note: We use a non-existent file name so ShouldCopyResource always returns true
            string fileName = "test_artifact_" + Guid.NewGuid();
            string extension = "dll";
            string subfolder = "Modules";

            // Use a valid resource name that actually exists in the real assembly for extraction,
            // or use a dummy name if you only want to test the failure paths.
            string resourceNamespace = "Servy.Core";

            // Setup the mock to match the 1-parameter signature used in the .NET 4.8 source
            _mockProcessKiller
                .Setup(p => p.KillProcessesUsingFile(It.IsAny<string>()))
                .Returns(true);

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _fakeAssembly,
                resourceNamespace,
                fileName,
                extension,
                stopServices: false,
                subfolder: subfolder);

            // Assert
            // result will be false if the resourceNamespace + fileName does not exist in the real assembly
            // To make this pass, ensure 'resourceNamespace' and 'fileName' match an actual embedded resource.
            Assert.False(result, "Expected false because the dummy resource does not exist in the real assembly.");
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenProcessTerminationFails_ReturnsFalse()
        {
            // Arrange (exe routes to KillProcessTreeAndParents)
            string fileName = "lockedapp";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");

            File.WriteAllText(targetPath, "existing file");

            // Retrieve host executable time and set the existing target file to be older than hostExeWriteTime - 20 minutes
            DateTime hostExeTime = _resourceHelper.GetHostProcessLastWriteTimeUtc();
            File.SetLastWriteTimeUtc(targetPath, hostExeTime.AddDays(-1));

            _mockProcessKiller
                .Setup(p => p.KillProcessTreeAndParents($"{fileName}.{extension}", It.IsAny<bool>()))
                .Returns(false);

            _fakeAssembly.OnGetManifestResourceStream = _ => new MemoryStream(new byte[] { 0x01 });

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _fakeAssembly, "Servy.Core.Resources", fileName, extension, stopServices: false);

            // Assert
            Assert.False(result);
            _mockProcessKiller.Verify(p => p.KillProcessTreeAndParents($"{fileName}.{extension}", It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenStopServicesIsTrue_StopsAndRestartsDependentServices()
        {
            // Arrange
            string fileName = "serviceapp";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");
            var testServices = new List<string> { "Servy_Service_A", "Servy_Service_B" };

            // Configure the process killer mock to return true for process tree clearing (matching .NET 4.8 method signature)
            _mockProcessKiller.Setup(p => p.KillProcessTreeAndParents($"{fileName}.{extension}", It.IsAny<bool>())).Returns(true);

            // Setup our fake assembly abstraction to yield a valid, populated stream to pass upfront validation checks
            var dummyResourceBytes = new byte[] { 0xAA, 0xBB, 0xCC };
            _fakeAssembly.OnGetManifestResourceStream = name => new MemoryStream(dummyResourceBytes);

            // Setup the service helper to discover our running mock services
            _mockServiceHelper.Setup(s => s.GetRunningServyServices()).Returns(testServices);

            // Mock the lifecycle control methods to return successful completed tasks
            _mockServiceHelper.Setup(s => s.StopServicesAsync(testServices, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockServiceHelper.Setup(s => s.StartServicesAsync(testServices, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _fakeAssembly,
                "Servy.Resources",
                fileName,
                extension,
                stopServices: true,
                cancellationToken: CancellationToken.None);

            // Assert
            // 1. Verify the core copy transaction reported a success state
            Assert.True(result);
            Assert.True(File.Exists(targetPath));

            // 2. VERIFICATION LOOP: Confirm the service management pipeline executed gracefully in order
            _mockServiceHelper.Verify(s => s.GetRunningServyServices(), Times.Once);

            // 3. Confirm that the targeted services were both cleanly stopped and subsequently revived
            _mockServiceHelper.Verify(s => s.StopServicesAsync(testServices, CancellationToken.None), Times.Once);

            // The CancellationToken.None below is deliberate, not an oversight: ResourceHelper restarts
            // with None on purpose, so an upfront pipeline cancellation cannot leave the services it
            // stopped orphaned. Do not "fix" this to a caller-supplied token.
            _mockServiceHelper.Verify(s => s.StartServicesAsync(testServices, CancellationToken.None), Times.Once,
                "StartServicesAsync must be called with CancellationToken.None so a cancelled copy still restarts the services it stopped.");
        }

        [Fact]
        public void CopyEmbeddedResourceForceSync_WhenResourceIsUpToDate_ReturnsTrueAndSkipsCopy()
        {
            // Arrange
            string fileName = "sync_up_to_date";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");

            // Create a file and anchor timestamp to hostExeTime + 5 minutes so it is within up-to-date window without triggering downgrade warning
            File.WriteAllText(targetPath, "up to date sync content");
            DateTime hostExeTime = _resourceHelper.GetHostProcessLastWriteTimeUtc();
            File.SetLastWriteTimeUtc(targetPath, hostExeTime.AddMinutes(5));

            // Act
            bool result = _resourceHelper.CopyEmbeddedResourceForceSync(
                _fakeAssembly, "Servy.Resources", fileName, extension);

            // Assert
            Assert.True(result); // Should return true early without copying
            _mockProcessKiller.Verify(p => p.KillProcessTreeAndParents(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public void CopyEmbeddedResourceForceSync_WhenProcessTerminationFails_ReturnsFalse()
        {
            // Arrange
            string fileName = "sync_lockedapp";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");

            File.WriteAllText(targetPath, "existing target");

            // Anchor timestamp to hostExeTime so TryPrepareExtraction evaluates the file as stale
            DateTime hostExeTime = _resourceHelper.GetHostProcessLastWriteTimeUtc();
            File.SetLastWriteTimeUtc(targetPath, hostExeTime.AddDays(-1));

            _mockProcessKiller
                .Setup(p => p.KillProcessTreeAndParents($"{fileName}.exe", It.IsAny<bool>()))
                .Returns(false);

            var dummyResourceBytes = new byte[] { 0x01, 0x02, 0x03 };
            _fakeAssembly.OnGetManifestResourceStream = _ => new MemoryStream(dummyResourceBytes);

            // Act
            bool result = _resourceHelper.CopyEmbeddedResourceForceSync(
                _fakeAssembly, "Servy.Core.Resources", fileName, "exe");

            // Assert
            Assert.False(result);
            _mockProcessKiller.Verify(p => p.KillProcessTreeAndParents($"{fileName}.exe", It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public void CopyEmbeddedResourceForceSync_WhenResourceStreamNotFound_ReturnsFalse()
        {
            // Arrange
            _mockProcessKiller.Setup(p => p.KillProcessTreeAndParents(It.IsAny<string>(), It.IsAny<bool>())).Returns(true);
            _fakeAssembly.OnGetManifestResourceStream = _ => null; // Simulate missing resource

            // Act
            bool result = _resourceHelper.CopyEmbeddedResourceForceSync(
                _fakeAssembly, "Servy.Resources", "missingapp", "exe");

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Batch CopyResources Tests

        [Fact]
        public async Task CopyResources_AllResourcesUpToDate_ReturnsTrueEarly()
        {
            // Arrange
            var items = new List<ResourceItem>
            {
                new ResourceItem { FileNameWithoutExtension = "app1", Extension = "exe" },
                new ResourceItem { FileNameWithoutExtension = "lib1", Extension = "dll" }
            };

            DateTime hostExeTime = _resourceHelper.GetHostProcessLastWriteTimeUtc();

            foreach (var item in items)
            {
                var path = Path.Combine(TempDirectory, $"{item.FileNameWithoutExtension}.{item.Extension}");
                File.WriteAllText(path, "content");
                File.SetLastWriteTimeUtc(path, hostExeTime.AddMinutes(5));
            }

            // Act
            bool result = await _resourceHelper.CopyResources(_fakeAssembly, "Servy.Resources", items, stopServices: false);

            // Assert
            Assert.True(result);
            Assert.DoesNotContain(items, i => i.ShouldCopy);
        }

        [Fact]
        public async Task CopyResources_Success_ProcessesItemsAndRespectsSkipDllLogic()
        {
            // Arrange
            var items = new List<ResourceItem>
            {
                new ResourceItem { FileNameWithoutExtension = "main", Extension = "exe" },
                new ResourceItem { FileNameWithoutExtension = "helper", Extension = "dll" }
            };

            // Setup Mocks
            _mockProcessKiller.Setup(p => p.KillProcessTreeAndParents(It.IsAny<string>(), It.IsAny<bool>())).Returns(true);
            // We do NOT setup KillProcessesUsingFile to return true, because skipDll = true in the batch method should bypass it

            _fakeAssembly.OnGetManifestResourceStream = _ => new MemoryStream(new byte[] { 0xFF });

            // Act
            bool result = await _resourceHelper.CopyResources(_fakeAssembly, "Servy.Resources", items, stopServices: false);

            // Assert
            Assert.True(result);

            // Verify .exe trigger
            _mockProcessKiller.Verify(p => p.KillProcessTreeAndParents("main.exe", It.IsAny<bool>()), Times.Once);

            // Verify skipDll logic works (should never attempt to kill individual DLL files in batch mode)
            _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Never);

            Assert.True(File.Exists(Path.Combine(TempDirectory, "main.exe")));
            Assert.True(File.Exists(Path.Combine(TempDirectory, "helper.dll")));
        }

        [Fact]
        public async Task CopyResources_OneItemFailsTermination_ContinuesAndReturnsFalse()
        {
            // Arrange: Use the Integration Test assembly which contains the resource
            var assembly = typeof(Testing.Helper).Assembly;

            // 1. Explicitly define resource details based on the error message
            string detectedNamespace = "Servy.Testing.Resources";
            string detectedFileName = "handle64";
            string detectedExtension = "exe";

            // 2. FORCE EXTRACTION: Pre-calculate the shadow-copy target path and delete it.
            // This ensures ShouldCopyResource returns true.
            string targetDir = Path.GetDirectoryName(assembly.Location);
            string expectedTargetPath = Path.Combine(targetDir, $"{detectedFileName}.{detectedExtension}");

            if (File.Exists(expectedTargetPath))
            {
                try { File.Delete(expectedTargetPath); } catch { /* Handle locked files */ }
            }

            var items = new List<ResourceItem>
            {
                // Item 1: Will fail termination to trigger the 'res = false' path
                new ResourceItem { FileNameWithoutExtension = "failapp", Extension = "exe" },
                // Item 2: The actual resource to be extracted
                new ResourceItem { FileNameWithoutExtension = detectedFileName, Extension = detectedExtension }
            };

            // 3. MOCK ALIGNMENT: Match the 1-parameter signature used in the .NET 4.8 build
            _mockProcessKiller
                .Setup(p => p.KillProcessTreeAndParents("failapp.exe", It.IsAny<bool>()))
                .Returns(false);

            _mockProcessKiller
                .Setup(p => p.KillProcessTreeAndParents($"{detectedFileName}.{detectedExtension}", It.IsAny<bool>()))
                .Returns(true);

            _mockProcessKiller
                .Setup(p => p.KillProcessesUsingFile(It.IsAny<string>()))
                .Returns(true);

            string itemTargetPath = Path.Combine(targetDir, $"{detectedFileName}.{detectedExtension}");

            if (File.Exists(itemTargetPath))
            {
                File.Delete(itemTargetPath); // Forces ShouldCopy to return true
            }

            // Act
            // Passing the correct namespace ensures GetManifestResourceStream finds the data.
            bool result = await _resourceHelper.CopyResources(assembly, detectedNamespace, items, stopServices: false);

            // Assert
            Assert.False(result); // Overall status should be false because the first item failed

            // Verify Item 2: The loop must have continued and successfully extracted the second file
            Assert.True(File.Exists(items[1].TargetPath),
                $"File was not created at: {items[1].TargetPath}. " +
                $"Shadow Copy Path: {targetDir}");

            // Clean up
            if (File.Exists(items[1].TargetPath)) File.Delete(items[1].TargetPath);
        }

        [Fact]
        public async Task CopyResources_CaughtOuterException_ReturnsFalse()
        {
            // Arrange
            var items = new List<ResourceItem> { new ResourceItem { FileNameWithoutExtension = "app", Extension = "exe" } };
            Assembly nullAssembly = null; // Will crash ShouldCopyResource and hit the catch(Exception ex) block safely

            // Act
            bool result = await _resourceHelper.CopyResources(nullAssembly, "Servy.Resources", items, stopServices: false);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Helper Method Tests

        [Fact]
        public void GetHostProcessLastWriteTimeUtc_ExecutesSuccessfullyAndReturnsValidDate()
        {
            // Arrange (Static execution pipeline framework proxy metrics context)

            // Act
            DateTime result = _resourceHelper.GetHostProcessLastWriteTimeUtc();

            // Assert
            Assert.True(result > DateTime.MinValue);
            Assert.True(result <= DateTime.UtcNow.AddMinutes(1));
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenStartServicesAsyncThrows_LogsAndStillReturnsCopyResult()
        {
            // Arrange
            string fileName = "restartfailapp";
            string extension = "exe";
            var testServices = new List<string> { "Servy_Service_A" };

            // exe routes to KillProcessTreeAndParents on this branch
            _mockProcessKiller.Setup(p => p.KillProcessTreeAndParents(It.IsAny<string>(), It.IsAny<bool>())).Returns(true);
            _fakeAssembly.OnGetManifestResourceStream = name => new MemoryStream(new byte[] { 0x01 });
            _mockServiceHelper.Setup(s => s.GetRunningServyServices()).Returns(testServices);
            _mockServiceHelper.Setup(s => s.StopServicesAsync(testServices, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockServiceHelper.Setup(s => s.StartServicesAsync(testServices, It.IsAny<CancellationToken>()))
                              .ThrowsAsync(new InvalidOperationException("restart boom"));

            // Act
            // LogCapture routes the static Logger into a private temp directory (the logDirectory
            // seam) so the restart-failure entry written inside the finally block can be read back,
            // without touching the product's own logs directory.
            var (result, textLogOutput) = await LogCapture.RunAsync(() => _resourceHelper.CopyEmbeddedResourceAsync(
                _fakeAssembly,
                "Servy.Resources",
                fileName,
                extension,
                stopServices: true,
                cancellationToken: CancellationToken.None));

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
        public async Task CopyEmbeddedResource_WhenCancelledBeforeTermination_ReturnsFalse()
        {
            // Arrange
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                _fakeAssembly.OnGetManifestResourceStream = name => new MemoryStream(new byte[] { 0x01 });

                // Act
                // The cancellation check before the process-termination step is not gated on stopServices,
                // so a pre-cancelled token reaches it even with stopServices: false.
                bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                    _fakeAssembly,
                    "Servy.Resources",
                    "cancelapp",
                    "exe",
                    stopServices: false,
                    cancellationToken: cts.Token);

                // Assert: the OperationCanceledException arm of the outer catch, not the general one
                Assert.False(result);
                _mockProcessKiller.Verify(p => p.KillProcessTreeAndParents(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            }
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenBaseExtractionDirectoryIsEmpty_ReturnsFalse()
        {
            // Arrange
            // Path.Combine("", "emptydirapp.exe") is the bare file name, whose Path.GetDirectoryName is
            // an empty string - the only input that reaches TryPrepareExtraction's parent-directory guard.
            _resourceHelper.BaseExtractionDirectory = string.Empty;

            _fakeAssembly.OnGetManifestResourceStream = name => new MemoryStream(new byte[] { 0x01 });

            // Let the exe process-kill step succeed, so a false result can only come from the guard
            // and not from TerminateBlockingProcesses' unconfigured default.
            _mockProcessKiller.Setup(p => p.KillProcessTreeAndParents(It.IsAny<string>(), It.IsAny<bool>())).Returns(true);

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _fakeAssembly,
                "Servy.Resources",
                "emptydirapp",
                "exe",
                stopServices: false);

            // Assert
            // The guard's IOException is caught by the method's own outer catch, so the observable
            // effect is a false result rather than a propagated exception.
            Assert.False(result);
        }

        #endregion
    }
}

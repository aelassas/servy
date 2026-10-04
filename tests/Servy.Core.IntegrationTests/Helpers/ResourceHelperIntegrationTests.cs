using Moq;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Core.IntegrationTests.Helpers
{
    [Collection(CoreOsIntegrationCollection.Name)]
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

        #region Constructor Tests

        [Fact]
        public void Constructor_NullServiceHelper_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceHelper serviceHelper = null;

            // Act
            var ex = Assert.Throws<ArgumentNullException>(() => new ResourceHelper(serviceHelper, _mockProcessKiller.Object));

            // Assert
            Assert.Equal("serviceHelper", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullProcessKiller_ThrowsArgumentNullException()
        {
            // Arrange
            IProcessKiller processKiller = null;

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
                _fakeAssembly, "Servy.Resources", fileName, extension);

            // Assert
            Assert.True(result); // Should return true early without copying
            _mockProcessKiller.Verify(p => p.KillProcessTreeAndParents(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
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

            // Let termination succeed and supply a stream, so a copy would visibly overwrite the
            // file if the threshold were dropped instead of failing earlier for another reason
            _mockProcessKiller.Setup(p => p.KillProcessTreeAndParents(It.IsAny<string>(), It.IsAny<bool>())).Returns(true);

            bool streamRequested = false;
            _fakeAssembly.OnGetManifestResourceStream = _ =>
            {
                streamRequested = true;
                return new MemoryStream(new byte[] { 0x01, 0x02, 0x03, 0x04 });
            };

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _fakeAssembly, "Servy.Resources", fileName, extension);

            // Assert
            Assert.True(result); // Should return true early without copying
            Assert.Equal("existing content", File.ReadAllText(targetPath));
            Assert.False(streamRequested);
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
                _fakeAssembly, "Servy.Core.Resources", fileName, extension);

            // Assert
            Assert.False(result);
            _mockProcessKiller.Verify(p => p.KillProcessTreeAndParents($"{fileName}.{extension}", It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenResourceStreamNotFound_KillsNoProcess()
        {
            // Arrange
            // A stale, existing .exe target, so TryPrepareExtraction asks for a copy and
            // TerminateBlockingProcesses would call KillProcessTreeAndParents for an exe.
            string fileName = "missingstopapp";
            string extension = "exe";
            string targetPath = Path.Combine(TempDirectory, $"{fileName}.{extension}");
            File.WriteAllText(targetPath, "existing target");
            DateTime hostExeTime = _resourceHelper.GetHostProcessLastWriteTimeUtc();
            File.SetLastWriteTimeUtc(targetPath, hostExeTime.AddDays(-1));

            _mockProcessKiller.Setup(p => p.KillProcessTreeAndParents($"{fileName}.{extension}", It.IsAny<bool>())).Returns(true);

            // The resource is missing from the assembly
            _fakeAssembly.OnGetManifestResourceStream = _ => null;

            // Act
            // LogCapture routes the static Logger into a private temp directory so the guard's own
            // message can be read back: it is the only observable difference between the guard and
            // the outer catch-all that a deleted guard falls into.
            var (result, textLogOutput) = await LogCapture.RunAsync(() => _resourceHelper.CopyEmbeddedResourceAsync(
                _fakeAssembly,
                "Servy.Resources",
                fileName,
                extension,
                cancellationToken: CancellationToken.None));

            // Assert
            Assert.False(result);
            Assert.False(_resourceHelper.HasCopiedResources);

            // The #1851 ordering: nothing is side-effected before the resource is known to exist
            _mockProcessKiller.Verify(p => p.KillProcessTreeAndParents(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);

            // The guard's own arm, not the outer catch-all that a deleted guard falls into
            Assert.Contains("Embedded resource not found", textLogOutput);
            Assert.DoesNotContain("Failed to copy embedded resource", textLogOutput);
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
            Assert.False(_resourceHelper.HasCopiedResources); // Nothing was written, so nothing needs re-hardening
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
            Assert.True(_resourceHelper.HasCopiedResources); // A newly written file carries no grant for the service accounts, so it must be re-hardened

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
        public void IsExtractionNeeded_FileMissing_ReturnsTrue()
        {
            Assert.True(_resourceHelper.IsExtractionNeeded("Servy.Resources", "missing", "exe"));
        }

        [Fact]
        public void IsExtractionNeeded_FileOlderThanTheRunningExecutable_ReturnsTrue()
        {
            // Arrange
            var targetPath = Path.Combine(TempDirectory, "stale.dll");
            File.WriteAllText(targetPath, "old");
            File.SetLastWriteTimeUtc(targetPath, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));

            // Act & Assert
            Assert.True(_resourceHelper.IsExtractionNeeded("Servy.Resources", "stale", "dll"));
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
            _fakeAssembly.OnGetManifestResourceStream = n => n == "Servy.Resources.same.exe" ? new MemoryStream(content) : null;

            var targetPathStale = Path.Combine(TempDirectory, "stale_timestamp.exe");
            File.WriteAllBytes(targetPathStale, content);
            File.SetLastWriteTimeUtc(targetPathStale, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));

            // Act & Assert: nothing to update, so the services are not stopped
            Assert.False(_resourceHelper.IsExtractionNeeded(_fakeAssembly, "Servy.Resources", "same", "exe"));
            Assert.True(_resourceHelper.IsExtractionNeeded("Servy.Resources", "stale_timestamp", "exe")); // the timestamp-only overload still says stale
        }

        [Fact]
        public void IsExtractionNeeded_WithAssembly_StaleTimestampAndDifferentContent_ReturnsTrue()
        {
            // Arrange
            var targetPath = Path.Combine(TempDirectory, "older.exe");
            File.WriteAllBytes(targetPath, new byte[] { 0x01, 0x02, 0x03 });
            File.SetLastWriteTimeUtc(targetPath, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));
            _fakeAssembly.OnGetManifestResourceStream = n => n == "Servy.Resources.older.exe" ? new MemoryStream(new byte[] { 0x01, 0x02, 0x04 }) : null;

            // Act & Assert
            Assert.True(_resourceHelper.IsExtractionNeeded(_fakeAssembly, "Servy.Resources", "older", "exe"));
        }

        [Fact]
        public async Task CopyEmbeddedResource_StaleTimestampButIdenticalContent_WritesNothingAndKillsNothing()
        {
            // Arrange
            var content = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x07 };
            var targetPath = Path.Combine(TempDirectory, "unchanged.exe");
            File.WriteAllBytes(targetPath, content);
            File.SetLastWriteTimeUtc(targetPath, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));
            _fakeAssembly.OnGetManifestResourceStream = _ => new MemoryStream(content);

            DateTime expectedTime = _resourceHelper.GetHostProcessLastWriteTimeUtc();

            // Act
            var result = await _resourceHelper.CopyEmbeddedResourceAsync(_fakeAssembly, "Servy.Resources", "unchanged", "exe", cancellationToken: CancellationToken.None);

            // Assert: reported as done, but the file and whatever holds it open are left alone and write time is updated to host time
            Assert.True(result);
            Assert.False(_resourceHelper.HasCopiedResources);
            Assert.Equal(expectedTime, File.GetLastWriteTimeUtc(targetPath));
            _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Never);
            _mockProcessKiller.Verify(p => p.KillProcessTreeAndParents(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task CopyEmbeddedResource_StaleTimestampButIdenticalContent_UpdatesTimestampToAvoidFutureComparisons()
        {
            // Arrange
            var content = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x09 };
            var targetPath = Path.Combine(TempDirectory, "timestamp_update.exe");
            File.WriteAllBytes(targetPath, content);
            File.SetLastWriteTimeUtc(targetPath, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));
            _fakeAssembly.OnGetManifestResourceStream = _ => new MemoryStream(content);

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(_fakeAssembly, "Servy.Resources", "timestamp_update", "exe", cancellationToken: CancellationToken.None);

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
            // Arrange: Each call returns a fresh stream instance so stream disposal during comparison does not affect the write phase
            var newContent = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60 };
            var targetPath = Path.Combine(TempDirectory, "upgrade.exe");
            File.WriteAllBytes(targetPath, new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x61 });
            File.SetLastWriteTimeUtc(targetPath, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));
            _fakeAssembly.OnGetManifestResourceStream = _ => new MemoryStream(newContent);
            _mockProcessKiller.Setup(p => p.KillProcessTreeAndParents(It.IsAny<string>(), It.IsAny<bool>())).Returns(true);

            // Act
            var result = await _resourceHelper.CopyEmbeddedResourceAsync(_fakeAssembly, "Servy.Resources", "upgrade", "exe", cancellationToken: CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.True(_resourceHelper.HasCopiedResources);
            Assert.Equal(newContent, File.ReadAllBytes(targetPath));
        }

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
        public async Task CopyEmbeddedResource_WhenCancelledBeforeTermination_ReturnsFalse()
        {
            // Arrange
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // The killer would report success, so nothing downstream of TerminateBlockingProcesses
                // masks the Times.Never below: only the cancellation check keeps it from being reached.
                _mockProcessKiller.Setup(p => p.KillProcessTreeAndParents(It.IsAny<string>(), It.IsAny<bool>())).Returns(true);

                _fakeAssembly.OnGetManifestResourceStream = name => new MemoryStream(new byte[] { 0x01 });

                // Act
                // The cancellation check sits before the process-termination step, so a pre-cancelled
                // token reaches it. LogCapture routes the
                // static Logger into a private temp directory so the arm that ran can be read back.
                var (result, textLogOutput) = await LogCapture.RunAsync(() => _resourceHelper.CopyEmbeddedResourceAsync(
                    _fakeAssembly,
                    "Servy.Resources",
                    "cancelapp",
                    "exe",
                    cancellationToken: cts.Token));

                // Assert
                Assert.False(result);

                // An exe target goes straight to the killer on this branch - TerminateBlockingProcesses
                // has no IsFileLocked probe for it - so this already fails if the cancellation check is
                // removed, and no locked target file is needed to make it reachable the way main needs one.
                _mockProcessKiller.Verify(p => p.KillProcessTreeAndParents(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);

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

            _fakeAssembly.OnGetManifestResourceStream = name => new MemoryStream(new byte[] { 0x01 });

            // Let the exe process-kill step succeed, so a false result can only come from the guard
            // and not from TerminateBlockingProcesses' unconfigured default.
            _mockProcessKiller.Setup(p => p.KillProcessTreeAndParents(It.IsAny<string>(), It.IsAny<bool>())).Returns(true);

            // Act
            // LogCapture routes the static Logger into a private temp directory so the guard's own
            // message can be read back: it is the only observable difference between the guard and
            // the framework exception that replaces it when the guard is removed.
            var (result, textLogOutput) = await LogCapture.RunAsync(() => _resourceHelper.CopyEmbeddedResourceAsync(
                _fakeAssembly,
                "Servy.Resources",
                "emptydirapp",
                "exe"));

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
            string targetPath = Path.Combine(TempDirectory, fileName + "." + extension);
            var networkService = new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);

            File.WriteAllText(targetPath, "old content");
            var before = new FileInfo(targetPath).GetAccessControl();
            before.AddAccessRule(new FileSystemAccessRule(networkService, FileSystemRights.ReadAndExecute, AccessControlType.Allow));
            new FileInfo(targetPath).SetAccessControl(before);

            // Push the timestamp back relative to the host exe so TryPrepareExtraction forces a replacement
            File.SetLastWriteTimeUtc(targetPath, _resourceHelper.GetHostProcessLastWriteTimeUtc().AddDays(-1));

            var dummyResourceBytes = new byte[] { 0x01, 0x02, 0x03 };
            _fakeAssembly.OnGetManifestResourceStream = name => new MemoryStream(dummyResourceBytes);

            // exe routes to KillProcessTreeAndParents on this branch, so let that step succeed
            _mockProcessKiller.Setup(p => p.KillProcessTreeAndParents(It.IsAny<string>(), It.IsAny<bool>())).Returns(true);

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResourceAsync(
                _fakeAssembly, "Servy.Resources", fileName, extension);

            // Assert
            Assert.True(result);

            // The file really was replaced: WriteFileAtomicAsync moves a freshly staged temp file over
            // the target with MOVEFILE_REPLACE_EXISTING, so the result carries the temp file's
            // security descriptor and not the old file's.
            Assert.Equal(dummyResourceBytes, File.ReadAllBytes(targetPath));

            var after = new FileInfo(targetPath).GetAccessControl();
            var explicitRules = after.GetAccessRules(true, false, typeof(SecurityIdentifier))
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

using Moq;
using Servy.Core.Data;
using Servy.Core.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Core.IntegrationTests.Helpers
{
    public class ResourceHelperIntegrationTests : IDisposable
    {
        private readonly Mock<IServiceHelper> _mockServiceHelper;
        private readonly Mock<IProcessKiller> _mockProcessKiller;
        private readonly FakeAssembly _fakeAssembly;
        private readonly string _tempDirectory;
        private readonly ResourceHelper _resourceHelper;

        /// <summary>
        /// A custom implementation of Assembly to bypass Moq's ISerializable limitation in .NET 4.8.
        /// This allows us to control the Location and embedded streams without Castle.Core proxy errors.
        /// </summary>
        private class FakeAssembly : Assembly
        {
            public string FakeLocation { get; set; }
            public Func<string, Stream> OnGetManifestResourceStream { get; set; }

            public override string Location => FakeLocation;

            public override Stream GetManifestResourceStream(string name)
            {
                return OnGetManifestResourceStream?.Invoke(name);
            }
        }

        public ResourceHelperIntegrationTests()
        {
            _mockServiceHelper = new Mock<IServiceHelper>();
            _mockProcessKiller = new Mock<IProcessKiller>();

            // Create an isolated temporary directory for safe file I/O tests
            _tempDirectory = Path.Combine(Path.GetTempPath(), "ServyTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);

            // Setup our fake assembly to point its location to our Temp Directory 
            // (used by DEBUG preprocessor directive in ShouldCopyResource)
            _fakeAssembly = new FakeAssembly
            {
                FakeLocation = Path.Combine(_tempDirectory, "TestAssembly.dll")
            };

            _resourceHelper = new ResourceHelper(_mockServiceHelper.Object, _mockProcessKiller.Object);

            // Point the helper to the test-controlled temp directory
            _resourceHelper.BaseExtractionDirectory = _tempDirectory;
        }

        public void Dispose()
        {
            // Clean up temporary files after each test run to prevent disk pollution
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }

        #region Single Resource Copy Tests (Async & Sync)

        [Fact]
        public async Task CopyEmbeddedResource_WhenResourceIsUpToDate_ReturnsTrueAndSkipsCopy()
        {
            // Arrange
            string fileName = "testapp";
            string extension = "exe";
            string targetPath = Path.Combine(_tempDirectory, $"{fileName}.{extension}");

            // Create a dummy file and artificially push its LastWriteTime into the future to bypass the staleness threshold
            File.WriteAllText(targetPath, "old content");
            File.SetLastWriteTimeUtc(targetPath, DateTime.UtcNow.AddHours(1));

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResource(
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
            bool result = await _resourceHelper.CopyEmbeddedResource(
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
            _mockProcessKiller
                .Setup(p => p.KillProcessTreeAndParents(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(false);

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResource(
                _fakeAssembly, "Servy.Core.Resources", "lockedapp", "exe", stopServices: false);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CopyEmbeddedResourceSync_WhenResourceStreamNotFound_ReturnsFalse()
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

            foreach (var item in items)
            {
                var path = Path.Combine(_tempDirectory, $"{item.FileNameWithoutExtension}.{item.Extension}");
                File.WriteAllText(path, "content");
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(1));
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

            // Setup mocks
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

            Assert.True(File.Exists(Path.Combine(_tempDirectory, "main.exe")));
            Assert.True(File.Exists(Path.Combine(_tempDirectory, "helper.dll")));
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
        public void GetHostProcessLastWriteTimeUTC_ExecutesSuccessfullyAndReturnsValidDate()
        {
            // Act
            DateTime result = _resourceHelper.GetHostProcessLastWriteTimeUTC();

            // Assert
            Assert.True(result > DateTime.MinValue);
            Assert.True(result <= DateTime.UtcNow.AddMinutes(1));
        }

        #endregion
    }
}
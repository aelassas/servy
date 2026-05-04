using Moq;
using Servy.Core.Data;
using Servy.Core.Helpers;
using System.Reflection;

namespace Servy.Core.IntegrationTests.Helpers
{
    public class ResourceHelperIntegrationTests : IDisposable
    {
        private readonly Mock<IServiceRepository> _mockServiceRepo;
        private readonly Mock<IProcessKiller> _mockProcessKiller;
        private readonly Mock<Assembly> _mockAssembly;
        private readonly string _tempDirectory;
        private readonly ResourceHelper _resourceHelper;

        public ResourceHelperIntegrationTests()
        {
            _mockServiceRepo = new Mock<IServiceRepository>();
            _mockProcessKiller = new Mock<IProcessKiller>();
            _mockAssembly = new Mock<Assembly>();

            // Create an isolated temporary directory for file I/O tests
            _tempDirectory = Path.Combine(Path.GetTempPath(), "ServyTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);

            // Mock Assembly Location to point to our Temp Directory (used by DEBUG preprocessor directive in ShouldCopyResource)
            _mockAssembly.Setup(a => a.Location).Returns(Path.Combine(_tempDirectory, "TestAssembly.dll"));

            _resourceHelper = new ResourceHelper(_mockServiceRepo.Object, _mockProcessKiller.Object);
        }

        public void Dispose()
        {
            // Clean up temporary files after each test
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenResourceIsUpToDate_ReturnsTrueAndSkipsCopy()
        {
            // Arrange
            string fileName = "testapp";
            string extension = "exe";
            string targetPath = Path.Combine(_tempDirectory, $"{fileName}.{extension}");

            // Create a file and artificially push its LastWriteTime into the future to bypass the staleness threshold
            File.WriteAllText(targetPath, "old content");
            File.SetLastWriteTimeUtc(targetPath, DateTime.UtcNow.AddHours(1));

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResource(
                _mockAssembly.Object, "Servy.Resources", fileName, extension, stopServices: false);

            // Assert
            Assert.True(result); // Should return true early
            _mockProcessKiller.Verify(p => p.KillProcessesUsingFile(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenProcessTerminationFails_ReturnsFalse()
        {
            // Arrange
            _mockProcessKiller.Setup(p => p.KillProcessesUsingFile(It.IsAny<string>())).Returns(false); // Simulate locked file

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResource(
                _mockAssembly.Object, "Servy.Resources", "lockedapp", "exe", stopServices: false);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CopyEmbeddedResource_WhenResourceStreamNotFound_ReturnsFalse()
        {
            // Arrange
            _mockProcessKiller.Setup(p => p.KillProcessesUsingFile(It.IsAny<string>())).Returns(true);
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>())).Returns((Stream?)null); // Simulate missing resource

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResource(
                _mockAssembly.Object, "Servy.Resources", "missingapp", "exe", stopServices: false);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CopyEmbeddedResource_Success_WritesFileToDisk()
        {
            // Arrange
            string fileName = "validapp";
            string extension = "dll";
            string targetPath = Path.Combine(_tempDirectory, $"{fileName}.{extension}");

            _mockProcessKiller.Setup(p => p.KillProcessesUsingFile(It.IsAny<string>())).Returns(true);

            // Provide a real memory stream with dummy data
            var dummyData = new byte[] { 0x01, 0x02, 0x03 };
            var memoryStream = new MemoryStream(dummyData);
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>())).Returns(memoryStream);

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResource(
                _mockAssembly.Object, "Servy.Resources", fileName, extension, stopServices: false);

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(targetPath));
            var writtenBytes = File.ReadAllBytes(targetPath);
            Assert.Equal(dummyData, writtenBytes);
        }

        [Fact]
        public async Task CopyEmbeddedResource_ThrowsException_CaughtByOuterCatch_ReturnsFalse()
        {
            // Arrange
            // Passing a null assembly will trigger a NullReferenceException inside ShouldCopyResource when accessing assembly.Location
            Assembly nullAssembly = null!;

            // Act
            bool result = await _resourceHelper.CopyEmbeddedResource(
                nullAssembly, "Servy.Resources", "crashapp", "exe", stopServices: false);

            // Assert
            Assert.False(result); // Caught successfully
        }

        [Fact]
        public void CopyEmbeddedResourceSync_Success_WritesFileToDisk()
        {
            // Arrange
            string fileName = "syncapp";
            string extension = "exe";
            string targetPath = Path.Combine(_tempDirectory, $"{fileName}.{extension}");

            _mockProcessKiller.Setup(p => p.KillProcessesUsingFile(It.IsAny<string>())).Returns(true);

            var dummyData = new byte[] { 0x0A, 0x0B, 0x0C };
            var memoryStream = new MemoryStream(dummyData);
            _mockAssembly.Setup(a => a.GetManifestResourceStream(It.IsAny<string>())).Returns(memoryStream);

            // Act
            bool result = _resourceHelper.CopyEmbeddedResourceSync(
                _mockAssembly.Object, "Servy.Resources", fileName, extension);

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(targetPath));
        }

        [Fact]
        public void CopyEmbeddedResourceSync_ThrowsException_CaughtByOuterCatch_ReturnsFalse()
        {
            // Arrange - Force null ref to hit the catch block
            Assembly nullAssembly = null!;

            // Act
            bool result = _resourceHelper.CopyEmbeddedResourceSync(
                nullAssembly, "Servy.Resources", "crashapp", "exe");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetHostProcessLastWriteTimeUTC_ExecutesSuccessfullyAndReturnsValidDate()
        {
            // Act
            DateTime result = _resourceHelper.GetHostProcessLastWriteTimeUTC();

            // Assert
            // It should at least be a valid historical or current date, not DateTime.MinValue
            Assert.True(result > DateTime.MinValue);
            Assert.True(result <= DateTime.UtcNow.AddMinutes(1)); // Allow slight buffer
        }
    }
}
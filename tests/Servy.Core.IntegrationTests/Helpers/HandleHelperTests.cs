using Servy.Core.Helpers;
using System.Diagnostics;
using System.Reflection;

namespace Servy.Core.IntegrationTests.Helpers
{
    /// <summary>
    /// Integration tests for the HandleHelper class.
    /// These tests require handle.exe to be present and the runner to be elevated.
    /// </summary>
    public class HandleHelperIntegrationTests : IDisposable
    {
        private readonly string _handleExePath;
        private readonly List<string> _tempFiles = new List<string>();
        private readonly List<FileStream> _openedStreams = new List<FileStream>();

        /// <summary>
        /// Initializes the test class by ensuring handle64.exe is extracted from resources.
        /// </summary>
        public HandleHelperIntegrationTests()
        {
            // Setup the path in the test execution directory
            _handleExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "handle64.exe");

            ExtractHandleExe();

            if (!File.Exists(_handleExePath))
            {
                Debug.WriteLine($"WARNING: handle64.exe not found and extraction failed at {_handleExePath}");
            }
        }

        /// <summary>
        /// Extracts handle64.exe from the assembly's embedded resources to the base directory.
        /// </summary>
        private void ExtractHandleExe()
        {
            if (File.Exists(_handleExePath)) return;

            var assembly = Assembly.GetExecutingAssembly();
            // Resource names usually follow: ProjectNamespace.Folder.FileName.Extension
            // Update "Servy.Core.IntegrationTests" to match your actual test project namespace if different.
            string resourceName = "Servy.Core.IntegrationTests.Resources.handle64.exe";

            using (Stream? resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                {
                    // Fallback: try to find the resource by name if the full namespace path is unknown
                    var actualName = assembly.GetManifestResourceNames()
                        .FirstOrDefault(n => n.EndsWith("handle64.exe"));

                    if (actualName == null) return;

                    using (var fallbackStream = assembly.GetManifestResourceStream(actualName))
                    {
                        WriteResourceToDisk(fallbackStream!);
                    }
                }
                else
                {
                    WriteResourceToDisk(resourceStream);
                }
            }
        }

        private void WriteResourceToDisk(Stream stream)
        {
            using (FileStream fileStream = new FileStream(_handleExePath, FileMode.Create, FileAccess.Write))
            {
                stream.CopyTo(fileStream);
            }
        }

        /// <summary>
        /// Cleans up any open file streams, temporary files, and the extracted executable.
        /// </summary>
        public void Dispose()
        {
            foreach (var stream in _openedStreams)
            {
                stream.Close();
                stream.Dispose();
            }

            foreach (var file in _tempFiles)
            {
                if (File.Exists(file))
                {
                    try { File.Delete(file); } catch { /* Ignore cleanup errors */ }
                }
            }

            // Optional: Cleanup the extracted handle64.exe
            if (File.Exists(_handleExePath))
            {
                try { File.Delete(_handleExePath); } catch { /* Ignore cleanup errors */ }
            }
        }

        private string CreateTempFile()
        {
            string path = Path.Combine(Path.GetTempPath(), $"ServyTest_{Guid.NewGuid()}.tmp");
            File.WriteAllText(path, "Integration Test Content");
            _tempFiles.Add(path);
            return path;
        }

        [Fact]
        public void GetProcessesUsingFile_ShouldThrow_WhenPathsAreNullOrEmpty()
        {
            // Assert
            Assert.Throws<ArgumentException>(() => HandleHelper.GetProcessesUsingFile("", "C:\\test.txt"));
            Assert.Throws<ArgumentException>(() => HandleHelper.GetProcessesUsingFile("handle.exe", ""));
        }

        [Fact]
        public void GetProcessesUsingFile_ShouldReturnEmptyList_WhenNoProcessHoldsHandle()
        {
            // Arrange
            string testFile = CreateTempFile();

            // Act
            var results = HandleHelper.GetProcessesUsingFile(_handleExePath, testFile);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void GetProcessesUsingFile_ShouldDetectCurrentProcess_WhenCurrentProcessHoldsHandle()
        {
            // Arrange
            string testFile = CreateTempFile();
            int currentPid = Process.GetCurrentProcess().Id;
            string currentName = Process.GetCurrentProcess().ProcessName;

            // Lock the file
            using (var fs = new FileStream(testFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                _openedStreams.Add(fs); // Keep track for disposal

                // Act
                var results = HandleHelper.GetProcessesUsingFile(_handleExePath, testFile);

                // Assert
                Assert.NotEmpty(results);
                var selfMatch = results.FirstOrDefault(p => p.ProcessId == currentPid);

                Assert.NotNull(selfMatch);
                // handle.exe output might include .exe or not, HandleHelper trims whitespace.
                Assert.Contains(currentName, selfMatch.ProcessName, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void GetProcessesUsingFile_ShouldHandleInvalidHandleExePath()
        {
            // Arrange
            string testFile = CreateTempFile();
            // A path that is guaranteed not to exist
            string invalidExe = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_missing.exe");

            // Act & Assert
            // We expect Win32Exception because Process.Start throws when the file is not found
            // and UseShellExecute is set to false.
            Assert.Throws<System.ComponentModel.Win32Exception>(() =>
                HandleHelper.GetProcessesUsingFile(invalidExe, testFile));
        }

        [Fact]
        public void GetProcessesUsingFile_ShouldWorkWithMultipleHandles()
        {
            // Arrange
            string testFile = CreateTempFile();

            // Open multiple streams (this simulates multiple handles from the same or different threads)
            var fs1 = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var fs2 = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _openedStreams.Add(fs1);
            _openedStreams.Add(fs2);

            // Act
            var results = HandleHelper.GetProcessesUsingFile(_handleExePath, testFile);

            // Assert
            // handle.exe returns one line per handle found.
            Assert.True(results.Count >= 2, "Should have detected at least two handles.");
            Assert.All(results, r => Assert.Equal(Process.GetCurrentProcess().Id, r.ProcessId));
        }

        [Fact]
        public void GetProcessesUsingFile_ShouldRespectAsyncTimeout()
        {
            // Arrange
            string testFile = CreateTempFile();

            // Act
            var stopwatch = Stopwatch.StartNew();
            var results = HandleHelper.GetProcessesUsingFile(_handleExePath, testFile);
            stopwatch.Stop();

            // Assert
            Assert.NotNull(results);
            // Ensure we didn't block longer than the timeout if the exe is working
            Assert.True(stopwatch.ElapsedMilliseconds < 5000, "Normal execution should be faster than timeout.");
        }
    }
}
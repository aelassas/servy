using Servy.Core.IO;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Servy.Core.UnitTests.IO
{
    public class RotatingStreamWriterTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _logFilePath;

        public RotatingStreamWriterTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "RotatingStreamWriterTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDir);
            _logFilePath = Path.Combine(_testDir, "test.log");
        }

        [Fact]
        public void Constructor_InvalidPath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new RotatingStreamWriter(null, 100));
            Assert.Throws<ArgumentException>(() => new RotatingStreamWriter("", 100));
            Assert.Throws<ArgumentException>(() => new RotatingStreamWriter("   ", 100));
        }

        [Fact]
        public void Constructor_CreatesDirectoryIfNotExists()
        {
            var newDir = Path.Combine(_testDir, "newfolder");
            var newFile = Path.Combine(newDir, "file.log");

            Assert.False(Directory.Exists(newDir));

            using (var writer = new RotatingStreamWriter(newFile, 100))
            {
                // Just ensure no exception and directory created
            }

            Assert.True(Directory.Exists(newDir));
        }

        [Fact]
        public void Constructor_NoParentDirectory_DoesNotThrow()
        {
            // Arrange
            var newFile = "file.log";

            // Act
            var exception = Record.Exception(() =>
            {
                using (var writer = new RotatingStreamWriter(newFile, 100)) { }
            });

            // Assert
            Assert.Null(exception); // passes if no exception was thrown
        }

        [Fact]
        public void GenerateUniqueFileName_FileDoesNotExist_ReturnsOriginalPath()
        {
            var path = Path.Combine(_testDir, "log.txt");
            var result = InvokeGenerateUniqueFileName(path);
            Assert.Equal(path, result);
        }

        [Fact]
        public void GenerateUniqueFileName_FileExists_AppendsNumber()
        {
            var path = Path.Combine(_testDir, "log.txt");
            File.WriteAllText(path, "dummy"); // create first file

            var first = InvokeGenerateUniqueFileName(path);
            File.WriteAllText(first, "dummy"); // create second file

            var second = InvokeGenerateUniqueFileName(path);

            Assert.Equal(Path.Combine(_testDir, "log(2).txt"), second);
        }

        [Fact]
        public void Rotate_CreatesRotatedFileAndNewWriter()
        {
            var filePath = Path.Combine(_testDir, "rotate.txt");

            using (var writer = new RotatingStreamWriter(filePath, 5))
            {
                // Write something to trigger rotation
                writer.WriteLine("more"); // small write
                writer.WriteLine("12345"); // triggers rotation

                // Original file still exists (new empty log)
                Assert.True(File.Exists(filePath));

                // There is at least one rotated file
                var rotatedFiles = Directory.GetFiles(_testDir, "rotate.txt.*");
                Assert.NotEmpty(rotatedFiles);
            }

            // Find the latest rotated file by timestamp
            var latestRotatedFile = Directory.GetFiles(_testDir, "rotate.txt.*")
                    .Select(f => new FileInfo(f))
                    .Where(f => !f.Name.Equals("rotate.txt"))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();

            Assert.NotNull(latestRotatedFile); // make sure rotation happened

            // Read content of the latest rotated file
            var content = File.ReadAllText(latestRotatedFile.FullName);

            // Assert that it contains "more"
            Assert.Contains("more", content);
        }

        [Fact]
        public void Flush_WhenWriterIsNotNull_CallsUnderlyingFlush()
        {
            var filePath = Path.Combine(_testDir, "test.txt");

            using (var writer = new RotatingStreamWriter(filePath, 10))
            {
                writer.WriteLine("hello");

                // Just call Flush; should not throw and writes should be persisted
                writer.Flush();
            }

            // Check that content is actually written to file
            var content = File.ReadAllText(filePath);
            Assert.Equal("hello\r\n", content);
        }

        [Fact]
        public void Flush_WhenWriterIsNull_DoesNothing()
        {
            // Create writer and immediately dispose so _writer becomes null
            var writer = new RotatingStreamWriter(Path.Combine(_testDir, "test.txt"), 10);
            writer.Dispose(); // _writer is now null

            // Calling Flush should not throw
            var exception = Record.Exception(() => writer.Flush());
            Assert.Null(exception);
        }

        // Helper to invoke private static GenerateUniqueFileName via reflection
        private string InvokeGenerateUniqueFileName(string path)
        {
            var method = typeof(RotatingStreamWriter).GetMethod("GenerateUniqueFileName",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (string)method.Invoke(null, new object[] { path });
        }


        [Fact]
        public void Dispose_ClosesWriter()
        {
            var writer = new RotatingStreamWriter(_logFilePath, 1000);
            writer.WriteLine("Test line");
            writer.Dispose();

            Assert.Throws<NullReferenceException>(() => writer.WriteLine("Another line"));

            // Try dispose again to cover all branches of Dispose method
            writer.Dispose();
            Assert.Throws<NullReferenceException>(() => writer.WriteLine("Another line"));
        }

        [Fact]
        public void GenerateUniqueFileName_ReturnsNonExistingFileName()
        {
            var methodInfo = typeof(RotatingStreamWriter)
                .GetMethod("GenerateUniqueFileName", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(methodInfo);  // Ensure method exists to avoid null dereference

            using (var writer = new RotatingStreamWriter(_logFilePath, 1000))
            {

                var basePath = Path.Combine(_testDir, "file.log");

                File.WriteAllText(basePath, "test");
                File.WriteAllText(Path.Combine(_testDir, "file(1).log"), "test");
                File.WriteAllText(Path.Combine(_testDir, "file(2).log"), "test");

                var uniqueName = (string)methodInfo.Invoke(writer, new object[] { basePath });

                Assert.Equal(Path.Combine(_testDir, "file(3).log"), uniqueName);
            }
        }

        [Fact]
        public void WriteLine_DoesNotRotate_WhenRotationSizeZero()
        {
            var filePath = Path.Combine(_testDir, "zero.txt");

            using (var writer = new RotatingStreamWriter(filePath, 0))
            {
                writer.WriteLine("Hello"); // rotation size = 0, should not rotate
            }

            Assert.True(File.Exists(filePath));

            var rotatedFiles = Directory.GetFiles(_testDir, "zero.txt.*")
                    .Select(f => new FileInfo(f))
                    .Where(f => !f.Name.Equals("zero.txt"));
            Assert.Empty(rotatedFiles);
        }

        [Fact]
        public void WriteLine_DoesNotRotate_WhenFileSmallerThanRotationSize()
        {
            var filePath = Path.Combine(_testDir, "small.txt");

            using (var writer = new RotatingStreamWriter(filePath, 1024)) // 1 KB
            {
                writer.WriteLine("small"); // file < rotation size
            }

            Assert.True(File.Exists(filePath));

            var rotatedFiles = Directory.GetFiles(_testDir, "small.txt.*")
                              .Select(f => new FileInfo(f))
                              .Where(f => !f.Name.Equals("small.txt"));
            Assert.Empty(rotatedFiles);
        }

        [Fact]
        public void WriteLine_Rotates_WhenFileExceedsRotationSize()
        {
            var filePath = Path.Combine(_testDir, "rotate.txt");

            using (var writer = new RotatingStreamWriter(filePath, 5)) // tiny size
            {
                writer.WriteLine("12345"); // exactly 5 bytes → triggers rotation
                writer.Flush();

                writer.WriteLine("more"); // write to new file
                writer.Flush();
            }

            Assert.True(File.Exists(filePath));

            var rotatedFiles = Directory.GetFiles(_testDir, "rotate.txt.*");
            Assert.NotEmpty(rotatedFiles);
        }


        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }

                GC.SuppressFinalize(this);
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }
}

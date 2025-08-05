using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Xunit;

namespace Servy.Core.UnitTests
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
            string newDir = Path.Combine(_testDir, "newfolder");
            string newFile = Path.Combine(newDir, "file.log");

            Assert.False(Directory.Exists(newDir));

            using (var writer = new RotatingStreamWriter(newFile, 100))
            {
                // Just ensure no exception and directory created
            }

            Assert.True(Directory.Exists(newDir));
        }

        [Fact]
        public void WriteLine_WritesToFile()
        {
            using (var writer = new RotatingStreamWriter(_logFilePath, 1000))
            {
                writer.WriteLine("Line1");
                writer.WriteLine("Line2");
            }

            string[] lines = File.ReadAllLines(_logFilePath);
            Assert.Contains("Line1", lines);
            Assert.Contains("Line2", lines);
        }

        [Fact]
        public void WriteLine_RotatesFileWhenSizeExceeded()
        {
            const long rotationSize = 100; // small size for test

            using (var writer = new RotatingStreamWriter(_logFilePath, rotationSize))
            {
                for (int i = 0; i < 20; i++)
                {
                    writer.WriteLine(new string('x', 10));
                }
            }

            Thread.Sleep(100);

            var logFiles = Directory.GetFiles(_testDir, "test.log*").ToList();

            Assert.True(logFiles.Count >= 2, "Expected rotated and current log files.");

            var currentFileInfo = new FileInfo(_logFilePath);
            Assert.True(currentFileInfo.Length < rotationSize);
        }

        [Fact]
        public void Dispose_ClosesWriter()
        {
            var writer = new RotatingStreamWriter(_logFilePath, 1000);
            writer.WriteLine("Test line");
            writer.Dispose();

            Assert.Throws<ObjectDisposedException>(() => writer.WriteLine("Another line"));
        }

        [Fact]
        public void GenerateUniqueFileName_ReturnsNonExistingFileName()
        {
            var methodInfo = typeof(RotatingStreamWriter)
                .GetMethod("GenerateUniqueFileName", BindingFlags.NonPublic | BindingFlags.Instance);

            using (var writer = new RotatingStreamWriter(_logFilePath, 1000))
            {
                string basePath = Path.Combine(_testDir, "file.log");

                File.WriteAllText(basePath, "test");
                File.WriteAllText(Path.Combine(_testDir, "file(1).log"), "test");
                File.WriteAllText(Path.Combine(_testDir, "file(2).log"), "test");

                string uniqueName = (string)methodInfo.Invoke(writer, new object[] { basePath });

                Assert.Equal(Path.Combine(_testDir, "file(3).log"), uniqueName);
            }
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }
}

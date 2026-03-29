using Servy.Core.Enums;
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

        // Helper to construct per the new constructor signature
        private RotatingStreamWriter CreateWriter(
            string path,
            bool enableSizeRotation = true,
            long rotationSizeInBytes = 1024,
            bool enableDateRotation = false,
            DateRotationType dateRotationType = DateRotationType.Daily,
            int maxRotations = 0)
        {
            return new RotatingStreamWriter(
                path,
                enableSizeRotation,
                rotationSizeInBytes,
                enableDateRotation,
                dateRotationType,
                maxRotations);
        }

        [Fact]
        public void Constructor_InvalidPath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => CreateWriter(null, true, 100));
            Assert.Throws<ArgumentException>(() => CreateWriter("", true, 100));
            Assert.Throws<ArgumentException>(() => CreateWriter("   ", true, 100));
        }

        [Fact]
        public void Constructor_CreatesDirectoryIfNotExists()
        {
            var newDir = Path.Combine(_testDir, "newfolder");
            var newFile = Path.Combine(newDir, "file.log");

            Assert.False(Directory.Exists(newDir));

            using (var writer = CreateWriter(newFile, true, 100))
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
                using (var writer = CreateWriter(newFile, true, 100)) { }
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

            Assert.Equal(Path.Combine(_testDir, "log.(2).txt"), second);
        }

        [Fact]
        public void ReturnsOriginalPath_WhenFileDoesNotExist()
        {
            var path = Path.Combine(_testDir, "fresh_file.log");
            var result = InvokeGenerateUniqueFileName(path);
            Assert.Equal(path, result);
        }

        [Theory]
        [InlineData("App.20260325_001611.log", "App.20260325_001611.(1).log")] // Sandwich case
        [InlineData("App.20260325_001611", "App.20260325_001611.(1)")]         // Suffix case
        [InlineData("data.txt", "data.(1).txt")]                               // Standard file
        [InlineData("archive.123", "archive.(1).123")]                         // Short numeric ext (not timestamp)
        public void HandlesCollisions_BasedOnExtensionType(string fileName, string expectedName)
        {
            // Arrange
            var basePath = Path.Combine(_testDir, fileName);
            var expectedPath = Path.Combine(_testDir, expectedName);
            File.WriteAllText(basePath, "dummy"); // Create the collision

            // Act
            var result = InvokeGenerateUniqueFileName(basePath);

            // Assert
            Assert.Equal(expectedPath, result);
        }

        [Fact]
        public void IncrementsCounter_WhenMultipleCollisionsExist()
        {
            // Arrange
            var fileName = "App.20260325_001611.log";
            var basePath = Path.Combine(_testDir, fileName);

            // Create: .log AND .(1).log
            File.WriteAllText(basePath, "orig");
            File.WriteAllText(Path.Combine(_testDir, "App.20260325_001611.(1).log"), "coll1");

            var expectedPath = Path.Combine(_testDir, "App.20260325_001611.(2).log");

            // Act
            var result = InvokeGenerateUniqueFileName(basePath);

            // Assert
            Assert.Equal(expectedPath, result);
        }


        [Fact]
        public void Rotate_CreatesRotatedFileAndNewWriter()
        {
            var filePath = Path.Combine(_testDir, "rotate.txt");

            using (var writer = CreateWriter(filePath, true, 5, false, DateRotationType.Daily, 0))
            {
                // Write something to trigger rotation
                writer.WriteLine("more"); // small write
                writer.Write("12345"); // triggers rotation

                // Original file still exists (new empty log)
                Assert.True(File.Exists(filePath));

                // There is at least one rotated file
                var rotatedFiles = Directory.GetFiles(_testDir, "rotate.*.txt").Where(f => !f.EndsWith("rotate.txt")).ToList();
                Assert.NotEmpty(rotatedFiles);
                Assert.Contains(DateTime.Now.ToString("yyyyMMdd"), rotatedFiles[0]);
            }

            // Find the latest rotated file by timestamp
            var latestRotatedFile = Directory.GetFiles(_testDir, "rotate.*.txt")
                    .Select(f => new FileInfo(f))
                    .Where(f => !f.Name.Equals("rotate.txt"))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();

            Assert.NotNull(latestRotatedFile); // make sure rotation happened

            // Read content of the latest rotated file
            var content = File.ReadAllText(latestRotatedFile.FullName);

            // Assert that it contains "12345"
            Assert.Contains("12345", content);
        }

        [Fact]
        public void Flush_WhenWriterIsNotNull_CallsUnderlyingFlush()
        {
            var filePath = Path.Combine(_testDir, "test.txt");

            using (var writer = CreateWriter(filePath, true, 10))
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
            var writer = CreateWriter(Path.Combine(_testDir, "test.txt"), true, 10);
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
            var writer = CreateWriter(_logFilePath, true, 1000);
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

            using (var writer = CreateWriter(_logFilePath, true, 1000))
            {

                var basePath = Path.Combine(_testDir, "file.log");

                File.WriteAllText(basePath, "test");
                File.WriteAllText(Path.Combine(_testDir, "file.(1).log"), "test");
                File.WriteAllText(Path.Combine(_testDir, "file.(2).log"), "test");

                var uniqueName = (string)methodInfo.Invoke(writer, new object[] { basePath });

                Assert.Equal(Path.Combine(_testDir, "file.(3).log"), uniqueName);
            }
        }

        [Fact]
        public void WriteLine_DoesNotRotate_WhenRotationSizeZero()
        {
            var filePath = Path.Combine(_testDir, "zero.txt");

            using (var writer = CreateWriter(filePath, true, 0)) // enable size rotation but size==0 -> no size rotation
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

            using (var writer = CreateWriter(filePath, true, 1024)) // 1 KB
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
            var filePath = Path.Combine(_testDir, "rotate2.txt");

            using (var writer = CreateWriter(filePath, true, 5)) // tiny size
            {
                writer.WriteLine("12345"); // exactly 5 bytes -> triggers rotation
                writer.Flush();

                writer.WriteLine("more"); // write to new file
                writer.Flush();
            }

            Assert.True(File.Exists(filePath));

            var rotatedFiles = Directory.GetFiles(_testDir, "rotate2.txt.*");
            Assert.NotEmpty(rotatedFiles);
        }

        [Fact]
        public void EnforceMaxRotations_CoversAllBranches()
        {
            // Arrange
            string baseLog = Path.Combine(_testDir, "service.log");
            File.WriteAllText(baseLog, "base");

            var writer = CreateWriter(baseLog, true, 1000);
            var enforceMethod = typeof(RotatingStreamWriter).GetMethod("EnforceMaxRotations", BindingFlags.NonPublic | BindingFlags.Instance);
            var maxRotField = typeof(RotatingStreamWriter).GetField("_maxRotations", BindingFlags.NonPublic | BindingFlags.Instance);

            // ---- BRANCH 1: _maxRotations <= 0 ----
            maxRotField.SetValue(writer, 0);
            enforceMethod.Invoke(writer, null); // Coverage: return early

            // ---- BRANCH 2: Filter Logic (StartsWith and EndsWith) ----
            // f1: Valid rotated file
            string f1 = Path.Combine(_testDir, "service.20260325_000001.log");
            // noise1: Starts with "service" but NO DOT (e.g., service_backup.log) 
            // This hits the false side of: StartsWith($"{fileNameWithoutExt}.")
            string noise1 = Path.Combine(_testDir, "service_backup.log");
            // noise2: Starts with "service." but wrong extension (e.g., service.2026.txt)
            // This hits the false side of: EndsWith(extension)
            string noise2 = Path.Combine(_testDir, "service.20260325.txt");

            File.WriteAllText(f1, "valid");
            File.WriteAllText(noise1, "noise");
            File.WriteAllText(noise2, "noise");

            // ---- BRANCH 3: rotatedFiles.Count <= _maxRotations ----
            maxRotField.SetValue(writer, 5);
            enforceMethod.Invoke(writer, null);

            Assert.True(File.Exists(f1));
            Assert.True(File.Exists(noise1)); // Noise should never be deleted
            Assert.True(File.Exists(noise2));

            // ---- BRANCH 4: Deletion happens (rotatedFiles.Count > _maxRotations) ----
            maxRotField.SetValue(writer, 0); // Force deletion of all detected rotated files
                                             // Note: we can't set it to 0 because the method returns early if <= 0. 
                                             // So we use 1 and provide 2 valid files.
            string f2 = Path.Combine(_testDir, "service.20260325_000002.log");
            File.WriteAllText(f2, "valid2");
            File.SetLastWriteTimeUtc(f1, DateTime.UtcNow.AddMinutes(-10)); // Oldest
            File.SetLastWriteTimeUtc(f2, DateTime.UtcNow);               // Newest

            maxRotField.SetValue(writer, 1);
            enforceMethod.Invoke(writer, null);

            Assert.True(File.Exists(f2));     // Kept (newest)
            Assert.False(File.Exists(f1));    // Deleted (valid rotated)
            Assert.True(File.Exists(noise1)); // Kept (filter rejected it)
            Assert.True(File.Exists(noise2)); // Kept (filter rejected it)

            // ---- BRANCH 5: Deletion Failure (IOException) ----
            File.WriteAllText(f1, "recreate");
            File.SetLastWriteTimeUtc(f1, DateTime.UtcNow.AddMinutes(-10));
            using (var locked = new FileStream(f1, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var ex = Record.Exception(() => enforceMethod.Invoke(writer, null));
                Assert.Null(ex); // Resilience check
            }

            writer.Dispose();
        }

        [Fact]
        public void EnforceMaxRotations_NoExtension_CoversIsNullOrEmpty()
        {
            // Arrange: File with NO extension
            string baseLog = Path.Combine(_testDir, "plainfile");
            File.WriteAllText(baseLog, "base");

            var writer = CreateWriter(baseLog, true, 1000);
            var enforceMethod = typeof(RotatingStreamWriter).GetMethod("EnforceMaxRotations", BindingFlags.NonPublic | BindingFlags.Instance);
            var maxRotField = typeof(RotatingStreamWriter).GetField("_maxRotations", BindingFlags.NonPublic | BindingFlags.Instance);

            // Create a rotated file for a no-extension base
            // Pattern: plainfile.timestamp
            string f1 = Path.Combine(_testDir, "plainfile.20260325_000001");
            File.WriteAllText(f1, "rotated");
            File.SetLastWriteTimeUtc(f1, DateTime.UtcNow.AddMinutes(-10));

            // Act: Set max rotations to 0 (effectively 1 in logic since we skip)
            maxRotField.SetValue(writer, 1); // Keep 1

            // To trigger deletion, we need 2 files
            string f2 = Path.Combine(_testDir, "plainfile.20260325_000002");
            File.WriteAllText(f2, "rotated2");
            File.SetLastWriteTimeUtc(f2, DateTime.UtcNow);

            // This hits the (string.IsNullOrEmpty(extension)) branch!
            enforceMethod.Invoke(writer, null);

            Assert.True(File.Exists(f2));
            Assert.False(File.Exists(f1));

            writer.Dispose();
        }

        [Fact]
        public void EnforceMaxRotations_DeletionFails_DoesNotThrow()
        {
            var logPath = Path.Combine(_testDir, "service.log");
            File.WriteAllText(logPath, "current");

            var rotated1 = Path.Combine(_testDir, "service.log.1");
            File.WriteAllText(rotated1, "new");

            var rotated2 = Path.Combine(_testDir, "service.log.2");
            File.WriteAllText(rotated2, "old");

            File.SetLastWriteTimeUtc(rotated1, DateTime.Now.AddMinutes(0));
            File.SetLastWriteTimeUtc(rotated2, DateTime.Now.AddMinutes(-1));

            var writer = CreateWriter(logPath, true, 1, false, DateRotationType.Daily, 1); // keep 1

            // Make the rotated file read-only to simulate deletion failure
            File.SetAttributes(rotated2, FileAttributes.ReadOnly);

            var ex = Record.Exception(() =>
            {
                // Force cleanup directly via reflection
                var method = typeof(RotatingStreamWriter)
                    .GetMethod("EnforceMaxRotations", BindingFlags.NonPublic | BindingFlags.Instance);
                method.Invoke(writer, Array.Empty<object>());
            });

            Assert.Null(ex); // no exception should propagate
        }

        [Fact]
        public void DateRotation_Daily_Rotates_WhenDateBoundaryCrossed()
        {
            var filePath = Path.Combine(_testDir, "daily.log");
            using (var writer = CreateWriter(filePath, false, 0, true, DateRotationType.Daily, 0))
            {
                // Force last rotation to yesterday
                var lastField = typeof(RotatingStreamWriter).GetField("_lastRotationDate", BindingFlags.NonPublic | BindingFlags.Instance);
                lastField.SetValue(writer, DateTime.Now.AddDays(-1));

                writer.WriteLine("rotate on daily boundary");
                writer.Flush();
            }

            var rotated = Directory.GetFiles(_testDir, "daily.*.log").Where(f => !f.EndsWith("daily.log")).ToArray();
            Assert.NotEmpty(rotated);
            Assert.Contains(DateTime.Now.ToString("yyyyMMdd"), rotated[0]);
        }

        [Fact]
        public void DateRotation_Weekly_Rotates_WhenYearBoundaryCrossed()
        {
            var filePath = Path.Combine(_testDir, "weekly_year.log");
            using (var writer = CreateWriter(filePath, false, 0, true, DateRotationType.Weekly, 0))
            {
                // 1. Arrange: Set last rotation to Dec 31st of the previous year
                var lastYear = DateTime.Now.Year - 1;
                var dec31 = new DateTime(lastYear, 12, 31);

                var lastField = typeof(RotatingStreamWriter).GetField("_lastRotationDate", BindingFlags.NonPublic | BindingFlags.Instance);
                lastField.SetValue(writer, dec31);

                // 2. Act: This should trigger rotation because now.Year != 2025
                writer.WriteLine("rotate on year change");
                writer.Flush();
            }

            // 3. Assert
            var rotated = Directory.GetFiles(_testDir, "weekly_year.*.log")
                                   .Where(f => !f.EndsWith("weekly_year.log"))
                                   .ToArray();

            Assert.NotEmpty(rotated);
            // Verifies the branch now.Year != _lastRotationDate.Year was evaluated as true
        }

        [Fact]
        public void DateRotation_Weekly_DoesNotRotate_OnSameWeek()
        {
            var filePath = Path.Combine(_testDir, "weekly_same.log");
            // Ensure we aren't running this at 11:59 PM on a Sunday!
            var recently = DateTime.Now.AddHours(-1);

            using (var writer = CreateWriter(filePath, false, 0, true, DateRotationType.Weekly, 0))
            {
                var lastField = typeof(RotatingStreamWriter).GetField("_lastRotationDate", BindingFlags.NonPublic | BindingFlags.Instance);
                lastField.SetValue(writer, recently);

                writer.WriteLine("should not rotate");
                writer.Flush();
            }

            var rotated = Directory.GetFiles(_testDir, "weekly_same.*.log")
                                   .Where(f => !f.EndsWith("weekly_same.log"));
            Assert.Empty(rotated);
        }

        [Fact]
        public void DateRotation_Monthly_Rotates_WhenMonthBoundaryCrossed()
        {
            var filePath = Path.Combine(_testDir, "monthly.log");
            using (var writer = CreateWriter(filePath, false, 0, true, DateRotationType.Monthly, 0))
            {
                // Set last rotation to previous month
                var lastField = typeof(RotatingStreamWriter).GetField("_lastRotationDate", BindingFlags.NonPublic | BindingFlags.Instance);
                lastField.SetValue(writer, DateTime.Now.AddMonths(-1));

                writer.WriteLine("rotate on monthly boundary");
                writer.Flush();
            }

            var rotated = Directory.GetFiles(_testDir, "monthly.*.log").Where(f => !f.EndsWith("monthly.log")).ToArray();
            Assert.NotEmpty(rotated);
            Assert.Contains(DateTime.Now.ToString("yyyyMMdd"), rotated[0]);
        }

        [Fact]
        public void DateRotation_Monthly_Rotates_WhenYearBoundaryCrossed()
        {
            var filePath = Path.Combine(_testDir, "monthly.log");
            using (var writer = CreateWriter(filePath, false, 0, true, DateRotationType.Monthly, 0))
            {
                // Set last rotation to previous month
                var lastField = typeof(RotatingStreamWriter).GetField("_lastRotationDate", BindingFlags.NonPublic | BindingFlags.Instance);
                lastField.SetValue(writer, DateTime.Now.AddYears(-1));

                writer.WriteLine("rotate on monthly boundary");
                writer.Flush();
            }

            var rotated = Directory.GetFiles(_testDir, "monthly.*.log").Where(f => !f.EndsWith("monthly.log")).ToArray();
            Assert.NotEmpty(rotated);
            Assert.Contains(DateTime.Now.ToString("yyyyMMdd"), rotated[0]);
        }

        [Fact]
        public void SizeAndDateRotation_SizePrecedence_WhenBothEnabled()
        {
            var filePath = Path.Combine(_testDir, "sizeDate.log");

            // enable both rotations. small rotation size so size triggers.
            using (var writer = CreateWriter(filePath, true, 5, true, DateRotationType.Daily, 0))
            {
                // Set last rotation to yesterday to make date eligible too
                var lastField = typeof(RotatingStreamWriter).GetField("_lastRotationDate", BindingFlags.NonPublic | BindingFlags.Instance);
                lastField.SetValue(writer, DateTime.Now.AddDays(-1));

                // Write to exceed size threshold -> size rotation should happen and date rotation skipped for that write
                writer.Write("123456"); // >5
                writer.Flush();
            }

            var rotated = Directory.GetFiles(_testDir, "sizeDate.*.log").Where(f => !f.EndsWith("sizeDate.log")).ToArray();
            Assert.NotEmpty(rotated);

            // Read rotated content to ensure size-based content exists
            var latest = new DirectoryInfo(_testDir)
                .GetFiles("sizeDate.*.log")
                .Where(fi => !fi.Name.Equals("sizeDate.log"))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .First();
            Assert.Contains("123456", File.ReadAllText(latest.FullName));
        }

        [Fact]
        public void SizeAndDateRotation_DateWhenSizeNotExceeded()
        {
            var filePath = Path.Combine(_testDir, "dateOnlyWhenSizeNotExceeded.log");

            using (var writer = CreateWriter(filePath, true, 1024, true, DateRotationType.Daily, 0))
            {
                // make date eligible
                var lastField = typeof(RotatingStreamWriter).GetField("_lastRotationDate", BindingFlags.NonPublic | BindingFlags.Instance);
                lastField.SetValue(writer, DateTime.Now.AddDays(-1));

                // Do small write that won't exceed size -> should rotate by date
                writer.WriteLine("date rotation hit");
                writer.Flush();
            }

            var rotated = Directory.GetFiles(_testDir, "dateOnlyWhenSizeNotExceeded.*.log").Where(f => !f.EndsWith("dateOnlyWhenSizeNotExceeded.log")).ToArray();
            Assert.NotEmpty(rotated);
        }

        [Fact]
        public void ShouldRotateByDate_DefaultCase_ReturnsFalse()
        {
            // Arrange
            using (var writer = new RotatingStreamWriter(
                "dummy.log",
                enableSizeRotation: false,
                rotationSizeInBytes: 1000,
                enableDateRotation: true,
                dateRotationType: DateRotationType.Daily,
                maxRotations: 1))
            {
                // Force an invalid enum value
                var field = typeof(RotatingStreamWriter)
                    .GetField("_dateRotationType", BindingFlags.NonPublic | BindingFlags.Instance);
                field.SetValue(writer, (DateRotationType)999);

                // Use reflection to call the private method
                var method = typeof(RotatingStreamWriter)
                    .GetMethod("ShouldRotateByDate", BindingFlags.NonPublic | BindingFlags.Instance);

                // Act
                var result = (bool)method.Invoke(writer, Array.Empty<object>());

                // Assert
                Assert.False(result);
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

                GC.SuppressFinalize(this);
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }
}

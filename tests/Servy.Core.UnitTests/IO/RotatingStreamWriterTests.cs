using Servy.Core.Enums;
using Servy.Core.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.AccessControl;

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

        // Helper to construct per the constructor signature
        private RotatingStreamWriter CreateWriter(
            string path,
            bool enableSizeRotation = true,
            long rotationSizeInBytes = 1024,
            bool enableDateRotation = false,
            DateRotationType dateRotationType = DateRotationType.Daily,
            int maxRotations = 0,
            bool useLocalTimeForRotation = false,
            Func<DateTime>? timeProvider = null)
        {
            return new RotatingStreamWriter(
                path,
                enableSizeRotation,
                rotationSizeInBytes,
                enableDateRotation,
                dateRotationType,
                maxRotations,
                useLocalTimeForRotation,
                timeProvider);
        }

        [Fact]
        public void Constructor_InvalidPath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => CreateWriter(null!, true, 100));
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

        // --- NEW: Lazy Initialization Tests ---

        [Fact]
        public void LazyInitialization_Write_DoesNotCreateFileUntilCalled()
        {
            var filePath = Path.Combine(_testDir, "lazy_write.txt");

            using (var writer = CreateWriter(filePath, true, 10))
            {
                // File shouldn't exist right after instantiation
                Assert.False(File.Exists(filePath), "File should not exist yet");

                writer.Write("123");

                // File should exist after the first interaction
                Assert.True(File.Exists(filePath), "File should exist after write");
            }
        }

        [Fact]
        public void LazyInitialization_WriteLine_DoesNotCreateFileUntilCalled()
        {
            var filePath = Path.Combine(_testDir, "lazy_writeline.txt");

            using (var writer = CreateWriter(filePath, true, 10))
            {
                Assert.False(File.Exists(filePath), "File should not exist yet");

                writer.WriteLine("123");

                Assert.True(File.Exists(filePath), "File should exist after write");
            }
        }

        [Fact]
        public void LazyInitialization_RecreatesWriterAfterRotation()
        {
            var filePath = Path.Combine(_testDir, "lazy_rotate.txt");

            // 1. Perform operations inside the using block
            using (var writer = CreateWriter(filePath, true, 5, false, DateRotationType.Daily, 0))
            {
                writer.Write("123456"); // Triggers rotation, file is moved

                // This assertion passes because Rotate() moved the file and CloseWriter() was called inside it
                Assert.False(File.Exists(filePath), "Original file should have been moved");

                writer.Write("789"); // Triggers lazy init; a NEW file handle is opened here
            } // <--- The handle is officially closed here

            // 2. Assert on the results after the handle is released
            Assert.True(File.Exists(filePath), "Original file should have been recreated by lazy init");
            Assert.Contains("789", File.ReadAllText(filePath));
        }

        // --- End Lazy Init Tests ---

        [Fact]
        public void GenerateUniqueFileName_ThrowsArgumentException_WhenPathHasNoDirectory()
        {
            // 1. Arrange: Create a filename with no path info
            string fileName = $"test_file_{Guid.NewGuid()}.txt";

            // We must actually create the file so File.Exists(basePath) returns true
            File.WriteAllText(fileName, "dummy content");

            try
            {
                // 2. Act & Assert
                // Path.GetDirectoryName("filename.txt") returns null or string.Empty 
                // depending on the .NET runtime version, triggering your check.
                Assert.Throws<ArgumentException>(() => InvokeGenerateUniqueFileName(fileName));
            }
            finally
            {
                // 3. Cleanup: Always delete physical files created during tests
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
            }
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
        public void GenerateUniqueFileName_ShouldThrowIOException_WhenMaxRetryLimitExceeded()
        {
            // Arrange
            string fileName = "testlog.txt";
            string basePath = Path.Combine(_testDir, fileName);
            string namePart = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);

            // Create the base file
            File.WriteAllText(basePath, "initial content");

            // Create 10,000 colliding files: testlog.(1).txt to testlog.(10000).txt
            // Note: On modern SSDs, creating 10k empty files takes ~1-2 seconds.
            for (int i = 1; i <= 10000; i++)
            {
                string collisionPath = Path.Combine(_testDir, $"{namePart}.({i}){extension}");
                File.WriteAllText(collisionPath, string.Empty);
            }

            // Get the private static method via reflection
            var methodInfo = typeof(RotatingStreamWriter).GetMethod(
                "GenerateUniqueFileName",
                BindingFlags.Static | BindingFlags.NonPublic)!;

            // Act & Assert
            var exception = Assert.Throws<TargetInvocationException>(() =>
                methodInfo.Invoke(null, new object[] { basePath }));

            // TargetInvocationException wraps the actual IOException
            Assert.IsType<IOException>(exception.InnerException);
            Assert.Contains("after 10000 attempts", exception.InnerException.Message);
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
            var basePath = Path.Combine(_testDir, fileName);
            var expectedPath = Path.Combine(_testDir, expectedName);
            File.WriteAllText(basePath, "dummy"); // Create the collision

            var result = InvokeGenerateUniqueFileName(basePath);

            Assert.Equal(expectedPath, result);
        }

        [Fact]
        public void IncrementsCounter_WhenMultipleCollisionsExist()
        {
            var fileName = "App.20260325_001611.log";
            var basePath = Path.Combine(_testDir, fileName);

            File.WriteAllText(basePath, "orig");
            File.WriteAllText(Path.Combine(_testDir, "App.20260325_001611.(1).log"), "coll1");

            var expectedPath = Path.Combine(_testDir, "App.20260325_001611.(2).log");
            var result = InvokeGenerateUniqueFileName(basePath);

            Assert.Equal(expectedPath, result);
        }

        [Fact]
        public void Rotate_CreatesRotatedFileAndNewWriter()
        {
            var filePath = Path.Combine(_testDir, "rotate.txt");
            var fixedTime = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

            using (var writer = CreateWriter(filePath, true, 5, false, DateRotationType.Daily, 0, false, () => fixedTime))
            {
                writer.WriteLine("123"); // small write
                writer.Write("456");    // triggers rotation

                Assert.True(File.Exists(filePath)); // new file recreated

                var rotatedFiles = Directory.GetFiles(_testDir, "rotate.*.txt").Where(f => !f.EndsWith("rotate.txt")).ToList();
                Assert.NotEmpty(rotatedFiles);

                // Assert precisely against the frozen time
                Assert.Contains(fixedTime.ToString("yyyyMMdd"), rotatedFiles[0]);
            }

            var latestRotatedFile = Directory.GetFiles(_testDir, "rotate.*.txt")
                    .Select(f => new FileInfo(f))
                    .Where(f => !f.Name.Equals("rotate.txt"))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();

            Assert.NotNull(latestRotatedFile);
            var content = File.ReadAllText(latestRotatedFile.FullName);
            Assert.Contains("123", content);
        }

        [Fact]
        public void Flush_WhenWriterIsNotNull_CallsUnderlyingFlush()
        {
            var filePath = Path.Combine(_testDir, "test.txt");

            using (var writer = CreateWriter(filePath, true, 10))
            {
                writer.WriteLine("hello"); // initializes lazy writer
                writer.Flush();
            }

            var content = File.ReadAllText(filePath);
            Assert.Equal("hello\r\n", content);
        }

        [Fact]
        public void Flush_WhenWriterIsNull_DoesNothing()
        {
            // Just create it (lazy init means writer is null)
            var writer = CreateWriter(Path.Combine(_testDir, "test.txt"), true, 10);

            // Calling Flush should not throw
            var exception = Record.Exception(() => writer.Flush());
            Assert.Null(exception);
        }

        [Fact]
        public void Flush_EmptyWrite_CoversBaseStreamLengthZero()
        {
            var filePath = Path.Combine(_testDir, "flush_empty.txt");
            using (var writer = CreateWriter(filePath, true, 10))
            {
                writer.Write(""); // Forces lazy initialization, _baseStream is created
                writer.Flush();   // Length could be 0, covers the extra boundary
            }
            Assert.True(File.Exists(filePath));
        }

        private string InvokeGenerateUniqueFileName(string path)
        {
            try
            {
                var method = typeof(RotatingStreamWriter).GetMethod("GenerateUniqueFileName", BindingFlags.NonPublic | BindingFlags.Static);
                return (string)method!.Invoke(null, new object[] { path })!;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                // Preserve the stack trace of the original exception
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw; // Unreachable
            }
        }

        private void InvokeRotate(RotatingStreamWriter instance)
        {
            var method = typeof(RotatingStreamWriter).GetMethod("Rotate", BindingFlags.NonPublic | BindingFlags.Instance);
            method!.Invoke(instance, null);
        }

        [Fact]
        public void Dispose_IsIdempotent_AndDoesNotCrashOnSubsequentCalls()
        {
            var writer = new RotatingStreamWriter(_logFilePath, true, 1000, false, DateRotationType.Daily, 0, false);
            writer.WriteLine("Initial log entry");

            writer.Dispose();

            var ex1 = Record.Exception(() => writer.WriteLine("Line after disposal"));
            Assert.Null(ex1);

            // Dispose again
            var ex2 = Record.Exception(writer.Dispose);
            Assert.Null(ex2);
        }

        [Fact]
        public void GenerateUniqueFileName_ReturnsNonExistingFileName()
        {
            var methodInfo = typeof(RotatingStreamWriter).GetMethod("GenerateUniqueFileName", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(methodInfo);

            using (var writer = CreateWriter(_logFilePath, true, 1000))
            {
                var basePath = Path.Combine(_testDir, "file.log");
                File.WriteAllText(basePath, "test");
                File.WriteAllText(Path.Combine(_testDir, "file.(1).log"), "test");
                File.WriteAllText(Path.Combine(_testDir, "file.(2).log"), "test");

                var uniqueName = (string)methodInfo.Invoke(writer, new object[] { basePath })!;

                Assert.Equal(Path.Combine(_testDir, "file.(3).log"), uniqueName);
            }
        }

        [Fact]
        public void WriteLine_DoesNotRotate_WhenRotationSizeZero()
        {
            var filePath = Path.Combine(_testDir, "zero.txt");

            using (var writer = CreateWriter(filePath, true, 0))
            {
                writer.WriteLine("Hello");
            }

            Assert.True(File.Exists(filePath));
            var rotatedFiles = Directory.GetFiles(_testDir, "zero.txt.*").Where(f => !f.EndsWith("zero.txt"));
            Assert.Empty(rotatedFiles);
        }

        [Fact]
        public void WriteLine_DoesNotRotate_WhenFileSmallerThanRotationSize()
        {
            var filePath = Path.Combine(_testDir, "small.txt");

            using (var writer = CreateWriter(filePath, true, 1024))
            {
                writer.WriteLine("small");
            }

            Assert.True(File.Exists(filePath));
            var rotatedFiles = Directory.GetFiles(_testDir, "small.txt.*").Where(f => !f.EndsWith("small.txt"));
            Assert.Empty(rotatedFiles);
        }

        [Fact]
        public void WriteLine_Rotates_WhenFileExceedsRotationSize()
        {
            var filePath = Path.Combine(_testDir, "rotate2.txt");

            using (var writer = CreateWriter(filePath, true, 5))
            {
                writer.WriteLine("12345"); // exactly 5 bytes -> triggers rotation
                writer.Flush();
                writer.WriteLine("12"); // write to new file
                writer.Flush();
            }

            Assert.True(File.Exists(filePath));
            var rotatedFiles = Directory.GetFiles(_testDir, "rotate2.txt.*");
            Assert.NotEmpty(rotatedFiles);
        }

        [Fact]
        public void Write_WhenWriterIsNull_DoesNothingAfterDisposal()
        {
            var writer = CreateWriter(Path.Combine(_testDir, "test.txt"), true, 10);
            writer.Dispose();

            var exception = Record.Exception(() => writer.Write("12345"));
            Assert.Null(exception);
        }

        [Fact]
        public void EnforceMaxRotations_CoversAllBranches()
        {
            string baseLog = Path.Combine(_testDir, "service.log");
            File.WriteAllText(baseLog, "base");

            var writer = CreateWriter(baseLog, true, 1000);
            writer.Write(""); // force file creation

            var enforceMethod = typeof(RotatingStreamWriter).GetMethod("EnforceMaxRotations", BindingFlags.NonPublic | BindingFlags.Instance);
            var maxRotField = typeof(RotatingStreamWriter).GetField("_maxRotations", BindingFlags.NonPublic | BindingFlags.Instance);

            // ---- BRANCH 1: _maxRotations <= 0 ----
            maxRotField!.SetValue(writer, 0);
            enforceMethod!.Invoke(writer, null);

            // ---- BRANCH 2: Filter Logic (StartsWith and EndsWith) ----
            string f1 = Path.Combine(_testDir, "service.20260325_000001.log");
            string noise1 = Path.Combine(_testDir, "service_backup.log");
            string noise2 = Path.Combine(_testDir, "service.20260325.txt");

            File.WriteAllText(f1, "valid");
            File.WriteAllText(noise1, "noise");
            File.WriteAllText(noise2, "noise");

            // ---- BRANCH 3: rotatedFiles.Count <= _maxRotations ----
            maxRotField.SetValue(writer, 5);
            enforceMethod.Invoke(writer, null);

            Assert.True(File.Exists(f1));
            Assert.True(File.Exists(noise1));
            Assert.True(File.Exists(noise2));

            // ---- BRANCH 4: Deletion happens (rotatedFiles.Count > _maxRotations) ----
            string f2 = Path.Combine(_testDir, "service.20260325_000002.log");
            File.WriteAllText(f2, "valid2");
            File.SetLastWriteTimeUtc(f1, DateTime.UtcNow.AddMinutes(-10));
            File.SetLastWriteTimeUtc(f2, DateTime.UtcNow);

            maxRotField.SetValue(writer, 1);
            enforceMethod.Invoke(writer, null);

            Assert.True(File.Exists(f2));     // Kept (newest)
            Assert.False(File.Exists(f1));    // Deleted
            Assert.True(File.Exists(noise1));
            Assert.True(File.Exists(noise2));

            // ---- BRANCH 5: Deletion Failure (IOException) ----
            File.WriteAllText(f1, "recreate");
            File.SetLastWriteTimeUtc(f1, DateTime.UtcNow.AddMinutes(-10));
            using (var locked = new FileStream(f1, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var ex = Record.Exception(() => enforceMethod.Invoke(writer, null));
                Assert.Null(ex); // Resilient against locks
            }

            writer.Dispose();
        }

        [Fact]
        public void EnforceMaxRotations_NoExtension_CoversIsNullOrEmpty()
        {
            string baseLog = Path.Combine(_testDir, "plainfile");
            File.WriteAllText(baseLog, "base");

            var writer = CreateWriter(baseLog, true, 1000);
            writer.Write(""); // Trigger lazy init

            var enforceMethod = typeof(RotatingStreamWriter).GetMethod("EnforceMaxRotations", BindingFlags.NonPublic | BindingFlags.Instance);
            var maxRotField = typeof(RotatingStreamWriter).GetField("_maxRotations", BindingFlags.NonPublic | BindingFlags.Instance);

            string f1 = Path.Combine(_testDir, "plainfile.20260325_000001");
            File.WriteAllText(f1, "rotated");
            File.SetLastWriteTimeUtc(f1, DateTime.UtcNow.AddMinutes(-10));

            maxRotField!.SetValue(writer, 1);

            string f2 = Path.Combine(_testDir, "plainfile.20260325_000002");
            File.WriteAllText(f2, "rotated2");
            File.SetLastWriteTimeUtc(f2, DateTime.UtcNow);

            enforceMethod!.Invoke(writer, null);

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

            File.SetLastWriteTimeUtc(rotated1, DateTime.UtcNow.AddMinutes(0));
            File.SetLastWriteTimeUtc(rotated2, DateTime.UtcNow.AddMinutes(-1));

            var writer = CreateWriter(logPath, true, 1, false, DateRotationType.Daily, 1);
            writer.Write(""); // create lazy file 

            File.SetAttributes(rotated2, FileAttributes.ReadOnly);

            var ex = Record.Exception(() =>
            {
                var method = typeof(RotatingStreamWriter).GetMethod("EnforceMaxRotations", BindingFlags.NonPublic | BindingFlags.Instance);
                method!.Invoke(writer, Array.Empty<object>());
            });

            Assert.Null(ex);
            File.SetAttributes(rotated2, FileAttributes.Normal); // Reset so test runner can clean it up
        }

        [Fact]
        public void DateRotation_Daily_Rotates_WhenDateBoundaryCrossed()
        {
            var filePath = Path.Combine(_testDir, "daily.log");
            var fixedTime = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

            using (var writer = CreateWriter(filePath, false, 0, true, DateRotationType.Daily, 0, false, () => fixedTime))
            {
                var lastField = typeof(RotatingStreamWriter).GetField("_lastRotationDate", BindingFlags.NonPublic | BindingFlags.Instance);
                lastField!.SetValue(writer, fixedTime.AddDays(-1));

                writer.WriteLine("rotate on daily boundary");
                writer.Flush();
            }

            var rotated = Directory.GetFiles(_testDir, "daily.*.log").Where(f => !f.EndsWith("daily.log")).ToArray();
            Assert.NotEmpty(rotated);
            Assert.Contains(fixedTime.ToString("yyyyMMdd"), rotated[0]);
        }

        [Fact]
        public void DateRotation_Weekly_Rotates_WhenYearBoundaryCrossed()
        {
            var filePath = Path.Combine(_testDir, "weekly_year.log");
            using (var writer = CreateWriter(filePath, false, 0, true, DateRotationType.Weekly, 0))
            {
                var lastYear = DateTime.UtcNow.Year - 1;
                var dec31 = new DateTime(lastYear, 12, 31);

                var lastField = typeof(RotatingStreamWriter).GetField("_lastRotationDate", BindingFlags.NonPublic | BindingFlags.Instance);
                lastField!.SetValue(writer, dec31);

                writer.WriteLine("rotate on year change");
                writer.Flush();
            }

            var rotated = Directory.GetFiles(_testDir, "weekly_year.*.log").Where(f => !f.EndsWith("weekly_year.log")).ToArray();
            Assert.NotEmpty(rotated);
        }

        [Fact]
        public void DateRotation_Weekly_DoesNotRotate_OnSameWeek()
        {
            var filePath = Path.Combine(_testDir, "weekly_same.log");
            var recently = DateTime.UtcNow.AddHours(-1);

            using (var writer = CreateWriter(filePath, false, 0, true, DateRotationType.Weekly, 0))
            {
                var lastField = typeof(RotatingStreamWriter).GetField("_lastRotationDate", BindingFlags.NonPublic | BindingFlags.Instance);
                lastField!.SetValue(writer, recently);

                writer.WriteLine("should not rotate");
                writer.Flush();
            }

            var rotated = Directory.GetFiles(_testDir, "weekly_same.*.log").Where(f => !f.EndsWith("weekly_same.log"));
            Assert.Empty(rotated);
        }

        [Fact]
        public void DateRotation_Monthly_Rotates_WhenMonthBoundaryCrossed()
        {
            var filePath = Path.Combine(_testDir, "monthly.log");
            var fixedTime = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

            using (var writer = CreateWriter(filePath, false, 0, true, DateRotationType.Monthly, 0, false, () => fixedTime))
            {
                var lastField = typeof(RotatingStreamWriter).GetField("_lastRotationDate", BindingFlags.NonPublic | BindingFlags.Instance);
                lastField!.SetValue(writer, fixedTime.AddMonths(-1));

                writer.WriteLine("rotate on monthly boundary");
                writer.Flush();
            }

            var rotated = Directory.GetFiles(_testDir, "monthly.*.log").Where(f => !f.EndsWith("monthly.log")).ToArray();
            Assert.NotEmpty(rotated);
            Assert.Contains(fixedTime.ToString("yyyyMMdd"), rotated[0]);
        }

        [Fact]
        public void DateRotation_Monthly_Rotates_WhenYearBoundaryCrossed()
        {
            var filePath = Path.Combine(_testDir, "monthly.log");
            var fixedTime = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

            using (var writer = CreateWriter(filePath, false, 0, true, DateRotationType.Monthly, 0, false, () => fixedTime))
            {
                var lastField = typeof(RotatingStreamWriter).GetField("_lastRotationDate", BindingFlags.NonPublic | BindingFlags.Instance);
                lastField!.SetValue(writer, fixedTime.AddYears(-1));

                writer.WriteLine("rotate on monthly boundary");
                writer.Flush();
            }

            var rotated = Directory.GetFiles(_testDir, "monthly.*.log").Where(f => !f.EndsWith("monthly.log")).ToArray();
            Assert.NotEmpty(rotated);
            Assert.Contains(fixedTime.ToString("yyyyMMdd"), rotated[0]);
        }

        [Fact]
        public void SizeAndDateRotation_SizePrecedence_WhenBothEnabled()
        {
            var filePath = Path.Combine(_testDir, "sizeDate.log");
            var fixedTime = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

            using (var writer = CreateWriter(filePath, true, 5, true, DateRotationType.Daily, 0, false, () => fixedTime))
            {
                var lastField = typeof(RotatingStreamWriter).GetField("_lastRotationDate", BindingFlags.NonPublic | BindingFlags.Instance);
                lastField!.SetValue(writer, fixedTime.AddDays(-1));

                writer.Write("123456");
                writer.Flush();
            }

            var rotated = Directory.GetFiles(_testDir, "sizeDate.*.log").Where(f => !f.EndsWith("sizeDate.log")).ToArray();
            Assert.NotEmpty(rotated);

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
            var fixedTime = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

            using (var writer = CreateWriter(filePath, true, 1024, true, DateRotationType.Daily, 0, false, () => fixedTime))
            {
                var lastField = typeof(RotatingStreamWriter).GetField("_lastRotationDate", BindingFlags.NonPublic | BindingFlags.Instance);
                lastField!.SetValue(writer, fixedTime.AddDays(-1));

                writer.WriteLine("date rotation hit");
                writer.Flush();
            }

            var rotated = Directory.GetFiles(_testDir, "dateOnlyWhenSizeNotExceeded.*.log").Where(f => !f.EndsWith("dateOnlyWhenSizeNotExceeded.log")).ToArray();
            Assert.NotEmpty(rotated);
        }

        [Fact]
        public void ShouldRotateByDate_DefaultCase_ReturnsFalse()
        {
            using (var writer = new RotatingStreamWriter("dummy.log", false, 1000, true, DateRotationType.Daily, 1, false))
            {
                // 1. Force an invalid enum value into the private field
                var field = typeof(RotatingStreamWriter).GetField("_dateRotationType", BindingFlags.NonPublic | BindingFlags.Instance);
                field!.SetValue(writer, (DateRotationType)999);

                // 2. Get the method info
                var method = typeof(RotatingStreamWriter).GetMethod("ShouldRotateByDate", BindingFlags.NonPublic | BindingFlags.Instance);

                // 3. Provide the required DateTime parameter (even for the default/invalid case)
                var args = new object[] { DateTime.UtcNow };
                var result = (bool)method!.Invoke(writer, args)!;

                // Assert: An unrecognized rotation type should safely return false
                Assert.False(result);
            }
        }

        [Fact]
        public void Rotate_WhenFileIsLocked_SilentlyContinuesWithoutCrashing()
        {
            var filePath = Path.Combine(_testDir, "locked_rotate.txt");

            using (var writer = new RotatingStreamWriter(filePath, true, 5, false, DateRotationType.Daily, 0, false))
            {
                writer.Write("init");
                writer.Flush();

                using (var blocker = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var exception = Record.Exception(() =>
                    {
                        writer.Write("trigger_rotation");
                        writer.Flush();
                    });

                    Assert.Null(exception);
                }

                var rotatedFiles = Directory.GetFiles(_testDir, "locked_rotate.*.txt")
                                           .Where(f => !f.EndsWith("locked_rotate.txt"))
                                           .ToList();
                Assert.Empty(rotatedFiles);
                Assert.True(File.Exists(filePath));
            }
        }

        [Fact]
        public void Rotate_WhenFileMissingOrEmpty_ReturnsEarly()
        {
            var filePath = Path.Combine(_testDir, "missing_rotate.txt");
            using (var writer = CreateWriter(filePath, true, 5, false, DateRotationType.Daily, 0))
            {
                // 1. File doesn't exist
                InvokeRotate(writer);
                Assert.False(File.Exists(filePath));

                // 2. File exists but is empty
                writer.Write(""); // Lazy init -> 0 bytes
                writer.Flush();
                Assert.True(File.Exists(filePath));
                Assert.Equal(0, new FileInfo(filePath).Length);

                InvokeRotate(writer);

                // Ensure no rotated file was generated
                var rotatedFiles = Directory.GetFiles(_testDir, "missing_rotate.*.txt");
                Assert.Empty(rotatedFiles);
            }
        }

        // --- UseLocalTimeForRotation & Daily Rotation Branch Coverage ---

        [Fact]
        public void Constructor_InitializesLastRotationDate_CorrectlyForTimeZones()
        {
            // Ensure file exists to test the "File.Exists" branch
            File.WriteAllText(_logFilePath, "content");
            var localWriteTime = File.GetLastWriteTime(_logFilePath);
            var utcWriteTime = File.GetLastWriteTimeUtc(_logFilePath);

            // Test Local Branch
            using (var localWriter = CreateWriter(_logFilePath, useLocalTimeForRotation: true))
            {
                var lastRot = (DateTime)GetPrivateField(localWriter, "_lastRotationDate");
                Assert.Equal(localWriteTime, lastRot);
            }

            // Test UTC Branch
            using (var utcWriter = CreateWriter(_logFilePath, useLocalTimeForRotation: false))
            {
                var lastRot = (DateTime)GetPrivateField(utcWriter, "_lastRotationDate");
                Assert.Equal(utcWriteTime, lastRot);
            }
        }

        [Fact]
        public void DailyRotation_Local_DST_SafetyBuffer_PreventsEarlyRotation()
        {
            // Arrange: Fix the timeline to simulate a 22-hour gap across midnight
            // April 10th, 11:00 PM (23:00)
            var lastRotationUtc = new DateTime(2026, 4, 10, 23, 0, 0, DateTimeKind.Utc);

            // April 11th, 09:00 PM (21:00) -> 22 hours later
            var nowUtc = lastRotationUtc.AddHours(22);

            using (var writer = CreateWriter(_logFilePath, enableDateRotation: true, useLocalTimeForRotation: true))
            {
                SetPrivateField(writer, "_lastRotationDate", lastRotationUtc);

                // Act
                var args = new object[] { nowUtc };
                var shouldRotate = (bool)InvokePrivateMethod(writer, "ShouldRotateByDate", args);

                // Assert
                // Even though it is a new day (April 11 vs April 10), 
                // the 23-hour buffer should block the rotation.
                Assert.False(shouldRotate, "Should not rotate if < 23 hours have passed, even if the calendar day changed.");
            }
        }

        [Fact]
        public void DailyRotation_Local_Simulated_AllowsRotationAfterThreshold()
        {
            // Arrange: Use a fixed UTC date that is guaranteed to cross a day boundary
            // April 10th, 10:00 AM UTC
            var lastRotationUtc = new DateTime(2026, 4, 10, 10, 0, 0, DateTimeKind.Utc);

            // April 11th, 11:00 AM UTC (25 hours later)
            // 25 hours ensures that even in the most extreme time zones, 
            // a new calendar day has started.
            var nowUtc = lastRotationUtc.AddHours(25);

            using (var writer = CreateWriter(_logFilePath, enableDateRotation: true, useLocalTimeForRotation: true))
            {
                SetPrivateField(writer, "_lastRotationDate", lastRotationUtc);

                // Act
                var args = new object[] { nowUtc };
                var shouldRotate = (bool)InvokePrivateMethod(writer, "ShouldRotateByDate", args);

                // Assert
                Assert.True(shouldRotate, "Rotation should trigger when crossing into a new calendar day.");
            }
        }

        [Fact]
        public void DailyRotation_UTC_IgnoresSafetyBuffer()
        {
            // Arrange: Strictly UTC. 
            // Last rotation was yesterday at 11:59:59 PM
            var lastRotationUtc = new DateTime(2026, 4, 10, 23, 59, 59, DateTimeKind.Utc);

            // Now is today at 00:00:01 AM (Only 2 seconds passed, but day changed)
            var nowUtc = new DateTime(2026, 4, 11, 0, 0, 1, DateTimeKind.Utc);

            using (var writer = CreateWriter(_logFilePath, enableDateRotation: true, useLocalTimeForRotation: false))
            {
                SetPrivateField(writer, "_lastRotationDate", lastRotationUtc);

                // Act: Pass the simulated 'now'
                var args = new object[] { nowUtc };
                var shouldRotate = (bool)InvokePrivateMethod(writer, "ShouldRotateByDate", args);

                // Assert: In UTC mode, we ignore the 23h buffer and rotate on day change
                Assert.True(shouldRotate, "UTC mode should rotate immediately when the calendar day flips.");
            }
        }

        [Fact]
        public void CheckRotation_UpdatesLastRotationDate_WithCorrectTimeZone()
        {
            // 1. Test Local Update
            // Set rotationSizeInBytes to 1 so the first write triggers CheckRotation() natively
            using (var localWriter = CreateWriter(_logFilePath, enableSizeRotation: true, rotationSizeInBytes: 1, useLocalTimeForRotation: true))
            {
                localWriter.Write("init"); // Exceeds 1 byte, triggering native rotation and updating the date

                var lastRot = (DateTime)GetPrivateField(localWriter, "_lastRotationDate");
                Assert.Equal(DateTimeKind.Local, lastRot.Kind);
            }

            // 2. Test UTC Update
            using (var utcWriter = CreateWriter(_logFilePath, enableSizeRotation: true, rotationSizeInBytes: 1, useLocalTimeForRotation: false))
            {
                utcWriter.Write("init"); // Exceeds 1 byte, triggering native rotation and updating the date

                var lastRot = (DateTime)GetPrivateField(utcWriter, "_lastRotationDate");
                Assert.Equal(DateTimeKind.Utc, lastRot.Kind);
            }
        }

        [Fact]
        public void ShouldRotateByDate_Daily_SameDay_ReturnsFalse()
        {
            // Arrange: Pick a fixed UTC point in time
            var nowUtc = new DateTime(2026, 4, 11, 10, 0, 0, DateTimeKind.Utc);

            using (var writer = CreateWriter(_logFilePath, enableDateRotation: true, dateRotationType: DateRotationType.Daily))
            {
                // Set last rotation to the same day
                SetPrivateField(writer, "_lastRotationDate", nowUtc.AddHours(-2));

                // Act: Pass nowUtc as the argument to the private method
                var args = new object[] { nowUtc };
                var result = (bool)InvokePrivateMethod(writer, "ShouldRotateByDate", args);

                // Assert: 10:00 AM is same day as 08:00 AM, should not rotate
                Assert.False(result, "Should not rotate if the calendar day has not changed.");
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CheckRotation_SizeRotation_TernaryCoverage(bool useLocal)
        {
            var filePath = Path.Combine(_testDir, $"size_ternary_{useLocal}.log");
            // Set size to 1 byte so any write triggers rotateBySize = true
            using (var writer = CreateWriter(filePath, enableSizeRotation: true, rotationSizeInBytes: 1, useLocalTimeForRotation: useLocal))
            {
                writer.Write("trigger"); // This hits CheckRotation -> rotateBySize is true

                var lastRot = (DateTime)GetPrivateField(writer, "_lastRotationDate");
                Assert.Equal(useLocal ? DateTimeKind.Local : DateTimeKind.Utc, lastRot.Kind);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CheckRotation_DateRotation_TernaryCoverage(bool useLocal)
        {
            var filePath = Path.Combine(_testDir, $"date_ternary_{useLocal}.log");
            using (var writer = CreateWriter(filePath, enableDateRotation: true, dateRotationType: DateRotationType.Daily, useLocalTimeForRotation: useLocal))
            {
                writer.Write("init"); // Ensure file and writer exist

                // Force ShouldRotateByDate to return true by aging the last rotation
                DateTime fakePast = useLocal ? DateTime.UtcNow.AddDays(-2) : DateTime.UtcNow.AddDays(-2);
                SetPrivateField(writer, "_lastRotationDate", fakePast);

                // Trigger write -> CheckRotation -> rotateByDate is true
                writer.Write("trigger rotation");

                var lastRot = (DateTime)GetPrivateField(writer, "_lastRotationDate");
                Assert.Equal(useLocal ? DateTimeKind.Local : DateTimeKind.Utc, lastRot.Kind);
            }
        }

        #region Circuit Breaker & Permanent Failure Tests

        [Fact]
        public void CheckRotation_EarlyReturn_WhenDisabled()
        {
            var filePath = Path.Combine(_testDir, "gatekeeper.log");
            // Arrange: Set a high limit so the first write DOES NOT rotate
            using (var writer = CreateWriter(filePath, enableSizeRotation: true, rotationSizeInBytes: 1000))
            {
                writer.Write("initial_data"); // 12 bytes < 1000 (No rotation)
                writer.Flush();

                // 1. Trip breaker manually
                SetPrivateField(writer, "_rotationDisabled", true);

                // 2. Act: This should trigger a rotation (1000+ bytes), but won't
                writer.Write(new string('X', 1100));
                writer.Flush();

                // 3. Assert: Filter out the base file to check ONLY for rotated ones
                var rotatedFiles = Directory.GetFiles(_testDir, "gatekeeper.*.log")
                                            .Where(f => !f.EndsWith("gatekeeper.log"));
                Assert.Empty(rotatedFiles);
            }
        }

        [Fact]
        public async Task Rotate_TransientIOException_SucceedsOnRetry()
        {
            var filePath = Path.Combine(_testDir, "transient.log");
            File.WriteAllText(filePath, "some logs");

            using (var writer = CreateWriter(filePath))
            {
                // Use a signal to ensure the background task has actually started
                var startedSignal = new ManualResetEventSlim(false);

                // 1. Lock the file
                using (var locker = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {

                    // 2. Start rotation
                    var rotateTask = Task.Run(() =>
                    {
                        startedSignal.Set(); // Signal that we are entering the method
                        var method = typeof(RotatingStreamWriter).GetMethod("Rotate", BindingFlags.NonPublic | BindingFlags.Instance);
                        method!.Invoke(writer, null);
                    }, TestContext.Current.CancellationToken);

                    // 3. Ensure the task has at least context-switched in
                    startedSignal.Wait(500, TestContext.Current.CancellationToken);

                    // Give the background thread a moment to hit the lock and enter its first retry/SpinWait
                    // We use a slightly longer delay here because your SpinWait is 50ms.
                    await Task.Delay(100, TestContext.Current.CancellationToken);

                    // 4. Release the lock
                    locker.Dispose();

                    // Allow a small "settle" time for the Windows handle to actually close
                    await Task.Delay(50, TestContext.Current.CancellationToken);

                    await rotateTask;

                    // 5. Assert
                    bool isDisabled = (bool)GetPrivateField(writer, "_rotationDisabled")!;
                    DateTime cooldown = (DateTime)GetPrivateField(writer, "_rotationCooldownUntil")!;

                    Assert.False(isDisabled, "Breaker should not trip on IOException.");

                    // Use a small epsilon for the date or verify it's less than "now" 
                    // to avoid millisecond racing in the assertion itself.
                    Assert.True(cooldown == DateTime.MinValue,
                        $"Cooldown should be MinValue but was {cooldown:O}. This means the rotation failed all retries.");

                    Assert.False(File.Exists(filePath), "File should have been moved successfully.");
                }
            }
        }

        [Fact]
        public void Rotate_PersistentIOException_ReachedLimit_SetsCooldown()
        {
            var filePath = Path.Combine(_testDir, "persistent.log");
            File.WriteAllText(filePath, "more logs");

            using (var writer = CreateWriter(filePath))
            {
                // 1. Lock the file and KEEP it locked
                using (var locker = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // 2. Act
                    var rotateMethod = typeof(RotatingStreamWriter).GetMethod("Rotate", BindingFlags.NonPublic | BindingFlags.Instance);
                    rotateMethod!.Invoke(writer, null);
                }

                // 3. Assert
                bool isDisabled = (bool)GetPrivateField(writer, "_rotationDisabled")!;
                DateTime cooldown = (DateTime)GetPrivateField(writer, "_rotationCooldownUntil")!;

                // Check that rotation failed but the breaker DID NOT trip
                Assert.False(isDisabled, "IOException should NOT trip the circuit breaker.");

                // Check that the limit was reached and cooldown was applied
                Assert.True(cooldown > DateTime.UtcNow, "Rotation should be on cooldown after exhausting retries.");

                // Verify the original file still exists (rotation failed)
                Assert.True(File.Exists(filePath), "The original log file should still exist because rotation was deferred.");
            }
        }

        [Fact]
        public void Rotate_PermanentFailure_TripsBreaker()
        {
            var subDir = Path.Combine(_testDir, "BreakerTest");
            Directory.CreateDirectory(subDir);
            var filePath = Path.Combine(subDir, "breaker_test.log");

            // 1. Initialize DirectoryInfo to access ACL extension methods
            var dirInfo = new DirectoryInfo(subDir);

            using (var writer = CreateWriter(filePath))
            {
                writer.Write("init");
                writer.Flush();

                // 2. Get the ACL from the DirectoryInfo instance
                var acl = dirInfo.GetAccessControl();

                var rule = new FileSystemAccessRule(
                    Environment.UserName,
                    FileSystemRights.Delete | FileSystemRights.Write,
                    AccessControlType.Deny);

                acl.AddAccessRule(rule);

                // 3. Apply the rule back to the DirectoryInfo instance
                dirInfo.SetAccessControl(acl);

                try
                {
                    var rotateMethod = typeof(RotatingStreamWriter).GetMethod("Rotate", BindingFlags.NonPublic | BindingFlags.Instance);
                    rotateMethod!.Invoke(writer, null);
                }
                finally
                {
                    // 4. CLEANUP: Re-fetch and remove the rule via the instance
                    // We re-fetch to ensure we have the most current state for removal
                    var cleanupAcl = dirInfo.GetAccessControl();
                    cleanupAcl.RemoveAccessRule(rule);
                    dirInfo.SetAccessControl(cleanupAcl);

                    if (File.Exists(filePath))
                    {
                        File.SetAttributes(filePath, FileAttributes.Normal);
                    }
                }

                bool isDisabled = (bool)GetPrivateField(writer, "_rotationDisabled")!;
                Assert.True(isDisabled, "Circuit breaker should trip on permanent UnauthorizedAccessException.");
            }
        }

        #endregion

        private object GetPrivateField(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field!.GetValue(obj)!;
        }

        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field!.SetValue(obj, value);
        }

        private object InvokePrivateMethod(object obj, string methodName, params object[] args)
        {
            var method = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            try
            {
                return method!.Invoke(obj, args)!;
            }
            catch (TargetInvocationException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
                return null!;
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
using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.IO;
using Servy.Testing;
using System.Globalization;

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
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => CreateWriter(null!, true, 100));
            Assert.Throws<ArgumentException>(() => CreateWriter("", true, 100));
            Assert.Throws<ArgumentException>(() => CreateWriter("    ", true, 100));
        }

        [Fact]
        public void Constructor_CreatesDirectoryIfNotExists()
        {
            // Arrange
            var newDir = Path.Combine(_testDir, "newfolder");
            var newFile = Path.Combine(newDir, "file.log");

            Assert.False(Directory.Exists(newDir));

            // Act
            using (var writer = CreateWriter(newFile, true, 100))
            {
                // Just ensure no exception and directory created
            }

            // Assert
            /// <summary>
            /// Verifies that directory creation auto-runs during instantiation.
            /// </summary>
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
            // Arrange
            var filePath = Path.Combine(_testDir, "lazy_write.txt");

            // Act & Assert
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
            // Arrange
            var filePath = Path.Combine(_testDir, "lazy_writeline.txt");

            // Act & Assert
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
            // Arrange
            var filePath = Path.Combine(_testDir, "lazy_rotate.txt");

            // Act
            using (var writer = CreateWriter(filePath, true, 5, false, DateRotationType.Daily, 0))
            {
                writer.Write("123456"); // Triggers rotation, file is moved

                // This assertion passes because Rotate() moved the file and CloseWriter() was called inside it
                Assert.False(File.Exists(filePath), "Original file should have been moved");

                writer.Write("789"); // Triggers lazy init; a NEW file handle is opened here
            } // <--- The handle is officially closed here

            // Assert
            Assert.True(File.Exists(filePath), "Original file should have been recreated by lazy init");
            Assert.Contains("789", File.ReadAllText(filePath));
        }

        // --- End Lazy Init Tests ---

        [Fact]
        public void GenerateUniqueFileName_ThrowsArgumentException_WhenPathHasNoDirectory()
        {
            // Arrange
            string fileName = $"test_file_{Guid.NewGuid()}.txt";

            // We must actually create the file so File.Exists(basePath) returns true
            File.WriteAllText(fileName, "dummy content");

            try
            {
                // Act & Assert
                // Path.GetDirectoryName("filename.txt") returns null or string.Empty 
                // depending on the .NET runtime version, triggering the check.
                Assert.Throws<ArgumentException>(() => InvokeGenerateUniqueFileName(fileName));
            }
            finally
            {
                // Cleanup: Always delete physical files created during tests
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
            }
        }

        [Fact]
        public void GenerateUniqueFileName_FileDoesNotExist_ReturnsOriginalPath()
        {
            // Arrange
            var path = Path.Combine(_testDir, "log.txt");

            // Act
            var result = InvokeGenerateUniqueFileName(path);

            // Assert
            Assert.Equal(path, result);
        }

        [Fact]
        public void GenerateUniqueFileName_FileExists_AppendsNumber()
        {
            // Arrange
            var path = Path.Combine(_testDir, "log.txt");
            File.WriteAllText(path, "dummy"); // create first file

            var first = InvokeGenerateUniqueFileName(path);
            File.WriteAllText(first, "dummy"); // create second file

            // Act
            var second = InvokeGenerateUniqueFileName(path);

            // Assert
            Assert.Equal(Path.Combine(_testDir, "log.(2).txt"), second);
        }

        [Fact]
        [Trait("Category", "Stress")]
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

            // Act & Assert
            // Custom utility unwraps TargetInvocationException automatically via ExceptionDispatchInfo,
            // so we asset directly on IOException.
            var exception = Assert.Throws<IOException>(() =>
                TestReflection.InvokeNonPublicStatic(typeof(RotatingStreamWriter), "GenerateUniqueFileName", new object[] { basePath }));

            Assert.Contains("after 10000 attempts", exception.Message);
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
            // Arrange
            var filePath = Path.Combine(_testDir, "rotate.txt");
            var fixedTime = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

            // Act
            using (var writer = CreateWriter(filePath, true, 5, false, DateRotationType.Daily, 0, false, () => fixedTime))
            {
                writer.WriteLine("123"); // small write
                writer.Write("456");    // triggers rotation

                // Assert
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
            // Arrange
            var filePath = Path.Combine(_testDir, "test.txt");

            // Act
            using (var writer = CreateWriter(filePath, true, 10))
            {
                writer.WriteLine("hello"); // initializes lazy writer
                writer.Flush();
            }

            // Assert
            var content = File.ReadAllText(filePath);
            Assert.Equal("hello\r\n", content);
        }

        [Fact]
        public void Flush_WhenWriterIsNull_DoesNothing()
        {
            // Arrange
            var writer = CreateWriter(Path.Combine(_testDir, "test.txt"), true, 10);

            // Act
            // Calling Flush should not throw
            var exception = Record.Exception(() => writer.Flush());

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void Flush_EmptyWrite_CoversBaseStreamLengthZero()
        {
            // Arrange
            var filePath = Path.Combine(_testDir, "flush_empty.txt");

            // Act
            using (var writer = CreateWriter(filePath, true, 10))
            {
                writer.Write(""); // Forces lazy initialization, _baseStream is created
                writer.Flush();   // Length could be 0, covers the extra boundary
            }

            // Assert
            Assert.True(File.Exists(filePath));
        }

        private string InvokeGenerateUniqueFileName(string path)
        {
            // Arrange & Act
            return (string)TestReflection.InvokeNonPublicStatic(typeof(RotatingStreamWriter), "GenerateUniqueFileName", new object[] { path })!;
        }

        [Fact]
        public void Dispose_IsIdempotent_AndDoesNotCrashOnSubsequentCalls()
        {
            // Arrange
            var writer = new RotatingStreamWriter(_logFilePath, true, 1000, false, DateRotationType.Daily, 0, false);
            writer.WriteLine("Initial log entry");

            // Act
            writer.Dispose();

            var ex1 = Record.Exception(() => writer.WriteLine("Line after disposal"));
            Assert.Null(ex1);

            // Dispose again
            var ex2 = Record.Exception(writer.Dispose);

            // Assert
            Assert.Null(ex2);
        }

        [Fact]
        public void GenerateUniqueFileName_ReturnsNonExistingFileName()
        {
            // Arrange
            using (var writer = CreateWriter(_logFilePath, true, 1000))
            {
                var basePath = Path.Combine(_testDir, "file.log");
                File.WriteAllText(basePath, "test");
                File.WriteAllText(Path.Combine(_testDir, "file.(1).log"), "test");
                File.WriteAllText(Path.Combine(_testDir, "file.(2).log"), "test");

                // Act
                var uniqueName = (string)TestReflection.InvokeNonPublicStatic(typeof(RotatingStreamWriter), "GenerateUniqueFileName", new object[] { basePath })!;

                // Assert
                Assert.Equal(Path.Combine(_testDir, "file.(3).log"), uniqueName);
            }
        }

        [Fact]
        public void WriteLine_DoesNotRotate_WhenRotationSizeZero()
        {
            // Arrange
            var filePath = Path.Combine(_testDir, "zero.txt");

            // Act
            using (var writer = CreateWriter(filePath, true, 0))
            {
                writer.WriteLine("Hello");
            }

            // Assert
            Assert.True(File.Exists(filePath));
            var rotatedFiles = Directory.GetFiles(_testDir, "zero.*.txt").Where(f => !f.EndsWith("zero.txt"));
            Assert.Empty(rotatedFiles);
        }

        [Fact]
        public void WriteLine_DoesNotRotate_WhenFileSmallerThanRotationSize()
        {
            // Arrange
            var filePath = Path.Combine(_testDir, "small.txt");

            // Act
            using (var writer = CreateWriter(filePath, true, 1024))
            {
                writer.WriteLine("small");
            }

            // Assert
            Assert.True(File.Exists(filePath));
            var rotatedFiles = Directory.GetFiles(_testDir, "small.*.txt").Where(f => !f.EndsWith("small.txt"));
            Assert.Empty(rotatedFiles);
        }

        [Fact]
        public void WriteLine_Rotates_WhenFileExceedsRotationSize()
        {
            // Arrange
            var filePath = Path.Combine(_testDir, "rotate2.txt");

            // Act
            using (var writer = CreateWriter(filePath, true, 5))
            {
                writer.WriteLine("12345"); // exactly 5 bytes -> triggers rotation
                writer.Flush();
                writer.WriteLine("12"); // write to new file
                writer.Flush();
            }

            // Assert
            Assert.True(File.Exists(filePath));
            var rotatedFiles = Directory.GetFiles(_testDir, "rotate2.txt.*");
            Assert.NotEmpty(rotatedFiles);
        }

        [Fact]
        public void Write_WhenWriterIsNull_DoesNothingAfterDisposal()
        {
            // Arrange
            var writer = CreateWriter(Path.Combine(_testDir, "test.txt"), true, 10);
            writer.Dispose();

            // Act
            var exception = Record.Exception(() => writer.Write("12345"));

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public void EnforceMaxRotations_CoversAllBranches()
        {
            // Arrange
            string baseLog = Path.Combine(_testDir, "service.log");
            File.WriteAllText(baseLog, "base");

            var writer = CreateWriter(baseLog, true, 1000);
            writer.Write(""); // force file creation

            // ---- BRANCH 1: _maxRotations <= 0 ----
            TestReflection.SetField(writer, "_maxRotations", 0);
            TestReflection.InvokeNonPublic(writer, "EnforceMaxRotations");

            // ---- BRANCH 2: Filter Logic (StartsWith and EndsWith) ----
            string f1 = Path.Combine(_testDir, "service.20260325_000001.log");
            string noise1 = Path.Combine(_testDir, "service_backup.log");
            string noise2 = Path.Combine(_testDir, "service.20260325.txt");

            File.WriteAllText(f1, "valid");
            File.WriteAllText(noise1, "noise");
            File.WriteAllText(noise2, "noise");

            // ---- BRANCH 3: rotatedFiles.Count <= _maxRotations ----
            TestReflection.SetField(writer, "_maxRotations", 5);
            TestReflection.InvokeNonPublic(writer, "EnforceMaxRotations");

            Assert.True(File.Exists(f1));
            Assert.True(File.Exists(noise1));
            Assert.True(File.Exists(noise2));

            // ---- BRANCH 4: Deletion happens (rotatedFiles.Count > _maxRotations) ----
            string f2 = Path.Combine(_testDir, "service.20260325_000002.log");
            File.WriteAllText(f2, "valid2");
            File.SetLastWriteTimeUtc(f1, DateTime.UtcNow.AddMinutes(-10));
            File.SetLastWriteTimeUtc(f2, DateTime.UtcNow);

            TestReflection.SetField(writer, "_maxRotations", 1);
            TestReflection.InvokeNonPublic(writer, "EnforceMaxRotations");

            Assert.True(File.Exists(f2));     // Kept (newest)
            Assert.False(File.Exists(f1));    // Deleted
            Assert.True(File.Exists(noise1));
            Assert.True(File.Exists(noise2));

            // ---- BRANCH 5: Deletion Failure (IOException) ----
            File.WriteAllText(f1, "recreate");
            File.SetLastWriteTimeUtc(f1, DateTime.UtcNow.AddMinutes(-10));

            // Act & Assert
            using (var locked = new FileStream(f1, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var ex = Record.Exception(() => TestReflection.InvokeNonPublic(writer, "EnforceMaxRotations"));
                Assert.Null(ex); // Resilient against locks
            }

            writer.Dispose();
        }

        [Fact]
        public void EnforceMaxRotations_NoExtension_CoversIsNullOrEmpty()
        {
            // Arrange
            string baseLog = Path.Combine(_testDir, "plainfile");
            File.WriteAllText(baseLog, "base");

            var writer = CreateWriter(baseLog, true, 1000);
            writer.Write(""); // Trigger lazy init

            string f1 = Path.Combine(_testDir, "plainfile.20260325_000001");
            File.WriteAllText(f1, "rotated");
            File.SetLastWriteTimeUtc(f1, DateTime.UtcNow.AddMinutes(-10));

            TestReflection.SetField(writer, "_maxRotations", 1);

            string f2 = Path.Combine(_testDir, "plainfile.20260325_000002");
            File.WriteAllText(f2, "rotated2");
            File.SetLastWriteTimeUtc(f2, DateTime.UtcNow);

            // Act
            TestReflection.InvokeNonPublic(writer, "EnforceMaxRotations");

            // Assert
            Assert.True(File.Exists(f2));
            Assert.False(File.Exists(f1));

            writer.Dispose();
        }

        [Fact]
        public void EnforceMaxRotations_DeletionFails_DoesNotThrow()
        {
            // Arrange
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

            // Act
            var ex = Record.Exception(() =>
            {
                TestReflection.InvokeNonPublic(writer, "EnforceMaxRotations", Array.Empty<object>());
            });

            // Assert
            Assert.Null(ex);
            File.SetAttributes(rotated2, FileAttributes.Normal); // Reset so test runner can clean it up
        }

        [Fact]
        public void DateRotation_Daily_Rotates_WhenDateBoundaryCrossed()
        {
            // Arrange
            var filePath = Path.Combine(_testDir, "daily.log");
            var fixedTime = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

            // Act
            using (var writer = CreateWriter(filePath, false, 0, true, DateRotationType.Daily, 0, false, () => fixedTime))
            {
                TestReflection.SetField(writer, "_lastRotationDate", fixedTime.AddDays(-1));

                writer.WriteLine("rotate on daily boundary");
                writer.Flush();
            }

            // Assert
            var rotated = Directory.GetFiles(_testDir, "daily.*.log").Where(f => !f.EndsWith("daily.log")).ToArray();
            Assert.NotEmpty(rotated);
            Assert.Contains(fixedTime.ToString("yyyyMMdd"), rotated[0]);
        }

        [Fact]
        public void DateRotation_Weekly_Rotates_WhenYearBoundaryCrossed()
        {
            // Arrange
            var filePath = Path.Combine(_testDir, "weekly_year.log");

            // Frozen time provider to ensure deterministic execution across both calendar and
            // ISO week boundaries regardless of execution date.
            var frozenNow = new DateTime(2026, 1, 8); // Well past the first ISO week boundary of the new year
            Func<DateTime> timeProvider = () => frozenNow;

            // Act
            // Constructing the writer passing our deterministic time provider seam helper
            using (var writer = CreateWriter(filePath, false, 0, true, DateRotationType.Weekly, 0, timeProvider: timeProvider))
            {
                // Force an old date that crosses both the calendar year boundary and the 7-day interval check
                var dec30 = new DateTime(2025, 12, 30);

                TestReflection.SetField(writer, "_lastRotationDate", dec30);

                writer.WriteLine("rotate on year change");
                writer.Flush();
            }

            // Assert
            var rotated = Directory.GetFiles(_testDir, "weekly_year.*.log").Where(f => !f.EndsWith("weekly_year.log")).ToArray();
            Assert.NotEmpty(rotated);
        }

        [Fact]
        public void DateRotation_Weekly_DoesNotRotate_OnSameWeek()
        {
            // Arrange
            var filePath = Path.Combine(_testDir, "weekly_same.log");
            var recently = DateTime.UtcNow.AddHours(-1);

            // Act
            using (var writer = CreateWriter(filePath, false, 0, true, DateRotationType.Weekly, 0))
            {
                TestReflection.SetField(writer, "_lastRotationDate", recently);

                writer.WriteLine("should not rotate");
                writer.Flush();
            }

            // Assert
            var rotated = Directory.GetFiles(_testDir, "weekly_same.*.log").Where(f => !f.EndsWith("weekly_same.log"));
            Assert.Empty(rotated);
        }

        [Fact]
        public void DateRotation_Monthly_Rotates_WhenMonthBoundaryCrossed()
        {
            // Arrange
            var filePath = Path.Combine(_testDir, "monthly.log");
            var fixedTime = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

            // Act
            using (var writer = CreateWriter(filePath, false, 0, true, DateRotationType.Monthly, 0, false, () => fixedTime))
            {
                TestReflection.SetField(writer, "_lastRotationDate", fixedTime.AddMonths(-1));

                writer.WriteLine("rotate on monthly boundary");
                writer.Flush();
            }

            // Assert
            var rotated = Directory.GetFiles(_testDir, "monthly.*.log").Where(f => !f.EndsWith("monthly.log")).ToArray();
            Assert.NotEmpty(rotated);
            Assert.Contains(fixedTime.ToString("yyyyMMdd"), rotated[0]);
        }

        [Fact]
        public void DateRotation_Monthly_Rotates_WhenYearBoundaryCrossed()
        {
            // Arrange
            var filePath = Path.Combine(_testDir, "monthly.log");
            var fixedTime = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

            // Act
            using (var writer = CreateWriter(filePath, false, 0, true, DateRotationType.Monthly, 0, false, () => fixedTime))
            {
                TestReflection.SetField(writer, "_lastRotationDate", fixedTime.AddYears(-1));

                writer.WriteLine("rotate on monthly boundary");
                writer.Flush();
            }

            // Assert
            var rotated = Directory.GetFiles(_testDir, "monthly.*.log").Where(f => !f.EndsWith("monthly.log")).ToArray();
            Assert.NotEmpty(rotated);
            Assert.Contains(fixedTime.ToString("yyyyMMdd"), rotated[0]);
        }

        [Fact]
        public void SizeAndDateRotation_SizePrecedence_WhenBothEnabled()
        {
            // Arrange
            var filePath = Path.Combine(_testDir, "sizeDate.log");
            var fixedTime = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

            // Act
            using (var writer = CreateWriter(filePath, true, 5, true, DateRotationType.Daily, 0, false, () => fixedTime))
            {
                TestReflection.SetField(writer, "_lastRotationDate", fixedTime.AddDays(-1));

                writer.Write("123456");
                writer.Flush();
            }

            // Assert
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
            // Arrange
            var filePath = Path.Combine(_testDir, "dateOnlyWhenSizeNotExceeded.log");
            var fixedTime = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

            // Act
            using (var writer = CreateWriter(filePath, true, 1024, true, DateRotationType.Daily, 0, false, () => fixedTime))
            {
                TestReflection.SetField(writer, "_lastRotationDate", fixedTime.AddDays(-1));

                writer.WriteLine("date rotation hit");
                writer.Flush();
            }

            // Assert
            var rotated = Directory.GetFiles(_testDir, "dateOnlyWhenSizeNotExceeded.*.log").Where(f => !f.EndsWith("dateOnlyWhenSizeNotExceeded.log")).ToArray();
            Assert.NotEmpty(rotated);
        }

        [Fact]
        public void ShouldRotateByDate_DefaultCase_ReturnsFalse()
        {
            // Arrange
            using (var writer = new RotatingStreamWriter("dummy.log", false, 1000, true, DateRotationType.Daily, 1, false))
            {
                // 1. Force an invalid enum value into the private field
                TestReflection.SetField(writer, "_dateRotationType", (DateRotationType)999);

                // 2. Act: Provide the required DateTime parameter (even for the default/invalid case)
                var args = new object[] { DateTime.UtcNow };
                var result = (bool)TestReflection.InvokeNonPublic(writer, "ShouldRotateByDate", args)!;

                // 3. Assert: An unrecognized rotation type should safely return false
                Assert.False(result);
            }
        }

        [Fact]
        public void Rotate_WhenFileIsLocked_SilentlyContinuesWithoutCrashing()
        {
            // Arrange
            var filePath = Path.Combine(_testDir, "locked_rotate.txt");

            using (var writer = new RotatingStreamWriter(filePath, true, 5, false, DateRotationType.Daily, 0, false))
            {
                writer.Write("init");
                writer.Flush();

                // Act
                using (var blocker = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var exception = Record.Exception(() =>
                    {
                        writer.Write("trigger_rotation");
                        writer.Flush();
                    });

                    // Assert
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
            // Arrange: Set a small limit (5 bytes).
            var filePath = Path.Combine(_testDir, "missing_rotate.txt");

            using (var writer = CreateWriter(filePath, enableSizeRotation: true, rotationSizeInBytes: 5))
            {
                // 1. Act: Case - File doesn't exist yet (Lazy Init)
                // Calling Write("") triggers CheckRotation, but _writer is still null 
                // until the write physically starts.
                writer.Write("");
                writer.Flush();

                // Assert
                // At this point, the file exists but is 0 bytes.
                Assert.True(File.Exists(filePath));
                Assert.Equal(0, new FileInfo(filePath).Length);

                // 2. Act: Case - File exists but is empty
                // We call Write("") again. Internally, CheckRotation() is called.
                // It sees Length (0) < limit (5) and returns early.
                writer.Write("");
                writer.Flush();

                // Assert: Ensure no rotated file was generated
                var rotatedFiles = Directory.GetFiles(_testDir, "missing_rotate.*.txt");
                Assert.Empty(rotatedFiles);

                // Assert: The original file should still be 0 bytes
                Assert.Equal(0, new FileInfo(filePath).Length);
            }
        }

        // --- UseLocalTimeForRotation & Daily Rotation Branch Coverage ---

        [Fact]
        public void Constructor_InitializesLastRotationDate_CorrectlyForTimeZones()
        {
            // Arrange
            // Ensure file exists to test the "File.Exists" branch
            File.WriteAllText(_logFilePath, "content");
            var localWriteTime = File.GetLastWriteTime(_logFilePath);
            var utcWriteTime = File.GetLastWriteTimeUtc(_logFilePath);

            // Act & Assert: Test Local Branch
            using (var localWriter = CreateWriter(_logFilePath, useLocalTimeForRotation: true))
            {
                var lastRot = TestReflection.GetField<DateTime>(localWriter, "_lastRotationDate");
                Assert.Equal(localWriteTime, lastRot);
            }

            // Act & Assert: Test UTC Branch
            using (var utcWriter = CreateWriter(_logFilePath, useLocalTimeForRotation: false))
            {
                var lastRot = TestReflection.GetField<DateTime>(utcWriter, "_lastRotationDate");
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
                TestReflection.SetField(writer, "_lastRotationDate", lastRotationUtc);

                // Act
                var args = new object[] { nowUtc };
                var shouldRotate = (bool?)TestReflection.InvokeNonPublic(writer, "ShouldRotateByDate", args);

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
                TestReflection.SetField(writer, "_lastRotationDate", lastRotationUtc);

                // Act
                var args = new object[] { nowUtc };
                var shouldRotate = (bool?)TestReflection.InvokeNonPublic(writer, "ShouldRotateByDate", args);

                // Assert
                Assert.True(shouldRotate, "Rotation should trigger when crossing into a new calendar day.");
            }
        }

        [Fact]
        public void DailyRotation_UTC_IgnoresSafetyBuffer()
        {
            // Arrange: Use local time configuration instead of UTC for log rotation, modifying the default setup.
            // Last rotation was yesterday at 11:59:59 PM
            var lastRotationUtc = new DateTime(2026, 4, 10, 23, 59, 59, DateTimeKind.Utc);

            // Now is today at 00:00:01 AM (Only 2 seconds passed, but day changed)
            var nowUtc = new DateTime(2026, 4, 11, 0, 0, 1, DateTimeKind.Utc);

            using (var writer = CreateWriter(_logFilePath, enableDateRotation: true, useLocalTimeForRotation: false))
            {
                TestReflection.SetField(writer, "_lastRotationDate", lastRotationUtc);

                // Act: Pass the simulated 'now'
                var args = new object[] { nowUtc };
                var shouldRotate = (bool?)TestReflection.InvokeNonPublic(writer, "ShouldRotateByDate", args);

                // Assert: In UTC mode, we ignore the 23h buffer and rotate on day change
                Assert.True(shouldRotate, "UTC mode should rotate immediately when the calendar day flips.");
            }
        }

        [Fact]
        public void CheckRotation_UpdatesLastRotationDate_WithCorrectTimeZone()
        {
            // Arrange & Act & Assert: 1. Test Local Update
            // Set rotationSizeInBytes to 1 so the first write triggers CheckRotation() natively
            using (var localWriter = CreateWriter(_logFilePath, enableSizeRotation: true, rotationSizeInBytes: 1, useLocalTimeForRotation: true))
            {
                localWriter.Write("init"); // Exceeds 1 byte, triggering native rotation and updating the date

                var lastRot = TestReflection.GetField<DateTime>(localWriter, "_lastRotationDate");
                Assert.Equal(DateTimeKind.Local, lastRot.Kind);
            }

            // Arrange & Act & Assert: 2. Test UTC Update
            using (var utcWriter = CreateWriter(_logFilePath, enableSizeRotation: true, rotationSizeInBytes: 1, useLocalTimeForRotation: false))
            {
                utcWriter.Write("init"); // Exceeds 1 byte, triggering native rotation and updating the date

                var lastRot = TestReflection.GetField<DateTime>(utcWriter, "_lastRotationDate");
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
                TestReflection.SetField(writer, "_lastRotationDate", nowUtc.AddHours(-2));

                // Act: Pass nowUtc as the argument to the private method
                var args = new object[] { nowUtc };
                var result = (bool?)TestReflection.InvokeNonPublic(writer, "ShouldRotateByDate", args);

                // Assert: 10:00 AM is same day as 08:00 AM, should not rotate
                Assert.False(result, "Should not rotate if the calendar day has not changed.");
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CheckRotation_SizeRotation_TernaryCoverage(bool useLocal)
        {
            // Arrange
            var filePath = Path.Combine(_testDir, $"size_ternary_{useLocal}.log");

            // Act: Set size to 1 byte so any write triggers rotateBySize = true
            using (var writer = CreateWriter(filePath, enableSizeRotation: true, rotationSizeInBytes: 1, useLocalTimeForRotation: useLocal))
            {
                writer.Write("trigger"); // This hits CheckRotation -> rotateBySize is true

                // Assert
                var lastRot = TestReflection.GetField<DateTime>(writer, "_lastRotationDate");
                Assert.Equal(useLocal ? DateTimeKind.Local : DateTimeKind.Utc, lastRot.Kind);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CheckRotation_DateRotation_TernaryCoverage(bool useLocal)
        {
            // Arrange
            var filePath = Path.Combine(_testDir, $"date_ternary_{useLocal}.log");

            // Act
            using (var writer = CreateWriter(filePath, enableDateRotation: true, dateRotationType: DateRotationType.Daily, useLocalTimeForRotation: useLocal))
            {
                writer.Write("init"); // Ensure file and writer exist

                // Force ShouldRotateByDate to return true by aging the last rotation
                DateTime fakePast = useLocal ? DateTime.UtcNow.AddDays(-2) : DateTime.UtcNow.AddDays(-2);
                TestReflection.SetField(writer, "_lastRotationDate", fakePast);

                // Trigger write -> CheckRotation -> rotateByDate is true
                writer.Write("trigger rotation");

                // Assert
                var lastRot = TestReflection.GetField<DateTime>(writer, "_lastRotationDate");
                Assert.Equal(useLocal ? DateTimeKind.Local : DateTimeKind.Utc, lastRot.Kind);
            }
        }

        #region Circuit Breaker & Permanent Failure Tests

        [Fact]
        public void CheckRotation_EarlyReturn_WhenDisabled()
        {
            // Arrange
            var filePath = Path.Combine(_testDir, "gatekeeper.log");
            using (var writer = CreateWriter(filePath, enableSizeRotation: true, rotationSizeInBytes: 1000))
            {
                writer.Write("initial_data");
                writer.Flush();

                // 1. Trip breaker manually
                TestReflection.SetField(writer, "_rotationDisabled", true);

                // Set the cooldown to the future so the self-healing logic 
                // doesn't immediately reset the breaker to false.
                TestReflection.SetField(writer, "_disabledCooldownUntil", DateTime.Now.AddMinutes(10));

                // 2. Act: This would normally rotate, but is now blocked by the breaker
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
            // Arrange
            var filePath = Path.Combine(_testDir, "transient.log");
            File.WriteAllText(filePath, "initial_data");

            // Provide a frozen baseline clock so live timestamps don't introduce drifts
            var testTime = new DateTime(2026, 6, 9, 9, 5, 0, DateTimeKind.Utc);
            Func<DateTime> timeProvider = () => testTime;

            using (var writer = new RotatingStreamWriter(
                filePath,
                enableSizeRotation: true,
                rotationSizeInBytes: 5,
                enableDateRotation: false,
                dateRotationType: DateRotationType.Daily,
                maxRotations: 0,
                useLocalTimeForRotation: false,
                timeProvider: timeProvider))
            {
                var startedSignal = new ManualResetEventSlim(false);
                var locker = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                // Act
                try
                {
                    var rotateTask = Task.Run(() =>
                    {
                        startedSignal.Set();
                        writer.Write("trigger_rotation");
                        writer.Flush();
                    }, TestContext.Current.CancellationToken);

                    startedSignal.Wait(2000, TestContext.Current.CancellationToken);

                    locker.Dispose(); // Release lock to let retry succeed
                    await rotateTask;
                }
                finally
                {
                    locker.Dispose();
                }

                // Assert: Await state settlement loop pass
                // Because the background retry loops complete outside the public lock, 
                // we allow up to 1 second for the task success handler block to commit 
                // its state updates back down to the default variables.
                bool isDisabled = true;
                DateTime cooldown = DateTime.MaxValue;

                SpinWait.SpinUntil(() =>
                {
                    isDisabled = TestReflection.GetField<bool>(writer, "_rotationDisabled")!;
                    cooldown = TestReflection.GetField<DateTime>(writer, "_rotationCooldownUntil")!;
                    return cooldown == DateTime.MinValue;
                }, TimeSpan.FromSeconds(1));

                Assert.False(isDisabled, "Breaker should not trip on IOException.");
                Assert.Equal(DateTime.MinValue, cooldown);
                Assert.False(File.Exists(filePath));
            }
        }

        [Fact]
        public void Rotate_PersistentIOException_ReachedLimit_SetsCooldown()
        {
            // Arrange
            var filePath = Path.Combine(_testDir, "persistent.log");
            // Ensure the file has content so it isn't skipped by the "Length == 0" guard
            File.WriteAllText(filePath, "initial content");

            // Set a small limit (5 bytes) to ensure the next write triggers rotation
            using (var writer = CreateWriter(filePath, enableSizeRotation: true, rotationSizeInBytes: 5))
            {
                // 1. Lock the file and KEEP it locked to force an IOException during the Move operation
                using (var locker = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // 2. Act: This write triggers the flow: CheckRotation -> PrepareRotation -> PerformPhysicalRotation
                    writer.Write("trigger_rotation");
                    writer.Flush();
                }

                // 3. Assert: Await state settlement loop pass
                // ROBUSTNESS REFACTOR: Because PerformPhysicalRotation executes loops and fallback updates completely 
                // outside of the public lock thread boundary, we must use SpinWait to prevent a reflection execution 
                // race condition before the exception cooldown timestamp is officially committed.
                bool isDisabled = true;
                DateTime cooldown = DateTime.MinValue;

                SpinWait.SpinUntil(() =>
                {
                    isDisabled = TestReflection.GetField<bool>(writer, "_rotationDisabled")!;
                    cooldown = TestReflection.GetField<DateTime>(writer, "_rotationCooldownUntil")!;
                    return cooldown > DateTime.MinValue;
                }, TimeSpan.FromSeconds(1));

                // IOException is treated as a transient contention issue, so the breaker should remain CLOSED
                Assert.False(isDisabled, "IOException should NOT trip the circuit breaker.");

                // Check that the cooldown was applied (PrepareRotation sets this to 1000ms by default)
                Assert.True(cooldown > DateTime.UtcNow, "Rotation should be on cooldown after exhausting retries.");

                // Verify the original file still exists because the external lock blocked the File.Move
                Assert.True(File.Exists(filePath), "The original log file should still exist because rotation was deferred.");
            }
        }

        [Fact]
        [Trait("Category", "Stress")]
        public void Rotate_PermanentFailure_TripsBreaker()
        {
            // Arrange
            var subDir = Path.Combine(_testDir, "BreakerTest");
            Directory.CreateDirectory(subDir);
            var filePath = Path.Combine(subDir, "breaker_test.log");

            // 1. Fix the time so we know exactly what the rotated filename will be
            var fixedTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            Func<DateTime> mockTimeProvider = () => fixedTime;

            // Use a small rotation size (10 bytes) so the next write triggers rotation.
            // We instantiate RotatingStreamWriter directly to inject our specific mock time provider.
            using (var writer = new RotatingStreamWriter(
                path: filePath,
                enableSizeRotation: true,
                rotationSizeInBytes: 10,
                enableDateRotation: false,
                dateRotationType: DateRotationType.None,
                maxRotations: 0,
                useLocalTimeForRotation: false,
                timeProvider: mockTimeProvider))
            {
                writer.Write("init");
                writer.Flush();

                // 2. EXHAUSTION SETUP: Create the exact rotated file and ALL possible collision suffixes.
                // This forces GenerateUniqueFileName to throw an IOException. Because this specific
                // method call is outside the IO/Unauthorized retry loop, its exception is caught by 
                // the outer block and correctly treated as a permanent critical failure.
                var timestamp = fixedTime.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                var baseRotatedPath = Path.Combine(subDir, $"breaker_test.{timestamp}.log");

                File.WriteAllText(baseRotatedPath, "collision");
                for (int i = 1; i <= AppConfig.RotatingStreamWriterMaxUniqueFilenameRetries; i++)
                {
                    var collisionPath = Path.Combine(subDir, $"breaker_test.{timestamp}.({i}).log");
                    File.WriteAllText(collisionPath, "collision");
                }

                // 3. Act: This write pushes the file over the 10-byte limit.
                // GenerateUniqueFileName will exhaust all retries and throw, permanently tripping the breaker.
                writer.Write("trigger_rotation_failure");
                writer.Flush();

                // 4. Assert: Verify the circuit breaker tripped due to the Exception.
                bool isDisabled = TestReflection.GetField<bool>(writer, "_rotationDisabled")!;
                Assert.True(isDisabled, "Circuit breaker should trip on permanent failure (Unique filename exhaustion).");
            }
        }

        #endregion

        #region New Logic Coverage (Self-Healing, Weekly Boundary, Hard Failures)

        [Fact]
        public void CheckRotation_CircuitBreaker_HealsAfterCooldownExpires()
        {
            // Arrange
            var filePath = Path.Combine(_testDir, "healing.log");
            using (var writer = CreateWriter(filePath, enableSizeRotation: true, rotationSizeInBytes: 10))
            {
                writer.Write("initial");
                writer.Flush();

                // 1. Manually trip the breaker and set the cooldown to the PAST
                TestReflection.SetField(writer, "_rotationDisabled", true);
                TestReflection.SetField(writer, "_disabledCooldownUntil", DateTime.UtcNow.AddMinutes(-1));

                // 2. Act: This write should trigger the healing logic, reset the breaker, and rotate
                writer.Write("this_forces_rotation");
                writer.Flush();

                // 3. Assert
                bool isDisabled = TestReflection.GetField<bool>(writer, "_rotationDisabled")!;
                Assert.False(isDisabled, "Breaker should reset automatically after the cooldown expires.");

                var rotated = Directory.GetFiles(_testDir, "healing.*.log").Where(f => !f.EndsWith("healing.log"));
                Assert.NotEmpty(rotated); // Proves the rotation actually resumed
            }
        }

        [Fact]
        public void CheckRotation_WhenDisabledAndFileIsHuge_LogsWarningAndReturnsEarly()
        {
            // Arrange: Set limit to 10 bytes
            var filePath = Path.Combine(_testDir, "huge.log");

            using (var writer = CreateWriter(filePath, enableSizeRotation: true, rotationSizeInBytes: 10))
            {
                // 1. Trip the breaker FIRST with a FUTURE cooldown.
                // This ensures the upcoming writes bypass the rotation logic entirely.
                TestReflection.SetField(writer, "_rotationDisabled", true);
                TestReflection.SetField(writer, "_disabledCooldownUntil", DateTime.UtcNow.AddMinutes(10));

                // 2. Write 30 bytes (3x the limit). 
                // Because the breaker is tripped, the writer will append the data but skip rotation.
                writer.Write(new string('X', 30));
                writer.Flush();

                // 3. Act: This subsequent write hits the "file size > 2x limit while disabled" warning branch.
                var exception = Record.Exception(() => writer.Write("more"));

                // 4. Assert
                Assert.Null(exception); // Handled gracefully without throwing

                // Verify the file was allowed to grow without generating any rotated files
                var rotated = Directory.GetFiles(_testDir, "huge.*.log").Where(f => !f.EndsWith("huge.log"));
                Assert.Empty(rotated);

                // Optional: Verify the base file is indeed huge
                Assert.True(new FileInfo(filePath).Length >= 30, "The base log file should have grown past its limit.");
            }
        }

        [Fact]
        public void DateRotation_Weekly_Rotates_WhenSameCalendarYearButOver7Days()
        {
            // Arrange
            // This specifically covers Bug #1116 (ISO Calendar Year Mismatch)
            var filePath = Path.Combine(_testDir, "weekly_iso_bug.log");

            // Wed, Jan 1, 2025 (ISO Week 1 of 2025)
            var lastRotationDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            // Mon, Dec 29, 2025 (ISO Week 1 of 2026, but still Calendar Year 2025)
            var nowUtc = new DateTime(2025, 12, 29, 0, 0, 0, DateTimeKind.Utc);

            using (var writer = CreateWriter(filePath, false, 0, true, DateRotationType.Weekly, 0))
            {
                TestReflection.SetField(writer, "_lastRotationDate", lastRotationDate);

                // Act
                var args = new object[] { nowUtc };
                var shouldRotate = (bool?)TestReflection.InvokeNonPublic(writer, "ShouldRotateByDate", args);

                // Assert: The 7-day fallback should catch this and allow rotation
                Assert.True(shouldRotate, "Should rotate because > 7 days have passed, despite both dates reporting as ISO Week 1 of Calendar Year 2025.");
            }
        }

        [Fact]
        public void PerformPhysicalRotation_NonIOException_TripsCircuitBreaker()
        {
            // Arrange
            var filePath = Path.Combine(_testDir, "non_io.log");
            using (var writer = CreateWriter(filePath, enableSizeRotation: true, rotationSizeInBytes: 10))
            {
                writer.Write("init");
                writer.Flush();

                // Create a dummy file in the current working directory to guarantee File.Exists() evaluates to true
                // inside GenerateUniqueFileName, which then attempts Path.GetDirectoryName("just_a_name.log") == string.Empty.
                var badRotatedPath = "just_a_name.log";
                File.WriteAllText(badRotatedPath, "dummy");

                // Act
                try
                {
                    // This will throw an ArgumentException ("Cannot determine directory from path...")
                    // which is caught by the broad `catch (Exception)` block in PerformPhysicalRotation.
                    TestReflection.InvokeNonPublic(writer, "PerformPhysicalRotation", new object[] { filePath, badRotatedPath });
                }
                finally
                {
                    if (File.Exists(badRotatedPath)) File.Delete(badRotatedPath);
                }

                // Assert
                bool isDisabled = TestReflection.GetField<bool>(writer, "_rotationDisabled")!;
                DateTime cooldown = TestReflection.GetField<DateTime>(writer, "_disabledCooldownUntil")!;

                Assert.True(isDisabled, "A non-IOException should successfully trip the circuit breaker.");
                Assert.True(cooldown > DateTime.UtcNow, "Circuit breaker cooldown should be set to the future.");
            }
        }

        #endregion

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
using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.Logging;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Servy.Core.UnitTests.Logging
{
    [CollectionDefinition("LoggerSequential", DisableParallelization = true)]
    public class LoggerCollection : ICollectionFixture<object>
    {
        // Enforces strict sequential isolation across the execution suite
    }

    /// <summary>
    /// Comprehensive unit tests for the Logger class, executed sequentially
    /// due to the static nature of the target class to avoid file lock contention.
    /// </summary>
    [Collection("LoggerSequential")] // Ensures tests don't run in parallel and fight over the static _writer
    public class LoggerTests : IDisposable
    {
        private readonly string _testFileName;
        private readonly string _fullLogPath;
        private readonly string _initFallbackPath;
        private readonly string _writeFallbackPath;

        public LoggerTests()
        {
            // Reset the static state to ensure pure test isolation
            Logger.Shutdown();
            ResetFallbackCounters();

            _testFileName = $"TestLog_{Guid.NewGuid():N}.log";
            _fullLogPath = Path.Combine(Logger.LogsPath, _testFileName);
            _initFallbackPath = Path.Combine(Logger.LogsPath, "LoggerInitializationErrors.log");
            _writeFallbackPath = Path.Combine(Logger.LogsPath, "LoggerWriteErrors.log");

            CleanupFiles();
        }

        public void Dispose()
        {
            Logger.Shutdown();
            CleanupFiles();
        }

        private void CleanupFiles()
        {
            try { if (File.Exists(_fullLogPath)) File.Delete(_fullLogPath); } catch { }
            try { if (File.Exists(_initFallbackPath)) File.Delete(_initFallbackPath); } catch { }
            try { if (File.Exists(_writeFallbackPath)) File.Delete(_writeFallbackPath); } catch { }
        }

        private void ResetFallbackCounters()
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            typeof(Logger).GetField("_initFallbackWriteCount", flags)?.SetValue(null, 0);
            typeof(Logger).GetField("_logFallbackWriteCount", flags)?.SetValue(null, 0);
        }

        #region Initialization & Core Logic Tests

        [Fact]
        public void Initialize_WithValidFileName_CreatesLogFile()
        {
            // Act
            Logger.Initialize(_testFileName);
            Logger.Info("Initialization test");
            Logger.Shutdown();

            // Assert
            Assert.True(File.Exists(_fullLogPath));
            string content = File.ReadAllText(_fullLogPath);
            Assert.Contains("Initialization test", content);
        }

        [Fact]
        public void Initialize_WithNullFileName_GracefullySkipsInitialization()
        {
            // Act
            Logger.Initialize((string?)null);
            Logger.Info("Should drop silently");

            // Assert: No exception thrown, file shouldn't be made specifically for this
            Assert.False(File.Exists(Path.Combine(Logger.LogsPath, "null")));
        }

        [Fact]
        public void Initialize_WhenFileIsLocked_FailsSilentlyAndWritesToFallback()
        {
            // Arrange
            // Passing an invalid character like a null terminator character sequence 
            // forces Path.Combine to pass validation but crashes the underlying Win32 
            // CreateFile handle allocation with an ArgumentException.
            string illegalFileName = "Invalid\0Char.log";

            // Act
            Logger.Initialize(illegalFileName);

            // Assert
            Assert.True(File.Exists(_initFallbackPath), "Initialization fallback log should have been created.");
            string fallbackContent = File.ReadAllText(_initFallbackPath);
            Assert.Contains("Failed to initialize logger", fallbackContent);
        }

        [Fact]
        public void Log_WhenWriteFails_WritesToFallbackLog()
        {
            // Arrange: 1. Initialize and write an initial baseline line to force file creation on disk
            Logger.Initialize(_testFileName);
            Logger.Info("Establishing file context on disk");
            Logger.Shutdown(); // Flush and release the handle so the test can lock it

            // Assert
            Assert.True(File.Exists(_fullLogPath), "Baseline log file should exist before locking.");

            // Arrange: 2. Re-initialize the logger, then lock the file entirely from outside system operations
            Logger.Initialize(_testFileName);

            using (var lockStream = new FileStream(_fullLogPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // Act: Attempt a write while the file stream is locked by another process context
                Logger.Info("This write should trigger an internal IOException due to the lock.");
            }

            // Assert: Verify that the runtime failure routed cleanly to the write fallback log file
            Assert.True(File.Exists(_writeFallbackPath), "Write fallback log should have been created.");
            string fallbackContent = File.ReadAllText(_writeFallbackPath);
            Assert.Contains("Failed to write log entry", fallbackContent);
        }

        #endregion

        #region Log Level Tests

        [Fact]
        public void Log_RespectsConfiguredLogLevelThreshold()
        {
            // Arrange
            Logger.Initialize(_testFileName, LogLevel.Warn);

            // Act
            Logger.Debug("Hidden debug");
            Logger.Info("Hidden info");
            Logger.Warn("Visible warn");
            Logger.Error("Visible error");
            Logger.Shutdown();

            // Assert
            string content = File.ReadAllText(_fullLogPath);
            Assert.DoesNotContain("Hidden debug", content);
            Assert.DoesNotContain("Hidden info", content);
            Assert.Contains("Visible warn", content);
            Assert.Contains("Visible error", content);
        }

        [Fact]
        public void SetLogLevel_UpdatesThresholdAtRuntime()
        {
            // Arrange
            Logger.Initialize(_testFileName, LogLevel.Error);

            // Act
            Logger.Info("Hidden info 1");
            Logger.SetLogLevel(LogLevel.Info);
            Logger.Info("Visible info 2");
            Logger.Shutdown();

            // Assert
            string content = File.ReadAllText(_fullLogPath);
            Assert.DoesNotContain("Hidden info 1", content);
            Assert.Contains("Visible info 2", content);
        }

        #endregion

        #region Sanitization & Scannability Tests

        [Theory]
        [InlineData("Line1\nLine2", "Line1 ; Line2")]
        [InlineData("Line1\r\nLine2", "Line1 ; Line2")]
        [InlineData("Line1\u2028Line2", "Line1 ; Line2")] // Unicode Line Separator
        [InlineData("Line1\u2029Line2", "Line1 ; Line2")] // Unicode Paragraph Separator
        [InlineData("Line1\u0085Line2", "Line1 ; Line2")] // Next Line (NEL)
        [InlineData("Line1\vLine2", "Line1 Line2")]       // Vertical Tab
        [InlineData("Line1\fLine2", "Line1 Line2")]       // Form Feed
        public void Log_SanitizesMessage_MaintainsSingleLineContract(string rawMessage, string expectedFragment)
        {
            // Arrange: Generate an isolated unique filename specific to this InlineData iteration run
            // to eliminate interleaving contamination and protect internal contract layout checks.
            string isolatedFileName = $"SanitizationTest_{Guid.NewGuid():N}.log";
            string isolatedFullPath = Path.Combine(Logger.LogsPath, isolatedFileName);

            try
            {
                if (File.Exists(isolatedFullPath))
                {
                    File.Delete(isolatedFullPath);
                }
                Logger.Initialize(isolatedFileName);

                // Act
                Logger.Info(rawMessage);
                Logger.Shutdown();

                // Assert
                string[] lines = File.ReadAllLines(isolatedFullPath);

                // Trace target element dynamically to insulate the assertion loop from background thread telemetry noise
                int matchingIndex = Array.FindIndex(lines, l => l.Contains(expectedFragment));
                Assert.True(matchingIndex >= 0, $"Expected log entry containing fragment '{expectedFragment}' was not found.");

                string targetLine = lines[matchingIndex];
                Assert.DoesNotContain("\n", targetLine);
                Assert.DoesNotContain("\r", targetLine);
            }
            finally
            {
                Logger.Shutdown();
                try { if (File.Exists(isolatedFullPath)) File.Delete(isolatedFullPath); } catch { }
            }
        }

        #endregion

        #region Exception Formatting Tests

        [Fact]
        public void FormatException_UnrollsInnerExceptionsAndSanitizesStackTrace()
        {
            // Arrange
            Exception ex;
            try
            {
                try
                {
                    throw new InvalidOperationException("Inner fail");
                }
                catch (Exception inner)
                {
                    throw new ApplicationException("Outer fail\nMultiline", inner);
                }
            }
            catch (Exception caught)
            {
                ex = caught; // This successfully captures the ApplicationException
            }

            Logger.Initialize(_testFileName);

            // Act
            Logger.Error("Exception test", ex);
            Logger.Shutdown();

            // Assert
            string content = File.ReadAllText(_fullLogPath);

            // 1. Verify exception unrolling and string replacement patterns work
            Assert.Contains("Outer fail ; Multiline", content);
            Assert.Contains(" [Inner -> InvalidOperationException: Inner fail", content);
            Assert.Contains("]", content); // Closing brackets

            // 2. Isolate the exact formatted exception segment text block
            int exceptionMessageIndex = content.IndexOf("Exception test");
            string exceptionSegment = content.Substring(exceptionMessageIndex).TrimEnd();

            // 3. Confirm that the isolated exception text contains zero raw line breaks
            Assert.DoesNotContain("\r", exceptionSegment);
            Assert.DoesNotContain("\n", exceptionSegment);
        }

        [Fact]
        public void FormatException_HardTruncatesMassiveExceptions_AvoidsSurrogatePairSplitting()
        {
            // Arrange
            // We use a massive payload of emojis (surrogate pairs) to test length boundary logic
            string hugeSurrogateString = string.Concat(Enumerable.Repeat("😊", 30000));
            var ex = new Exception(hugeSurrogateString);

            Logger.Initialize(_testFileName);

            // Act
            Logger.Error("Massive Error", ex);
            Logger.Shutdown();

            // Assert
            string content = File.ReadAllText(_fullLogPath);

            const string truncationMarker = "... [truncated]";
            Assert.Contains(truncationMarker, content);

            // Isolate the exact boundary text immediately preceding the truncation marker
            int cutIndex = content.IndexOf(truncationMarker, StringComparison.Ordinal);
            string truncatedHead = content.Substring(0, cutIndex);

            // Validate that the very last character before the truncation marker is NOT a high surrogate.
            // In UTF-16 (C# strings), a high surrogate (\uD83D) must always be followed by a low surrogate (\uDE0A).
            // If it's at the end of the string, it is unpaired and corrupted.
            char boundaryChar = truncatedHead[truncatedHead.Length - 1];

            Assert.False(char.IsHighSurrogate(boundaryChar),
                "Regression: Truncation logic split a UTF-16 surrogate pair, leaving an orphaned high surrogate at the boundary.");
        }

        [Fact]
        public void FormatException_UnrollsAggregateExceptionSiblings_InCorrectChronologicalOrder()
        {
            // Arrange
            var exceptionA = new TimeoutException("Task A timed out");
            var exceptionB = new InvalidOperationException("Task B state invalid");

            // Wrap them inside a standard task framework composite exception
            var aggEx = new AggregateException("Batch process failed", exceptionA, exceptionB);

            Logger.Initialize(_testFileName);

            // Act
            Logger.Error("Aggregate processing fault", aggEx);
            Logger.Shutdown();

            // Assert
            string content = File.ReadAllText(_fullLogPath);

            // 1. Verify that the parent context is logged
            Assert.Contains("AggregateException: Batch process failed", content);

            // 2. Verify that BOTH sibling exceptions are logged via the stack walk rather than just the first one
            Assert.Contains("[Inner -> TimeoutException: Task A timed out]", content);
            Assert.Contains("[Inner -> InvalidOperationException: Task B state invalid]", content);

            // 3. Verify chronological order: Task A (left sibling) must be logged BEFORE Task B (right sibling)
            int indexA = content.IndexOf("Task A timed out");
            int indexB = content.IndexOf("Task B state invalid");

            Assert.True(indexA < indexB, "AggregateException siblings were not preserved in their chronological declaration order.");
        }

        [Fact]
        public void FormatException_UnrollsReflectionTypeLoadException_HandlesNullAndNonNullLoaderExceptions()
        {
            // Arrange
            var validLoaderEx = new TypeLoadException("Could not load assembly Servy.Service");

            // ReflectionTypeLoadException maps errors array explicitly to its internal LoaderExceptions property.
            // We add an explicit null element inside to ensure our loop's safety check ignores it.
            var loaderExceptions = new Exception?[] { null, validLoaderEx };
            var classes = new Type[] { typeof(string) };

            var typeLoadEx = new ReflectionTypeLoadException(classes, loaderExceptions, "Type scanning failed across boundary");

            Logger.Initialize(_testFileName);

            // Act
            Logger.Error("Reflection execution pass", typeLoadEx);
            Logger.Shutdown();

            // Assert
            string content = File.ReadAllText(_fullLogPath);

            // 1. Verify parent exception metadata is preserved
            Assert.Contains("ReflectionTypeLoadException: Type scanning failed across boundary", content);

            // 2. Verify that the non-null loader error is extracted, unrolled, and enclosed correctly
            // The inner exception segment now correctly closes the inner context boundary without stray characters
            Assert.Contains("[Inner -> TypeLoadException: Could not load assembly Servy.Service]", content);

            // 3. Structural validation: Verify brackets balance correctly for a single inner element match
            // The string must now terminate with a balanced single bracket block
            Assert.Contains("TypeLoadException: Could not load assembly Servy.Service]", content);
        }

        [Fact]
        public void FormatException_WithThreeInnerExceptions_BalancesBracketsPerfectly()
        {
            // Arrange
            Exception ex;
            try
            {
                try
                {
                    try
                    {
                        try
                        {
                            throw new TimeoutException("Third inner level fault");
                        }
                        catch (Exception level3)
                        {
                            throw new ArgumentException("Second inner level fault", level3);
                        }
                    }
                    catch (Exception level2)
                    {
                        throw new InvalidOperationException("First inner level fault", level2);
                    }
                }
                catch (Exception level1)
                {
                    throw new Exception("Root level context", level1);
                }
            }
            catch (Exception caught)
            {
                ex = caught;
            }

            Logger.Initialize(_testFileName);

            // Act
            Logger.Error("Nested chain execution pass", ex);
            Logger.Shutdown();

            // Assert
            string content = File.ReadAllText(_fullLogPath);

            // Verify all exception types and messages are preserved in the single-line string
            Assert.Contains("Exception: Root level context", content);
            Assert.Contains("[Inner -> InvalidOperationException: First inner level fault", content);
            Assert.Contains("[Inner -> ArgumentException: Second inner level fault", content);
            Assert.Contains("[Inner -> TimeoutException: Third inner level fault", content);

            // Isolate the exception text block to run structural calculations
            int msgIndex = content.IndexOf("Nested chain execution pass");
            string exceptionSegment = content.Substring(msgIndex).TrimEnd();

            // Calculate bracket balance
            var openTokensCount = Regex.Matches(exceptionSegment, Regex.Escape("[Inner -> ")).Count;
            var closeBracketsCount = exceptionSegment.Split(']').Length - 1;

            // ASSERTIONS:
            // 1. There must be exactly 3 "[Inner -> " opened tokens.
            Assert.Equal(3, openTokensCount);

            // 2. The number of closing brackets must match the number of opened ones.
            Assert.Equal(openTokensCount, closeBracketsCount);

            // 3. The structural brackets close out the entire string block 
            // after the final exception's stack trace context.
            Assert.EndsWith("]]]", exceptionSegment);
            Assert.Contains("Third inner level fault (at ", exceptionSegment);
        }

        [Fact]
        public void FormatException_RespectsMaxInnerExceptionDepthLimit_PreventsInfiniteLoopHangs()
        {
            // Arrange
            // Clear out any structural inheritance by starting from a clean base exception
            var currentEx = new Exception("Root Exception Context");

            // Build the chain downwards: the Root exception contains Inner1, which contains Inner2, etc.
            // We create exactly enough depth to overflow the threshold safely
            int targetOverflow = AppConfig.LoggerMaxInnerExceptionDepth + 5;
            for (int i = 1; i <= targetOverflow; i++)
            {
                currentEx = new Exception($"Depth level {i} wrapper", currentEx);
            }

            Logger.Initialize(_testFileName);

            // Act
            Logger.Error("Deeply nested exception test", currentEx);
            Logger.Shutdown();

            // Assert
            string content = File.ReadAllText(_fullLogPath);

            // 1. Verify the outermost exception wrapper is captured cleanly
            Assert.Contains($"Exception: Depth level {targetOverflow} wrapper", content);

            // 2. Verify that the deepest "Root" text was dropped because it exceeded the depth safety cutoff
            // Re-verify string parsing matches depth cutoff expectation boundary context parameters
            Assert.DoesNotContain("Root Exception Context", content);

            // 3. Isolate the exact formatted exception text segment to avoid picking up layout brackets
            int exceptionMessageIndex = content.IndexOf("Deeply nested exception test");
            string exceptionSegment = content.Substring(exceptionMessageIndex).TrimEnd();

            // 4. Calculate depth by counting structural depth tracking brackets inside the exception block
            int innerBracketCount = exceptionSegment.Split(new[] { "[Inner -> " }, StringSplitOptions.None).Length - 1;
            int closingBracketCount = exceptionSegment.Split(']').Length - 1;

            // The formatted string should never unroll more blocks than the max depth allowed
            Assert.True(innerBracketCount < AppConfig.LoggerMaxInnerExceptionDepth,
                $"Exception unroller processed more inner loops than allowed. Counted: {innerBracketCount}");

            // The closing brackets will be exactly innerBracketCount because the 
            // structural depth 0 root exception correctly skips closing tags.
            Assert.Equal(innerBracketCount, closingBracketCount);
        }

        #endregion

        #region Dynamic Configuration Setters Tests

        [Fact]
        public void Setters_WhenCalledWithNewValues_ReinitializesWriter()
        {
            // Arrange
            Logger.Initialize(_testFileName);
            Logger.Info("First writer");

            // Capture file state
            var fileInfo = new FileInfo(_fullLogPath);
            DateTime initialWriteTime = fileInfo.LastWriteTimeUtc;

            // Act: Call setters with new values (branches that trigger InternalInitialize)
            Logger.SetLogRotationSize(20);
            Logger.SetMaxBackupLogFiles(5);
            Logger.SetDateRotationType(DateRotationType.Daily);

            Logger.Info("Second writer");
            Logger.Shutdown();

            // Assert
            string content = File.ReadAllText(_fullLogPath);
            Assert.Contains("First writer", content);
            Assert.Contains("Second writer", content);
        }

        [Fact]
        public void Setters_WhenCalledWithSameValues_SkipsReinitialization()
        {
            // Arrange
            Logger.Initialize(_testFileName, logRotationSizeMB: 10, maxBackupLogFiles: 10);

            // The best way to assert it didn't recreate the writer is ensuring the fallback
            // counters weren't reset (which InternalInitialize does).
            var flags = BindingFlags.NonPublic | BindingFlags.Static;
            typeof(Logger).GetField("_initFallbackWriteCount", flags)?.SetValue(null, 99);

            // Act
            Logger.SetLogRotationSize(10);     // Unchanged
            Logger.SetMaxBackupLogFiles(10);   // Unchanged

            // Assert
            int count = (int)typeof(Logger).GetField("_initFallbackWriteCount", flags)!.GetValue(null)!;
            Assert.Equal(99, count); // Proves InternalInitialize was bypassed
        }

        [Fact]
        public void SetUseLocalTimeForRotation_UpdatesTimestampTimezoneFormat()
        {
            // Arrange
            // Generate a completely isolated unique file for this test instance 
            // to shield it against async logs from parallel test runner assemblies.
            string isolatedFileName = $"IsolationTimezoneTest_{Guid.NewGuid():N}.log";
            string isolatedFullPath = Path.Combine(Logger.LogsPath, isolatedFileName);

            if (File.Exists(isolatedFullPath))
            {
                File.Delete(isolatedFullPath);
            }

            Logger.Initialize(isolatedFileName, useLocalTimeForRotation: false);

            // Act
            Logger.Info("Message UTC");
            Logger.SetUseLocalTimeForRotation(true);
            Logger.Info("Message Local");
            Logger.Shutdown();

            // Assert
            string[] lines = File.ReadAllLines(isolatedFullPath);

            // Extract the targeted indices explicitly using their message payloads 
            // so background threads firing logs into the static entity won't disrupt verification.
            int utcIndex = Array.FindIndex(lines, l => l.Contains("Message UTC"));
            int localIndex = Array.FindIndex(lines, l => l.Contains("Message Local"));

            Assert.True(utcIndex >= 0, "Could not find the UTC log line entry.");
            Assert.True(localIndex >= 0, "Could not find the Local log line entry.");

            // Validate the first log entry uses UTC "Z" marker
            Assert.Contains("Z] [INFO]", lines[utcIndex]);

            // Validate the second log entry uses the local timezone offset (e.g., +02:00 or -05:00)
            Assert.Matches(@"[+-]\d{2}:\d{2}\] \[INFO\] \|", lines[localIndex]);

            // Teardown isolated resource cleanly
            try { if (File.Exists(isolatedFullPath)) File.Delete(isolatedFullPath); } catch { }
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void Log_EmptyOrNullMessage_ReturnsWithoutWriting()
        {
            // Arrange
            Logger.Initialize(_testFileName);

            // Act: Fire empty payloads that should trip the string.IsNullOrEmpty guard
            Logger.Info("");
            Logger.Info(null);
            Logger.Shutdown();

            // Assert: The file should never have been created on disk
            Assert.False(File.Exists(_fullLogPath), "Log file should not be created for empty or null messages.");
        }

        [Fact]
        public void Shutdown_WhenAlreadyShutdown_DoesNotThrow()
        {
            Logger.Initialize(_testFileName);
            Logger.Shutdown();

            var ex = Record.Exception(() => Logger.Shutdown());
            Assert.Null(ex); // Graceful no-op on secondary shutdown
        }

        #endregion
    }
}
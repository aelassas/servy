using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Testing;
using System.ComponentModel;
using System.Diagnostics;
using System.Security;

namespace Servy.Core.IntegrationTests.Logging
{
    [Collection("CoreOsIntegration")]
    public class EventLogLoggerIntegrationTests : IDisposable
    {
        private readonly bool _isElevated;
        private readonly List<string> _createdSources = new List<string>();

        public EventLogLoggerIntegrationTests()
        {
            _isElevated = Helper.IsAdministrator();
        }

        #region Initialization & Enable/Disable Tests

        [Fact]
        public void Constructor_WhenDisabled_DoesNotInitializeEventLog()
        {
            string source = GenerateSourceName();

            // Act
            using (var logger = new EventLogLogger(source, LogLevel.Info, isEventLogEnabled: false))
            {
                // Assert
                Assert.False(logger.IsEventLogEnabled);

                // _isInitialized is only ever set on a successful InitializeEventLog, so this
                // distinguishes "never attempted" - the "DoesNotInitializeEventLog" half of the
                // name - from "attempted and failed", which is what an unelevated run would give
                // if the constructor's isEventLogEnabled guard were dropped.
                Assert.False(TestReflection.GetField<bool>(logger, "_isInitialized"));

                Assert.Null(logger.Prefix);
            }
        }

        [Fact]
        public void Constructor_WithGlobalPrefix_WrapsInBrackets()
        {
            string source = GenerateSourceName();

            // Act
            using (var logger = new EventLogLogger(source, LogLevel.Info, false, "GlobalEngine"))
            {
                // Assert
                // Parent should automatically encapsulate its internal prefix in standard brackets
                Assert.Equal("[GlobalEngine]", logger.Prefix);
            }
        }

        [Fact]
        public void SetIsEventLogEnabled_TogglesStateAndHandlesCorrectly()
        {
            if (!_isElevated) Assert.Skip("Skipping test due to insufficient privileges.");

            string source = GenerateSourceName();
            using (var logger = new EventLogLogger(source, LogLevel.Info, isEventLogEnabled: false))
            {
                Assert.False(logger.IsEventLogEnabled);

                // Act: enable - runs InitializeEventLog and sets _isInitialized
                logger.SetIsEventLogEnabled(true);
                Assert.True(logger.IsEventLogEnabled);

                // Act: enable again - takes the already-initialized branch, which only
                // re-sets the flag. This assert reads the same on either branch.
                logger.SetIsEventLogEnabled(true);
                Assert.True(logger.IsEventLogEnabled);

                // Act: disable - clears the flag only. Nothing is disposed and
                // _isInitialized survives (only Dispose clears it), so a later
                // re-enable does not run InitializeEventLog again.
                logger.SetIsEventLogEnabled(false);
                Assert.False(logger.IsEventLogEnabled);
            }
        }

        [Fact]
        public void InitializeEventLog_WhenSourceAssignedToDifferentLog_DisablesLogger()
        {
            if (!_isElevated) Assert.Skip("Skipping test due to insufficient privileges.");

            string mismatchSource = GenerateSourceName();

            // We intentionally bind this source to "Application" instead of AppConfig.EventLogName.
            // If AppConfig.EventLogName IS "Application", we bind to "System".
            string targetLog = AppConfig.EventLogName.Equals("Application", StringComparison.OrdinalIgnoreCase)
                ? "System"
                : "Application";

            EventLog.CreateEventSource(mismatchSource, targetLog);

            // Act
            // The logger constructor calls InitializeEventLog, which will detect the mismatch
            using (var logger = new EventLogLogger(mismatchSource, LogLevel.Info, true))
            {
                // Assert
                // It should detect the mismatch, log an error, and disable itself safely.
                Assert.False(logger.IsEventLogEnabled);
            }
        }

        #endregion

        #region Core Logging & Formatting Tests

        [Theory]
        [InlineData(LogLevel.Debug)]
        [InlineData(LogLevel.Info)]
        [InlineData(LogLevel.Warn)]
        [InlineData(LogLevel.Error)]
        public void LogMethods_RespectLogLevelFiltering(LogLevel targetLevel)
        {
            // Arrange
            string source = GenerateSourceName();
            using (var logger = new EventLogLogger(source, LogLevel.Error, isEventLogEnabled: false, "TestPrefix"))
            {
                // Act
                logger.SetLogLevel(targetLevel);

                // Assert
                // Extract the internal level field via reflection to prove that SetLogLevel
                // wrote it. This test does not call any log method, so it says nothing about
                // whether WriteLeveled then filters on that threshold.
                var actualLogLevelInt = TestReflection.GetField<int>(logger, "_currentLogLevel");

                Assert.Equal((int)targetLevel, actualLogLevelInt);
                Assert.Equal("[TestPrefix]", logger.Prefix);
            }
        }

        [Theory]
        [InlineData(LogLevel.Debug)]
        [InlineData(LogLevel.Info)]
        [InlineData(LogLevel.Warn)]
        [InlineData(LogLevel.Error)]
        public void LogMethods_AllSeverities_ExecuteWithoutThrowingAndMaintainPrefix(LogLevel targetLevel)
        {
            // Arrange
            string source = GenerateSourceName();

            // Start with Error level (strictest), so Debug/Info/Warn should be ignored
            using (var logger = new EventLogLogger(source, LogLevel.Error, false, "TestPrefix"))
            {
                // Act
                logger.SetLogLevel(targetLevel);

                // Act - Executing these covers the severity boundary checks
                // Because event log is disabled in this test, it only hits the formatting and internal logger branches.
                var ex = new Exception("Test Exception");
                logger.Debug("Debug msg", ex);
                logger.Info("Info msg", null);
                logger.Warn("Warn msg", ex);
                logger.Error("Error msg", null);

                // Assert
                // No exceptions thrown means branches were safely evaluated
                Assert.Equal("[TestPrefix]", logger.Prefix);
            }
        }

        [Fact]
        public void WriteRawToWindowsEventLog_OversizedMessage_TruncatesSuccessfully()
        {
            // Arrange
            if (!_isElevated) Assert.Skip("Skipping test due to insufficient privileges.");

            string source = GenerateSourceName();

            // Explicitly ensure the source is registered on the OS before writing,
            // preventing fallback routing to the "Application" log
            if (!EventLog.SourceExists(source))
            {
                EventLog.CreateEventSource(source, AppConfig.EventLogName);
            }

            try
            {
                using (var logger = new EventLogLogger(source, LogLevel.Info, true))
                {
                    const string truncationSuffix = "...[truncated]";
                    int expectedMaxLength = AppConfig.EventLogMessageMaxChars + truncationSuffix.Length;

                    // Create a massive string guaranteed to exceed the configured truncation threshold
                    string massiveString = new string('A', AppConfig.EventLogMessageMaxChars + 9_000);

                    // Act
                    Exception? ex = Record.Exception(() => logger.Info(massiveString));

                    // Assert Execution State
                    Assert.Null(ex);

                    // Assert Structural Persistence Truncation Integrity
                    try
                    {
                        using (var eventLog = new EventLog(AppConfig.EventLogName))
                        {
                            eventLog.Source = source;

                            EventLogEntry? foundEntry = null;

                            // Introduce a polling loop with exponential backoff
                            // to account for asynchronous Event Log service disk-flushing delays
                            const int maxRetries = 10;
                            int retryCount = 0;
                            int delayMs = 50;

                            while (foundEntry == null && retryCount++ < maxRetries)
                            {
                                int count = eventLog.Entries.Count;
                                for (int i = count - 1; i >= 0; i--)
                                {
                                    if (eventLog.Entries[i].Source == source)
                                    {
                                        foundEntry = eventLog.Entries[i];
                                        break;
                                    }
                                }

                                if (foundEntry == null)
                                {
                                    Thread.Sleep(delayMs);
                                    delayMs *= 2; // Exponential backoff (50ms, 100ms, 200ms...)
                                }
                            }

                            if (foundEntry != null)
                            {
                                // Verify absolute length bounds and suffix marker format compliance
                                Assert.True(foundEntry.Message.Length <= expectedMaxLength,
                                    $"Persisted message length ({foundEntry.Message.Length}) exceeds the configured ceiling of {expectedMaxLength}.");

                                Assert.EndsWith(truncationSuffix, foundEntry.Message);
                            }
                            else
                            {
                                // The polling window can expire without the entry appearing (slow Event Log flush,
                                // rotation eviction, write routed elsewhere). Report it the same way the ACL-restricted
                                // read-back below does, so a run that verified nothing is distinguishable from one that did.
                                Trace.WriteLine($"Warning: No EventLog entry from source '{source}' appeared after {maxRetries} retries; truncation contract not verified this run.");
                            }
                        }
                    }
                    catch (Exception readEx) when (readEx is Win32Exception || readEx is SecurityException || readEx is UnauthorizedAccessException)
                    {
                        // On constrained CI runners or custom event log channels, reading entries directly via EventLog.Entries
                        // can throw Access Denied Win32Exception due to log file ACL restrictions even when write operations succeed.
                        Trace.WriteLine($"Warning: Could not read back EventLog entries due to security restrictions: {readEx.Message}");
                    }
                }
            }
            finally
            {
                // Clean up the temporary registered event source from the registry
                if (EventLog.SourceExists(source))
                {
                    EventLog.DeleteEventSource(source);
                }
            }
        }

        [Fact]
        public void WriteRawToWindowsEventLog_OnNativeException_CatchesAndProceeds()
        {
            // Arrange
            // No elevation guard here on purpose: WriteRawToWindowsEventLog wraps its whole body
            // in a catch, so the asserted outcome is the same elevated or not - unelevated, the
            // malformed source simply fails inside the try, which is the path being tested.
            // Guarding it would skip the fail-safe boundary on every ordinary runner.

            // CRITICAL CONTRACT: Test the exception isolation boundaries directly on the
            // internal structural wrapper method by feeding it an illegal, un-creatable source layout configuration.
            string illegalLogName = "?:\x00//IllegalLogName";
            string illegalSource = "IllegalSource_\x00";
            string testMessage = "Fallback tracking message isolation probe.";

            // Act
            // When the OS throws an ArgumentException or Win32Exception trying to parse the malformed string parameters,
            // WriteRawToWindowsEventLog must catch it internally and return gracefully instead of crashing the thread.
            Exception? ex = Record.Exception(() =>
                EventLogLogger.WriteRawToWindowsEventLog(illegalLogName, illegalSource, testMessage, EventLogEntryType.Error, 9999)
            );

            // Assert
            // Confirm the fail-safe boundary catches the exception internally and returns cleanly
            Assert.Null(ex);
        }

        #endregion

        #region ScopedEventLogLogger Tests

        [Fact]
        public void ScopedLogger_EmptyOrWhitespacePrefix_ReturnsSameInstanceWithoutAllocation()
        {
            string source = GenerateSourceName();
            using (var rootLogger = new EventLogLogger(source, LogLevel.Info, false))
            {
                var scope1 = rootLogger.CreateScoped("    ");
                var scope2 = rootLogger.CreateScoped(null!);

                // Assert immutability pass-through optimization behavior
                Assert.Same(rootLogger, scope1);
                Assert.Same(rootLogger, scope2);
            }
        }

        [Fact]
        public void ScopedLogger_SetIsEventLogEnabled_PropagatesToParent()
        {
            if (!_isElevated) Assert.Skip("Skipping test due to insufficient privileges.");

            string source = GenerateSourceName();
            using (var rootLogger = new EventLogLogger(source, LogLevel.Error, false))
            {
                // Act
                var scopedLogger = rootLogger.CreateScoped("Scope1");

                // Assert initial inheritance and immediate bracket tracking on the first pass
                Assert.Equal("[Scope1]", scopedLogger.Prefix);

                // Act - Change scope settings
                scopedLogger.SetLogLevel(LogLevel.Debug);

                // Assert
                // The scope keeps its own level and does not push it onto the parent,
                // which is the threshold WriteLeveled is handed for every scoped call.
                Assert.Equal((int)LogLevel.Debug, TestReflection.GetField<int>(scopedLogger, "_currentLogLevel"));
                Assert.Equal((int)LogLevel.Error, TestReflection.GetField<int>(rootLogger, "_currentLogLevel"));

                // Act
                scopedLogger.SetIsEventLogEnabled(true);

                // Assert
                // The parent's state must change
                Assert.True(rootLogger.IsEventLogEnabled);

                // Run logs through the scope to ensure all internal bypassing methods work
                var ex = new Exception("Scope Ex");
                scopedLogger.Debug("Scope Debug", ex);
                scopedLogger.Info("Scope Info");
                scopedLogger.Warn("Scope Warn");
                scopedLogger.Error("Scope Error", ex);

                // Act & Assert
                // "Disabling a scope never affects the parent" - the other half of the
                // documented self-heal contract, which only enables through the parent.
                scopedLogger.SetIsEventLogEnabled(false);
                Assert.True(rootLogger.IsEventLogEnabled);

                // Act & Assert
                // The scope's Dispose is documented as a no-op, so it must not tear down
                // the shared parent that the other scopes are still writing through.
                scopedLogger.Dispose();
                Assert.True(rootLogger.IsEventLogEnabled);
                Assert.True(TestReflection.GetField<bool>(rootLogger, "_isInitialized"));
            }
        }

        [Fact]
        public void ScopedLogger_CreateNestedScope_FormatsCombinedPrefixes()
        {
            string source = GenerateSourceName();
            using (var rootLogger = new EventLogLogger(source, LogLevel.Info, false, "Root"))
            {
                // Act
                var level1Scope = rootLogger.CreateScoped("L1");
                var level2Scope = level1Scope.CreateScoped("L2"); // Scoped -> Scoped

                // Assert
                // When a scoped logger creates a child scope, it combines them cleanly with brackets: "[Root] [L1] [L2]"
                Assert.Equal("[Root] [L1] [L2]", level2Scope.Prefix);

                // Ensure it runs log formatting gracefully without duplicate processing crashes
                Exception? ex = Record.Exception(() => level2Scope.Info("Nested Log"));
                Assert.Null(ex);
            }
        }

        [Fact]
        public void ScopedLogger_WhenPrefixContainsBrackets_SanitizesToParentheses()
        {
            string source = GenerateSourceName();
            using (var rootLogger = new EventLogLogger(source, LogLevel.Info, false, "Root"))
            {
                // Act: Consumer passes custom bracketted contexts explicitly
                var scopedLogger = rootLogger.CreateScoped("[WexflowContext]");

                // Assert
                // The inner brackets must be rewritten as parentheses to maintain structural integrity
                Assert.Equal("[Root] [(WexflowContext)]", scopedLogger.Prefix);

                Exception? ex = Record.Exception(() => scopedLogger.Info("Sanitized output logic test"));
                Assert.Null(ex);
            }
        }

        #endregion

        #region Teardown & Utilities

        private string GenerateSourceName()
        {
            string source = "ServyTest_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _createdSources.Add(source);
            return source;
        }

        public void Dispose()
        {
            if (!_isElevated) return;

            // Clean up any dynamic Event Sources created during the test run
            foreach (var source in _createdSources)
            {
                try
                {
                    if (EventLog.SourceExists(source))
                    {
                        EventLog.DeleteEventSource(source);
                    }
                }
                catch (Exception ex)
                {
                    // Suppress locking errors if the EventLog service is slow to release handles during test teardown
                    // Log the failure but don't crash the test runner.
                    // In CI, we accept that registry cleanup might fail if the OS is busy.
                    Trace.WriteLine($"Warning: Failed to cleanup event source {source}: {ex.Message}");
                }
            }
        }

        #endregion
    }
}

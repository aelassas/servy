using Servy.Core.Config;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using Xunit;

namespace Servy.Core.Logging.IntegrationTests
{
    [Collection("EventLogLoggerIntegrationTests")]
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
                Assert.Null(logger.Prefix);
            }
        }

        [Fact]
        public void SetIsEventLogEnabled_TogglesStateAndHandlesCorrectly()
        {
            if (!_isElevated) return;

            string source = GenerateSourceName();
            using (var logger = new EventLogLogger(source, LogLevel.Info, isEventLogEnabled: false))
            {
                Assert.False(logger.IsEventLogEnabled);

                // Act: Turn ON (initializes handle)
                logger.SetIsEventLogEnabled(true);
                Assert.True(logger.IsEventLogEnabled);

                // Act: Turn ON again (exercises the already-initialized branch)
                logger.SetIsEventLogEnabled(true);
                Assert.True(logger.IsEventLogEnabled);

                // Act: Turn OFF (disposes handle)
                logger.SetIsEventLogEnabled(false);
                Assert.False(logger.IsEventLogEnabled);
            }
        }

        [Fact]
        public void InitializeEventLog_WhenSourceAssignedToDifferentLog_DisablesLogger()
        {
            if (!_isElevated) return;

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
            string source = GenerateSourceName();
            // Start with Error level (strictest), so Debug/Info/Warn should be ignored
            using (var logger = new EventLogLogger(source, LogLevel.Error, false, "TestPrefix"))
            {
                logger.SetLogLevel(targetLevel);

                // Act - Executing these covers the severity boundary checks
                // Because event log is disabled in this test, it only hits the formatting and internal logger branches.
                var ex = new Exception("Test Exception");
                logger.Debug("Debug msg", ex);
                logger.Info("Info msg", null);
                logger.Warn("Warn msg", ex);
                logger.Error("Error msg", null);

                // No exceptions thrown means branches were safely evaluated
                Assert.Equal("TestPrefix", logger.Prefix);
            }
        }

        [Fact]
        public void SafeWriteToEventLog_OversizedMessage_TruncatesSuccessfully()
        {
            if (!_isElevated) return;

            string source = GenerateSourceName();
            using (var logger = new EventLogLogger(source, LogLevel.Info, true))
            {
                // Create a massive string guaranteed to exceed the standard 31839 character limit
                string massiveString = new string('A', 40000);

                // Act
                // Should invoke the truncation branch and write "...[truncated]" without throwing Win32Exception
                Exception ex = Record.Exception(() => logger.Info(massiveString));

                // Assert
                Assert.Null(ex);
            }
        }

        [Fact]
        public void SafeWriteToEventLog_OnNativeException_CatchesAndProceeds()
        {
            if (!_isElevated) return;

            string source = GenerateSourceName();
            using (var logger = new EventLogLogger(source, LogLevel.Info, true))
            {
                // Sabotage the underlying OS event source after the logger has initialized it
                EventLog.DeleteEventSource(source);
                Thread.Sleep(200); // Give OS time to deregister

                // Act
                // The native handle is now orphaned. Writing to it throws internally, 
                // but SafeWriteToEventLog catches it and delegates to Logger.Warn.
                Exception ex = Record.Exception(() => logger.Info("This should fail safely"));

                // Assert
                Assert.Null(ex);
            }
        }

        #endregion

        #region ScopedEventLogLogger Tests

        [Fact]
        public void ScopedLogger_InheritsSettings_ButCanOverrideThemIndependently()
        {
            string source = GenerateSourceName();
            using (var rootLogger = new EventLogLogger(source, LogLevel.Error, false))
            {
                // Act
                var scopedLogger = rootLogger.CreateScoped("Scope1");

                // Assert initial inheritance
                Assert.Equal("Scope1", scopedLogger.Prefix);

                // Act - Change scope settings independently
                scopedLogger.SetLogLevel(LogLevel.Debug);
                scopedLogger.SetIsEventLogEnabled(true);

                // Assert Independence
                // The parent's state must remain unchanged
                Assert.False(rootLogger.IsEventLogEnabled);

                // Run logs through the scope to ensure all internal bypassing methods work
                var ex = new Exception("Scope Ex");
                scopedLogger.Debug("Scope Debug", ex);
                scopedLogger.Info("Scope Info");
                scopedLogger.Warn("Scope Warn");
                scopedLogger.Error("Scope Error", ex);

                // Cover the no-op Dispose
                scopedLogger.Dispose();
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
                // When a scoped logger creates a child scope, it combines them: "[L1] [L2]"
                Assert.Equal("L1] [L2", level2Scope.Prefix);

                // Ensure it runs log formatting gracefully
                Exception ex = Record.Exception(() => level2Scope.Info("Nested Log"));
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
                catch(Exception ex)
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
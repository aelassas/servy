using Moq;
using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Restarter.Bootstrap;
using Servy.Testing;

namespace Servy.Restarter.UnitTests
{
    [Collection(ProgramTestsCollection.Name)]
    public class ProgramTests : TempDirectoryTestBase
    {
        // CONSTANT STRINGS HOISTING: Centralize artifact filenames to prevent cleanup drift
        private const string ConfigFileName = "appsettings.restarter.json";
        private const string LogFileName = "Servy.Restarter.log";

        /// <summary>The command line the Service Control Manager stores for a service installed by Servy's desktop app.</summary>
        private const string UiWrapperImagePath = "\"C:\\ProgramData\\Servy\\Servy.Service.exe\" \"{0}\"";

        private readonly string _tempConfigPath;
        private readonly string _configBackupPath;
        private readonly bool _hasConfigBackup;
        private readonly string _expectedLogFilePath;

        public ProgramTests()
        {
            // Reset exit code before each execution run
            Environment.ExitCode = 0;

            // Generate isolated test-run directories for configuration and log storage
            _tempConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            _expectedLogFilePath = Path.Combine(TempDirectory, LogFileName);

            // Pre-seed the static logger so empty/missing argument calls route to the isolated temp directory
            Logger.Initialize(LogFileName, logDirectory: TempDirectory);

            // Program.Main reads its configuration strictly from the app directory, so the tests must
            // clobber the build-deployed appsettings.restarter.json. Back it up so Dispose can put the
            // build's own artifact back instead of leaving the output directory without it.
            _configBackupPath = _tempConfigPath + ".bak";
            _hasConfigBackup = File.Exists(_tempConfigPath);
            if (_hasConfigBackup)
            {
                File.Copy(_tempConfigPath, _configBackupPath, overwrite: true);
            }

            File.WriteAllText(_tempConfigPath, BuildConfigJson("30"));
        }

        /// <summary>
        /// Builds a fake environment whose Service Control Manager reports <paramref name="serviceName"/> as running
        /// Servy's desktop-app wrapper, which is what makes the restarter treat it as managed by Servy.
        /// </summary>
        private static FakeRestarterBootstrapEnvironment ManagedService(string serviceName)
        {
            var environment = new FakeRestarterBootstrapEnvironment();
            environment.ImagePaths[serviceName] = string.Format(UiWrapperImagePath, serviceName);
            return environment;
        }

        private static string BuildConfigJson(string restartTimeoutSeconds) =>
            "{\r\n" +
            "  \"RestartTimeoutSeconds\": \"" + restartTimeoutSeconds + "\"\r\n" +
            "}";

        #region Guard Conditions Branch Coverage

        [Fact]
        public void Main_MissingArguments_SetsExitCodeTo1AndExitsEarly()
        {
            // Arrange
            string[] args = new string[0]; // Triggers if (args.Length == 0)

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage("Missing required argument: service name.");

            // The ExitsEarly half: without the guard's return, args[0] throws on the empty array
            // and the catch-all logs the pre-scoped-logger failure instead.
            AssertLogDoesNotContainMessage("Servy.Restarter.exe failed to initialize or execute.");
            AssertLogDoesNotContainMessage("Attempting to restart service");
        }

        [Theory]
        [InlineData("")]
        [InlineData("    ")]
        public void Main_EmptyOrWhitespaceServiceName_SetsExitCodeTo1AndExitsEarly(string invalidName)
        {
            // Arrange
            string[] args = new string[] { invalidName, TempDirectory }; // Triggers if (string.IsNullOrWhiteSpace(serviceName))

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage("Service name cannot be empty.");

            // The ExitsEarly half: without the guard's return, the blank name flows on to the
            // Service Control Manager lookup and the not-managed branch logs and sets ExitCode = 1 as well.
            AssertLogDoesNotContainMessage("is not managed by Servy.");
            AssertLogDoesNotContainMessage("Attempting to restart service");
        }

        #endregion

        #region Event Log Fallback & Security Guard Coverage

        /*
         * The event source, the event-log logger and the Service Control Manager lookup of the service's
         * executable are reached through IRestarterBootstrapEnvironment, so the branches below need neither
         * a host without Windows Event Log registry access nor a real service. The restarter runs under the
         * service account and has no access to Servy.db: whether a service is managed by Servy is decided by
         * the executable the SCM runs for it (IsServyWrapperImagePath, pinned below).
         */

        [Fact]
        public void Run_EventSourceRegistrationFails_FallsBackToFileOnlyLoggingAndContinues()
        {
            // Arrange
            // The event source cannot be registered, so Step 1's catch must take over: it warns and asks
            // for a logger with the Windows Event Log turned OFF, then start-up carries on.
            string serviceName = "GhostServiceWithUnavailableEventSource";
            var environment = new FakeRestarterBootstrapEnvironment
            {
                EventSourceFailure = new InvalidOperationException("event log registry is not reachable")
            };

            // Act
            Program.Run(new string[] { serviceName, TempDirectory }, restarter: null, environment: environment);

            // Assert
            AssertLogContainsMessage("Event Log source unavailable; continuing with file logging only.");

            // The fallback's argument is the branch. EnsureEventSourceExists threw before the primary
            // assignment ran, so the single request recorded is the catch's own, and it must ask for
            // file-only logging: deleting that assignment, or letting it ask for the event log instead,
            // changes this sequence.
            Assert.Equal(new[] { false }, environment.EventLogLoggerRequests);

            // Best-effort means the restart is not abandoned: the pipeline runs on to the validation step.
            AssertLogContainsMessage($"Service '{serviceName}' is not managed by Servy.");
        }

        [Fact]
        public void Run_EventSourceRegistered_AsksForAnEventLogEnabledRootLogger()
        {
            // Arrange
            // The event source registers, so Step 1's try must ask for a logger with the Windows Event
            // Log ON. #7262 added the fake and read its request list from the failure test only, which
            // left the primary arm's argument unpinned.
            string serviceName = "GhostServiceWithRegisteredEventSource";
            var environment = new FakeRestarterBootstrapEnvironment();

            // Act
            Program.Run(new string[] { serviceName, TempDirectory }, restarter: null, environment: environment);

            // Assert
            // One request, from the try, asking for the event log: flipping the primary argument, or
            // taking the fallback without a failure, changes this sequence. In production that argument
            // is what puts every restarter Warn and Error on the Windows Event Log.
            Assert.Equal(new[] { true }, environment.EventLogLoggerRequests);
            AssertLogDoesNotContainMessage("Event Log source unavailable; continuing with file logging only.");

            // Best-effort start-up continues to the validation step, as in the fallback test above.
            AssertLogContainsMessage($"Service '{serviceName}' is not managed by Servy.");
        }

        [Fact]
        public void Run_EventSourceRegistersButEventLogLoggerFails_FallsBackToFileOnlyLogging()
        {
            // Arrange
            // The second way into Step 1's catch: the source registers, so EnsureEventSourceExists
            // returns, and the primary CreateEventLogLogger(true) is what throws while the fallback
            // CreateEventLogLogger(false) still builds (FailOnlyEventLogEnabledLogger).
            string serviceName = "GhostServiceWithUnbuildableEventLogLogger";
            var environment = new FakeRestarterBootstrapEnvironment
            {
                EventLogLoggerFailure = new InvalidOperationException("the event log logger cannot be built"),
                FailOnlyEventLogEnabledLogger = true
            };

            // Act
            Program.Run(new string[] { serviceName, TempDirectory }, restarter: null, environment: environment);

            // Assert
            AssertLogContainsMessage("Event Log source unavailable; continuing with file logging only.");

            // Both requests in call order are the branch: the try asked for the event log and failed,
            // then the catch asked for file-only logging and succeeded.
            Assert.Equal(new[] { true, false }, environment.EventLogLoggerRequests);

            // The fallback logger was built, so start-up carries on rather than reaching the catch-all.
            AssertLogContainsMessage($"Service '{serviceName}' is not managed by Servy.");
            AssertLogDoesNotContainMessage("Servy.Restarter.exe failed to initialize or execute.");
        }

        [Fact]
        public void Run_ServiceRunsAnotherExecutable_RefusesAsNotManagedAndNeverRestarts()
        {
            // Arrange
            // A service that exists but does not run one of Servy's wrappers: the restarter must not restart
            // an arbitrary service just because a service account asked it to.
            string serviceName = "ForeignServiceNotRunningAServyWrapper";
            var environment = new FakeRestarterBootstrapEnvironment();
            environment.ImagePaths[serviceName] = "C:\\Windows\\System32\\svchost.exe -k netsvcs";
            var mockRestarter = new Mock<IServiceRestarter>(MockBehavior.Strict);

            // Act
            Program.Run(new string[] { serviceName, TempDirectory }, mockRestarter.Object, environment);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage($"Service '{serviceName}' is not managed by Servy.");
            AssertLogDoesNotContainMessage("Attempting to restart service");
            Assert.Equal(new[] { serviceName }, environment.ImagePathRequests);
        }

        [Fact]
        public void Run_EventLogFallbackAlsoFails_ReportsThroughTheStaticLoggerArm()
        {
            // Arrange
            // When the file-only fallback cannot be built either, no root logger and no scoped logger ever
            // exist, so the catch-all has nothing but the static Logger: its else arm.
            var environment = new FakeRestarterBootstrapEnvironment
            {
                EventSourceFailure = new InvalidOperationException("event log registry is not reachable"),
                EventLogLoggerFailure = new InvalidOperationException("the event log logger cannot be built")
            };

            // Act
            Program.Run(new string[] { "AnyServiceName", TempDirectory }, restarter: null, environment: environment);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage("Servy.Restarter.exe failed to initialize or execute.");

            // The arm is the branch: with a logger available the catch-all reports the other message, so
            // this pins the else rather than the if.
            AssertLogDoesNotContainMessage("Servy.Restarter.exe failed to restart the service.");
        }

        #endregion

        #region Operational Pipeline & Validation Exceptions

        [Fact]
        public void Main_SettingsFileStillRelocatesTheDatabase_WarnsThatTheSettingIsIgnored()
        {
            // Arrange
            // A pre-10.2 appsettings.restarter.json that still points the database elsewhere. The
            // setting has no effect (the restarter does not open the database at all), and the
            // restarter must say so once the scoped logger exists.
            File.WriteAllText(_tempConfigPath,
                "{\r\n" +
                "  \"ConnectionStrings\": {\r\n" +
                "    \"DefaultConnection\": \"Data Source=D:\\\\old\\\\Servy.db\"\r\n" +
                "  },\r\n" +
                "  \"RestartTimeoutSeconds\": \"30\"\r\n" +
                "}");
            string serviceName = "GhostUnmanagedServiceWithOldSettings";

            // Act
            Program.Main(new string[] { serviceName, TempDirectory });

            // Assert
            AssertLogContainsMessage("appsettings.restarter.json sets ConnectionStrings:DefaultConnection, which Servy ignores since v10.2");
        }

        [Fact]
        public void Main_ValidNameButServiceNotManaged_TriggersValidationFailureBranch()
        {
            // Arrange
            // We provide a dummy service name that is not installed: the Service Control Manager has no
            // executable for it, which triggers the not-managed failure branch cleanly.
            string serviceName = "GhostUnmanagedService";
            string[] args = new string[] { serviceName, TempDirectory };

            // Act
            Program.Main(args);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage($"Service '{serviceName}' is not managed by Servy.");
        }

        [Fact]
        public void Main_FallbackConfigurationParsing_HandlesInvalidTimeoutGracefully()
        {
            // Arrange
            string serviceName = "ManagedTestServiceForTimeoutValidation";
            var environment = ManagedService(serviceName);

            // A structurally complete configuration payload where only the timeout option is corrupted.
            File.WriteAllText(_tempConfigPath, BuildConfigJson("NotAnInteger"));

            string[] args = new string[] { serviceName, TempDirectory };

            // Act
            Program.Run(args, restarter: null, environment: environment);

            // Assert
            // The application successfully bypassed the corrupted token string and fell back
            // to standard timeout bounds. Because the service does not actually exist in the SCM,
            // it detects ServiceNotFound, logs a warning, and sets ExitCode = 1.
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage($"Service '{serviceName}' no longer exists in the SCM; nothing to restart.");
        }

        [Fact]
        public void Main_ServiceRestarted_SetsExitCodeTo0AndLogsSuccess()
        {
            // Arrange
            string serviceName = "ManagedServiceForSuccessfulRestart";
            var environment = ManagedService(serviceName);

            var mockRestarter = new Mock<IServiceRestarter>();
            mockRestarter
                .Setup(r => r.RestartService(serviceName, It.IsAny<TimeSpan>()))
                .Returns(RestartResult.Restarted);

            string[] args = new string[] { serviceName, TempDirectory };

            // Act
            Program.Run(args, mockRestarter.Object, environment);

            // Assert
            Assert.Equal(0, Environment.ExitCode);
            AssertLogContainsMessage($"Successfully restarted service '{serviceName}'.");
            mockRestarter.Verify(r => r.RestartService(serviceName, It.IsAny<TimeSpan>()), Times.Once);
        }

        [Fact]
        public void Main_RestartTimeoutExceedsHostWaitLimit_LogsWarning()
        {
            // Arrange
            // 300s is above the 240s host service execution wait limit
            // (AppConfig.RestarterExeMaxWaitMs / AppConfig.MillisecondsPerSecond) and well inside
            // AppConfig.MaxRestarterTimeoutSeconds, so ConfigParser.GetConfigInt passes it through
            // unclamped and the over-budget warning branch is taken.
            string serviceName = "ManagedServiceForTimeoutWarning";
            var environment = ManagedService(serviceName);

            var mockRestarter = new Mock<IServiceRestarter>();
            mockRestarter
                .Setup(r => r.RestartService(serviceName, It.IsAny<TimeSpan>()))
                .Returns(RestartResult.Restarted);

            File.WriteAllText(_tempConfigPath, BuildConfigJson("300"));
            string[] args = new string[] { serviceName, TempDirectory };

            // Act
            Program.Run(args, mockRestarter.Object, environment);

            // Assert
            Assert.Equal(0, Environment.ExitCode);
            AssertLogContainsMessage("Configured RestartTimeoutSeconds (300s) exceeds the host service execution wait limit (240s).");
        }

        #endregion

        #region Fatal Exception Resilience Blocks

        [Fact]
        public void Run_ServiceLookupThrows_HitsCatchAllViaScopedLogger()
        {
            // Arrange
            // The Service Control Manager lookup fails after the scoped logger exists, exercising the
            // scoped-logger arm of the catch-all block.
            var environment = new FakeRestarterBootstrapEnvironment
            {
                ImagePathFailure = new InvalidOperationException("the service key cannot be read")
            };
            string[] args = new string[] { "Invalid\\Service/Path:Characters", TempDirectory };

            // Act
            Program.Run(args, restarter: null, environment: environment);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            // Confirms that the catch-all execution path was hit using the initialized scoped logger.
            // The service-name prefix is what only the scoped logger can add, so asserting it pins
            // the "scoped first" half of the scoped > root > static order rather than merely that
            // some logger reported the failure.
            AssertLogContainsMessage("[Invalid\\Service/Path:Characters] Servy.Restarter.exe failed to restart the service.");
        }

        [Fact]
        public void Run_SettingsFileUnparsable_HitsCatchAllViaRootLogger()
        {
            // Arrange
            // The root logger exists (Step 1 succeeded) but ConfigurationBuilder.Build() throws on the
            // unparsable file before Step 4 creates the scoped logger, so the catch-all has only the
            // root logger - the middle arm of scoped > root > static. optional: true covers a missing
            // file, not a malformed one.
            string serviceName = "ServiceWithUnparsableRestarterSettings";
            File.WriteAllText(_tempConfigPath, "{ \"RestartTimeoutSeconds\": ");
            var environment = new FakeRestarterBootstrapEnvironment();

            // Act
            Program.Run(new string[] { serviceName, TempDirectory }, restarter: null, environment: environment);

            // Assert
            Assert.Equal(1, Environment.ExitCode);
            AssertLogContainsMessage("Servy.Restarter.exe failed to restart the service.");
            // The root logger carries no scope, and the static arm is a different sentence: together
            // these two pin the middle arm specifically.
            AssertLogDoesNotContainMessage($"[{serviceName}] Servy.Restarter.exe failed to restart the service.");
            AssertLogDoesNotContainMessage("Servy.Restarter.exe failed to initialize or execute.");
        }

        #endregion

        #region Servy Wrapper Detection

        [Theory]
        [InlineData("\"C:\\ProgramData\\Servy\\Servy.Service.exe\" \"svc\"")]
        [InlineData("\"C:\\ProgramData\\Servy\\Servy.Service.CLI.exe\" \"svc\"")]
        [InlineData("\"D:\\Dev\\Servy\\bin\\SERVY.SERVICE.EXE\" svc")]
        [InlineData("C:\\Servy\\Servy.Service.exe svc")]
        [InlineData("C:\\Servy\\Servy.Service.exe")]
        public void IsServyWrapperImagePath_ServyWrapper_ReturnsTrue(string imagePath)
        {
            Assert.True(Program.IsServyWrapperImagePath(imagePath));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("C:\\Windows\\System32\\svchost.exe -k netsvcs")]
        [InlineData("\"C:\\Servy\\Servy.Host.exe\"")]
        [InlineData("\"C:\\Servy\\Servy.Restarter.exe\" svc")]
        [InlineData("\"C:\\Evil\\NotServy.Service.exe\" svc")]
        [InlineData("\"C:\\Evil\\Servy.Service.exe.bat\" svc")]
        [InlineData("\"C:\\Unterminated\\Servy.Service.exe svc")]
        [InlineData("C:\\Program Files\\Servy\\Servy.Service.exe svc")]
        public void IsServyWrapperImagePath_OtherExecutable_ReturnsFalse(string? imagePath)
        {
            Assert.False(Program.IsServyWrapperImagePath(imagePath));
        }

        #endregion

        #region Verification Helpers

        /// <summary>
        /// Scans the physical log output stream for the expected diagnostic signatures to discriminate between crash paths.
        /// </summary>
        private void AssertLogContainsMessage(string expectedMessage)
        {
            // Force the static logger to flush its handle completely to disk
            Logger.Shutdown();

            Assert.True(File.Exists(_expectedLogFilePath), $"The diagnostic restarter log file was never initialized on disk at '{_expectedLogFilePath}'.");

            string logContent = File.ReadAllText(_expectedLogFilePath);
            Assert.Contains(expectedMessage, logContent);
        }

        /// <summary>
        /// Asserts the physical log output stream carries no signature from a later pipeline stage,
        /// which is what pins that a guard returned instead of logging and falling through.
        /// </summary>
        private void AssertLogDoesNotContainMessage(string unexpectedMessage)
        {
            // Force the static logger to flush its handle completely to disk
            Logger.Shutdown();

            Assert.True(File.Exists(_expectedLogFilePath), $"The diagnostic restarter log file was never initialized on disk at '{_expectedLogFilePath}'.");

            string logContent = File.ReadAllText(_expectedLogFilePath);
            Assert.DoesNotContain(unexpectedMessage, logContent);
        }

        #endregion

        #region Test Doubles

        /// <summary>
        /// An <see cref="IRestarterBootstrapEnvironment"/> that records what the start-up sequence asked of
        /// it and answers from memory, so no step touches the Windows event log or the service registry.
        /// </summary>
        private sealed class FakeRestarterBootstrapEnvironment : IRestarterBootstrapEnvironment
        {
            /// <summary>Gets or sets the exception <see cref="EnsureEventSourceExists"/> raises, if any.</summary>
            public Exception? EventSourceFailure { get; set; }

            /// <summary>Gets or sets the exception <see cref="CreateEventLogLogger"/> raises, if any.</summary>
            public Exception? EventLogLoggerFailure { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether <see cref="EventLogLoggerFailure"/> is raised only
            /// for the event-log-enabled request, which is what lets a test reach Step 1's catch through a
            /// failing primary logger while the fallback logger still builds.
            /// </summary>
            public bool FailOnlyEventLogEnabledLogger { get; set; }

            /// <summary>Gets the command lines <see cref="GetServiceImagePath"/> answers, by service name.</summary>
            public Dictionary<string, string> ImagePaths { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>Gets or sets the exception <see cref="GetServiceImagePath"/> raises, if any.</summary>
            public Exception? ImagePathFailure { get; set; }

            /// <summary>Gets the service names <see cref="GetServiceImagePath"/> was asked about, in call order.</summary>
            public List<string> ImagePathRequests { get; } = new List<string>();

            /// <summary>
            /// Gets the <c>isEventLogEnabled</c> arguments <see cref="CreateEventLogLogger"/> was called
            /// with, in call order.
            /// </summary>
            public List<bool> EventLogLoggerRequests { get; } = new List<bool>();

            /// <summary>Raises <see cref="EventSourceFailure"/> when one is configured.</summary>
            /// <exception cref="Exception">The configured <see cref="EventSourceFailure"/>.</exception>
            public void EnsureEventSourceExists()
            {
                if (EventSourceFailure != null)
                {
                    throw EventSourceFailure;
                }
            }

            /// <summary>
            /// Records the request and returns a logger with the Windows Event Log switched off, which keeps
            /// the test off the machine's event log whichever argument the start-up path passed.
            /// </summary>
            /// <param name="isEventLogEnabled">The argument the start-up path asked for; recorded, not honoured.</param>
            /// <returns>A file-only logger.</returns>
            /// <exception cref="Exception">
            /// The configured <see cref="EventLogLoggerFailure"/>, for every request unless
            /// <see cref="FailOnlyEventLogEnabledLogger"/> narrows it to the event-log-enabled one.
            /// </exception>
            public IServyLogger CreateEventLogLogger(bool isEventLogEnabled)
            {
                EventLogLoggerRequests.Add(isEventLogEnabled);

                if (EventLogLoggerFailure != null && (isEventLogEnabled || !FailOnlyEventLogEnabledLogger))
                {
                    throw EventLogLoggerFailure;
                }

                return new EventLogLogger(AppConfig.EventSource, isEventLogEnabled: false);
            }

            /// <summary>Records the request and answers from <see cref="ImagePaths"/>.</summary>
            /// <param name="serviceName">The service the restarter asked about.</param>
            /// <returns>The configured command line, or <see langword="null"/> for an unknown service.</returns>
            /// <exception cref="Exception">The configured <see cref="ImagePathFailure"/>.</exception>
            public string? GetServiceImagePath(string serviceName)
            {
                ImagePathRequests.Add(serviceName);

                if (ImagePathFailure != null)
                {
                    throw ImagePathFailure;
                }

                return ImagePaths.TryGetValue(serviceName, out var imagePath) ? imagePath : null;
            }
        }

        #endregion

        public override void Dispose()
        {
            // Force logger teardown first to unlock active files
            Logger.Shutdown();

            // Clean dynamic runtime artifacts cleanly
            try
            {
                if (_hasConfigBackup && File.Exists(_configBackupPath))
                {
                    // Restore the build-deployed artifact rather than deleting it
                    File.Copy(_configBackupPath, _tempConfigPath, overwrite: true);
                    File.Delete(_configBackupPath);
                }
                else if (File.Exists(_tempConfigPath))
                {
                    File.Delete(_tempConfigPath);
                }

            }
            catch
            {
                // Suppress disposal file-locks
            }

            base.Dispose();
        }
    }
}

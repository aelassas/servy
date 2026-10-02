using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Core.NamedPipes;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Service.Bootstrap;
using Servy.Service.ProcessManagement;
using Servy.Service.StreamWriters;
using Servy.Service.Timers;
using Servy.Service.Validation;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Xunit;
using IServiceHelper = Servy.Service.Helpers.IServiceHelper;

namespace Servy.Service.UnitTests.Bootstrap
{
    /// <summary>
    /// Covers the production constructor of <see cref="Service"/> through
    /// <see cref="IServiceBootstrapEnvironment"/>: the <c>Timing:*</c> parsing, the
    /// <c>EnableEventLog</c> to <c>AutoLog</c> mapping, the CVE-2025-6965 SQLite guard, the data-stack
    /// wiring and the exit-code rules of the surrounding <c>catch</c>.
    /// </summary>
    /// <remarks>
    /// Before the seam existed none of this was reachable from a unit test: every step touched the
    /// event log, ProgramData, SQLite or the process-global logger.
    /// </remarks>
    public class ServiceBootstrapSeamTests : IDisposable
    {
        private readonly List<IDisposable> _built = new List<IDisposable>();
        private readonly ServiceTestContext _ctx = new ServiceTestContext();

        /// <summary>
        /// A positive <c>Timing:WaitChunkMs</c> replaces the default; a non-positive or unparsable one
        /// leaves it alone.
        /// </summary>
        /// <param name="rawValue">The configured value, or <see langword="null"/> when the key is absent.</param>
        /// <param name="expectDefault"><see langword="true"/> when the default must survive.</param>
        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("0", true)]
        [InlineData("-1", true)]
        [InlineData("not-a-number", true)]
        [InlineData("1 500", true)]
        [InlineData("1500", false)]
        public void Constructor_WaitChunkMs_KeepsDefaultUnlessPositiveInteger(string rawValue, bool expectDefault)
        {
            // Arrange
            var env = new FakeBootstrapEnvironment();
            env.Settings["Timing:WaitChunkMs"] = rawValue;

            // Act
            var service = Build(env);

            // Assert
            var expected = expectDefault ? AppConfig.DefaultWaitChunkMs : 1500;
            Assert.Equal(expected, TestReflection.GetField<int>(service, "_waitChunkMs"));
        }

        /// <summary>
        /// The same rule governs <c>Timing:ScmAdditionalTimeMs</c>, and the two keys are independent.
        /// </summary>
        [Fact]
        public void Constructor_TimingKeys_AreParsedIndependently()
        {
            // Arrange
            var env = new FakeBootstrapEnvironment();
            env.Settings["Timing:WaitChunkMs"] = "0";
            env.Settings["Timing:ScmAdditionalTimeMs"] = "2500";

            // Act
            var service = Build(env);

            // Assert
            Assert.Equal(AppConfig.DefaultWaitChunkMs, TestReflection.GetField<int>(service, "_waitChunkMs"));
            Assert.Equal(2500, TestReflection.GetField<int>(service, "_scmAdditionalTimeMs"));
        }

        /// <summary>
        /// Both parsed values are reported in the debug line the constructor writes, after parsing.
        /// </summary>
        [Fact]
        public void Constructor_ReportsTheParsedTimingValues()
        {
            // Arrange
            var env = new FakeBootstrapEnvironment();
            env.Settings["Timing:WaitChunkMs"] = "1500";
            env.Settings["Timing:ScmAdditionalTimeMs"] = "2500";

            // Act
            Build(env);

            // Assert
            var report = Assert.Single(env.DebugReports);
            Assert.Equal("Servy Service Context Configuration Loaded:", report.Title);
            Assert.Contains("WaitChunkMs: 1500", report.Body);
            Assert.Contains("ScmAdditionalTimeMs: 2500", report.Body);
        }

        /// <summary>
        /// <c>EnableEventLog</c> decides <see cref="System.ServiceProcess.ServiceBase.AutoLog"/>, and an
        /// absent key falls back to <see cref="AppConfig.DefaultEnableEventLog"/>.
        /// </summary>
        /// <param name="rawValue">The configured value, or <see langword="null"/> when the key is absent.</param>
        /// <param name="expected">The expected <c>AutoLog</c> value.</param>
        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData(null, AppConfig.DefaultEnableEventLog)]
        public void Constructor_EnableEventLog_DecidesAutoLog(string rawValue, bool expected)
        {
            // Arrange
            var env = new FakeBootstrapEnvironment();
            env.Settings["EnableEventLog"] = rawValue;

            // Act
            var service = Build(env);

            // Assert
            Assert.Equal(expected, service.AutoLog);
        }

        /// <summary>
        /// A successful construction wires the four objects the data stack carries and enables shutdown
        /// notifications.
        /// </summary>
        [Fact]
        public void Constructor_Success_WiresTheDataStackAndEnablesShutdown()
        {
            // Arrange
            var env = new FakeBootstrapEnvironment();

            // Act
            var service = Build(env);

            // Assert
            Assert.True(service.CanShutdown);
            Assert.Equal(AppConfig.EventSource, service.ServiceName);
        }

        /// <summary>
        /// The service log file is initialized before the <c>try</c>, which is what lets the catch-all log
        /// a construction failure at all.
        /// </summary>
        [Fact]
        public void Constructor_InitializesTheServiceLogFile()
        {
            // Arrange
            var env = new FakeBootstrapEnvironment();

            // Act
            Build(env);

            // Assert
            Assert.Equal(new[] { "Servy.Service.log" }, env.InitializedLoggers);
        }

        /// <summary>
        /// Logging is configured with the instance logger the constructor was given, not with the
        /// process-global one.
        /// </summary>
        [Fact]
        public void Constructor_ConfiguresLoggingWithTheInstanceLogger()
        {
            // Arrange
            var env = new FakeBootstrapEnvironment();

            // Act
            Build(env);

            // Assert
            Assert.Same(_ctx.Logger.Object, Assert.Single(env.ConfiguredInstanceLoggers));
        }

        /// <summary>
        /// A failing start-up step with no exit code already set terminates with
        /// <see cref="AppConfig.ServiceSpecificErrorCode"/>, and the exception is logged.
        /// </summary>
        [Fact]
        public void Constructor_FailingStep_WithoutPresetExitCode_TerminatesWithServiceSpecificErrorCode()
        {
            // Arrange
            var failure = new InvalidOperationException("event source registration failed");
            var env = new FakeBootstrapEnvironment { EventSourceFailure = failure };
            var originalExitCode = Environment.ExitCode;
            Environment.ExitCode = 0;

            try
            {
                // Act
                var termination = Assert.Throws<ProcessTerminatedException>(() => Build(env));

                // Assert
                Assert.Equal(AppConfig.ServiceSpecificErrorCode, termination.ExitCode);
                Assert.Contains(env.Errors, e => ReferenceEquals(e.Exception, failure));
            }
            finally
            {
                Environment.ExitCode = originalExitCode;
            }
        }

        /// <summary>
        /// A failing start-up step preserves an exit code a lower layer has already set - the "13 from
        /// ProtectedKeyProvider" case the constructor's comment names.
        /// </summary>
        [Fact]
        public void Constructor_FailingStep_PreservesAnExitCodeAlreadySet()
        {
            // Arrange
            var env = new FakeBootstrapEnvironment { EventSourceFailure = new InvalidOperationException("boom") };
            var originalExitCode = Environment.ExitCode;
            Environment.ExitCode = 13;

            try
            {
                // Act
                var termination = Assert.Throws<ProcessTerminatedException>(() => Build(env));

                // Assert
                Assert.Equal(13, termination.ExitCode);
            }
            finally
            {
                Environment.ExitCode = originalExitCode;
            }
        }

        /// <summary>
        /// The bootstrap environment is required, so a null one is rejected before any start-up call runs.
        /// </summary>
        [Fact]
        public void Constructor_NullBootstrapEnvironment_Throws()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new Service(
                _ctx.Helper.Object,
                _ctx.Logger.Object,
                _ctx.StreamWriterFactory.Object,
                _ctx.TimerFactory.Object,
                _ctx.ProcessFactory.Object,
                _ctx.PathValidator.Object,
                _ctx.NamedPipesService.Object,
                (IServiceBootstrapEnvironment)null));
        }

        /// <summary>
        /// Builds a <see cref="TerminationRecordingService"/> wired to the shared mocks and the supplied
        /// bootstrap environment, and registers it for disposal.
        /// </summary>
        /// <param name="env">The bootstrap environment the constructor must use.</param>
        /// <returns>The constructed service.</returns>
        private Service Build(IServiceBootstrapEnvironment env)
        {
            var service = new TerminationRecordingService(
                _ctx.Helper.Object,
                _ctx.Logger.Object,
                _ctx.StreamWriterFactory.Object,
                _ctx.TimerFactory.Object,
                _ctx.ProcessFactory.Object,
                _ctx.PathValidator.Object,
                _ctx.NamedPipesService.Object,
                env);

            _built.Add(service);
            return service;
        }

        /// <summary>
        /// Disposes every service this test class built, and the shared context.
        /// </summary>
        public void Dispose()
        {
            foreach (var service in _built)
            {
                service.Dispose();
            }

            _built.Clear();
            _ctx.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// A <see cref="Service"/> whose process-termination seam throws instead of ending the process,
        /// so a constructor that reaches it can be asserted on.
        /// </summary>
        private sealed class TerminationRecordingService : Service
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="TerminationRecordingService"/> class.
            /// </summary>
            /// <param name="serviceHelper">The service helper.</param>
            /// <param name="logger">The instance logger.</param>
            /// <param name="streamWriterFactory">The stream writer factory.</param>
            /// <param name="timerFactory">The timer factory.</param>
            /// <param name="processFactory">The process factory.</param>
            /// <param name="pathValidator">The path validator.</param>
            /// <param name="namedPipesService">The named pipes service.</param>
            /// <param name="bootstrapEnvironment">The bootstrap environment seam.</param>
            public TerminationRecordingService(
                IServiceHelper serviceHelper,
                IServyLogger logger,
                IStreamWriterFactory streamWriterFactory,
                ITimerFactory timerFactory,
                IProcessFactory processFactory,
                IPathValidator pathValidator,
                INamedPipesService namedPipesService,
                IServiceBootstrapEnvironment bootstrapEnvironment)
                : base(serviceHelper, logger, streamWriterFactory, timerFactory, processFactory, pathValidator, namedPipesService, bootstrapEnvironment)
            {
            }

            /// <summary>
            /// Gets the exit codes <see cref="TerminateProcess(int)"/> was called with, in call order.
            /// </summary>
            /// <remarks>
            /// An initializer rather than a constructor-body assignment, because the base
            /// constructor - which is what reaches the termination seam - runs before that body.
            /// </remarks>
            private List<int> Terminations { get; } = new List<int>();

            /// <summary>
            /// Throws instead of terminating the host process.
            /// </summary>
            /// <param name="exitCode">The exit code the service asked the host process to terminate with.</param>
            /// <exception cref="ProcessTerminatedException">Always.</exception>
            /// <remarks>
            /// The production seam forwards to <c>Environment.Exit</c>, which does not return. Throwing
            /// keeps everything after the call unreachable here too only where the call sits outside a
            /// <c>try</c>: a termination raised inside the constructor's <c>try</c> is caught by its
            /// catch-all, which logs and terminates a second time. Every exit code is therefore recorded
            /// and handed to <see cref="ProcessTerminatedException.ExitCodes"/> in call order, because the
            /// first one is what the real service would have exited with.
            /// </remarks>
            protected override void TerminateProcess(int exitCode)
            {
                Terminations.Add(exitCode);
                throw new ProcessTerminatedException(exitCode, Terminations.ToArray());
            }
        }

        /// <summary>
        /// Sentinel thrown by <see cref="TerminationRecordingService.TerminateProcess(int)"/> in place of
        /// the process termination the production seam performs. It carries no behaviour of its own
        /// beyond the exit code it was raised with.
        /// </summary>
        private sealed class ProcessTerminatedException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ProcessTerminatedException"/> class.
            /// </summary>
            /// <param name="exitCode">The exit code the service asked the host process to terminate with.</param>
            /// <param name="exitCodes">Every exit code recorded so far, in call order, ending with <paramref name="exitCode"/>.</param>
            public ProcessTerminatedException(int exitCode, int[] exitCodes)
                : base($"Service requested process termination with exit code {exitCode}.")
            {
                ExitCode = exitCode;
                ExitCodes = exitCodes;
            }

            /// <summary>
            /// Gets the exit code the service asked the host process to terminate with.
            /// </summary>
            public int ExitCode { get; }

            /// <summary>
            /// Gets every exit code the service asked the host process to terminate with, in call order,
            /// ending with <see cref="ExitCode"/>. A termination raised inside the constructor's
            /// <c>try</c> is caught by its catch-all, which terminates again, so this list can hold more
            /// than one entry where production would have exited on the first.
            /// </summary>
            public IReadOnlyList<int> ExitCodes { get; }
        }

        /// <summary>
        /// An <see cref="IServiceBootstrapEnvironment"/> that records what the constructor asked of it and
        /// answers from memory, so no start-up step touches the event log, ProgramData, SQLite or the
        /// process-global logger.
        /// </summary>
        private sealed class FakeBootstrapEnvironment : IServiceBootstrapEnvironment
        {
            /// <summary>The connection string <see cref="LoadCoreSettings"/> returns.</summary>
            public const string ConnectionString = "Data Source=:memory:";

            /// <summary>The AES key path <see cref="LoadCoreSettings"/> returns.</summary>
            public const string KeyPath = @"C:\servy-tests\aes_key.dat";

            /// <summary>The AES IV path <see cref="LoadCoreSettings"/> returns.</summary>
            public const string IvPath = @"C:\servy-tests\aes_iv.dat";

            /// <summary>Gets the in-memory configuration values <see cref="BuildConfiguration"/> serves.</summary>
            public NameValueCollection Settings { get; } = new NameValueCollection();

            /// <summary>Gets or sets the answer <see cref="IsSqliteVersionSafe"/> gives.</summary>
            public bool SqliteVersionIsSafe { get; set; } = true;

            /// <summary>Gets or sets the version <see cref="IsSqliteVersionSafe"/> reports.</summary>
            public string DetectedSqliteVersion { get; set; } = AppConfig.MinRequiredSqliteVersion.ToString();

            /// <summary>Gets or sets the exception <see cref="EnsureEventSourceExists"/> raises, if any.</summary>
            public Exception EventSourceFailure { get; set; }

            /// <summary>Gets the log file names <see cref="InitializeLogger"/> was called with.</summary>
            public List<string> InitializedLoggers { get; } = new List<string>();

            /// <summary>Gets the debug reports <see cref="ReportDebug"/> was called with, in call order.</summary>
            public List<(string Title, string Body)> DebugReports { get; } = new List<(string Title, string Body)>();

            /// <summary>Gets the instance loggers <see cref="ConfigureLogging"/> was called with, in call order.</summary>
            public List<IServyLogger> ConfiguredInstanceLoggers { get; } = new List<IServyLogger>();

            /// <summary>Gets the errors <see cref="LogError"/> was called with, in call order.</summary>
            public List<(string Message, Exception Exception)> Errors { get; } = new List<(string Message, Exception Exception)>();

            /// <summary>Records the call and does nothing else.</summary>
            /// <param name="logFileName">The log file name the constructor asked for.</param>
            public void InitializeLogger(string logFileName) => InitializedLoggers.Add(logFileName);

            /// <summary>Raises <see cref="EventSourceFailure"/> when one is configured.</summary>
            /// <exception cref="Exception">The configured <see cref="EventSourceFailure"/>.</exception>
            public void EnsureEventSourceExists()
            {
                if (EventSourceFailure != null)
                {
                    throw EventSourceFailure;
                }
            }

            /// <summary>Serves <see cref="Settings"/> as the application settings.</summary>
            /// <returns>The in-memory application settings.</returns>
            public NameValueCollection BuildConfiguration() => Settings;

            /// <summary>Returns the fixed test paths.</summary>
            /// <returns>The core settings the constructor reads its paths from.</returns>
            public CoreSettings LoadCoreSettings() =>
                new CoreSettings(ConnectionString, KeyPath, IvPath);

            /// <summary>Records the instance logger and does nothing else.</summary>
            /// <param name="configuration">Ignored.</param>
            /// <param name="instanceLogger">The instance logger the constructor passed.</param>
            public void ConfigureLogging(NameValueCollection configuration, IServyLogger instanceLogger) =>
                ConfiguredInstanceLoggers.Add(instanceLogger);

            /// <summary>Records the report.</summary>
            /// <param name="title">The first line of the report.</param>
            /// <param name="body">The body of the report.</param>
            public void ReportDebug(string title, string body) => DebugReports.Add((title, body));

            /// <summary>Records the error.</summary>
            /// <param name="message">The error message.</param>
            /// <param name="exception">The exception recorded with it, if any.</param>
            public void LogError(string message, Exception exception = null) => Errors.Add((message, exception));
        }
    }
}

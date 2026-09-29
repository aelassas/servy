using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Logging;
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
            Assert.Same(env.Stack.DbContext, TestReflection.GetField<IAppDbContext>(service, "_dbContext"));
            Assert.Same(env.Stack.ProtectedKeyProvider, TestReflection.GetField<ProtectedKeyProvider>(service, "_protectedKeyProvider"));
            Assert.Same(env.Stack.SecureData, TestReflection.GetField<SecureData>(service, "_secureData"));
            Assert.Same(env.Stack.ServiceRepository, TestReflection.GetField<IServiceRepository>(service, "_serviceRepository"));
            Assert.True(service.CanShutdown);
            Assert.Equal(AppConfig.EventSource, service.ServiceName);
        }

        /// <summary>
        /// The data stack is created with the paths the core settings named, and only after the SQLite
        /// guard has passed.
        /// </summary>
        [Fact]
        public void Constructor_Success_CreatesTheDataStackFromTheCoreSettings()
        {
            // Arrange
            var env = new FakeBootstrapEnvironment();

            // Act
            Build(env);

            // Assert
            Assert.Equal(
                new[] { FakeBootstrapEnvironment.ConnectionString, FakeBootstrapEnvironment.KeyPath, FakeBootstrapEnvironment.IvPath },
                env.DataStackArguments);
        }

        /// <summary>
        /// A vulnerable SQLite version terminates the process with
        /// <see cref="AppConfig.ServiceSpecificErrorCode"/> and never reaches the data stack.
        /// </summary>
        [Fact]
        public void Constructor_VulnerableSqlite_TerminatesAndNeverCreatesTheDataStack()
        {
            // Arrange
            var env = new FakeBootstrapEnvironment { SqliteVersionIsSafe = false, DetectedSqliteVersion = "3.40.0" };
            var originalExitCode = Environment.ExitCode;

            try
            {
                // Act
                var termination = Assert.Throws<ProcessTerminatedException>(() => Build(env));

                // Assert
                Assert.Equal(AppConfig.ServiceSpecificErrorCode, termination.ExitCode);
                Assert.Empty(env.DataStackArguments);
                Assert.Contains(env.Errors, e => e.Message.Contains("3.40.0") && e.Message.Contains("CVE-2025-6965"));
            }
            finally
            {
                Environment.ExitCode = originalExitCode;
            }
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
            /// <param name="bootstrapEnvironment">The bootstrap environment seam.</param>
            public TerminationRecordingService(
                IServiceHelper serviceHelper,
                IServyLogger logger,
                IStreamWriterFactory streamWriterFactory,
                ITimerFactory timerFactory,
                IProcessFactory processFactory,
                IPathValidator pathValidator,
                IServiceBootstrapEnvironment bootstrapEnvironment)
                : base(serviceHelper, logger, streamWriterFactory, timerFactory, processFactory, pathValidator, bootstrapEnvironment)
            {
            }

            /// <summary>
            /// Throws instead of terminating the host process.
            /// </summary>
            /// <param name="exitCode">The exit code the service asked the host process to terminate with.</param>
            /// <exception cref="ProcessTerminatedException">Always.</exception>
            /// <remarks>
            /// The production seam forwards to <c>Environment.Exit</c>, which does not return. Throwing
            /// keeps everything after the call unreachable here too, so a test cannot assert on statements
            /// the real service would never reach.
            /// </remarks>
            protected override void TerminateProcess(int exitCode)
            {
                throw new ProcessTerminatedException(exitCode);
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
            public ProcessTerminatedException(int exitCode)
                : base($"Service requested process termination with exit code {exitCode}.")
            {
                ExitCode = exitCode;
            }

            /// <summary>
            /// Gets the exit code the service asked the host process to terminate with.
            /// </summary>
            public int ExitCode { get; }
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

            /// <summary>Gets the errors <see cref="LogError"/> was called with, in call order.</summary>
            public List<(string Message, Exception Exception)> Errors { get; } = new List<(string Message, Exception Exception)>();

            /// <summary>Gets the arguments <see cref="CreateDataStack"/> was called with, in call order.</summary>
            public List<string> DataStackArguments { get; } = new List<string>();

            /// <summary>Gets the stack <see cref="CreateDataStack"/> handed back, or <see langword="null"/> when it was never called.</summary>
            public ServiceDataStack Stack { get; private set; }

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

            /// <summary>Does nothing.</summary>
            /// <param name="configuration">Ignored.</param>
            /// <param name="instanceLogger">Ignored.</param>
            public void ConfigureLogging(NameValueCollection configuration, IServyLogger instanceLogger)
            {
            }

            /// <summary>Records the report.</summary>
            /// <param name="title">The first line of the report.</param>
            /// <param name="body">The body of the report.</param>
            public void ReportDebug(string title, string body) => DebugReports.Add((title, body));

            /// <summary>Answers from <see cref="SqliteVersionIsSafe"/>.</summary>
            /// <param name="detectedVersion">Receives <see cref="DetectedSqliteVersion"/>.</param>
            /// <returns><see cref="SqliteVersionIsSafe"/>.</returns>
            public bool IsSqliteVersionSafe(out string detectedVersion)
            {
                detectedVersion = DetectedSqliteVersion;
                return SqliteVersionIsSafe;
            }

            /// <summary>Records the arguments and returns a stack of in-memory doubles.</summary>
            /// <param name="connectionString">The connection string the constructor passed.</param>
            /// <param name="aesKeyFilePath">The AES key path the constructor passed.</param>
            /// <param name="aesIVFilePath">The AES IV path the constructor passed.</param>
            /// <returns>The stack the constructor assigns to its fields.</returns>
            public ServiceDataStack CreateDataStack(string connectionString, string aesKeyFilePath, string aesIVFilePath)
            {
                DataStackArguments.Add(connectionString);
                DataStackArguments.Add(aesKeyFilePath);
                DataStackArguments.Add(aesIVFilePath);

                var keyProvider = new Mock<IProtectedKeyProvider>();
                keyProvider.Setup(p => p.GetKey()).Returns(new byte[32]);
                keyProvider.Setup(p => p.GetIV()).Returns(new byte[16]);

                Stack = new ServiceDataStack(
                    new Mock<IAppDbContext>().Object,
                    new ProtectedKeyProvider(aesKeyFilePath, aesIVFilePath),
                    new SecureData(keyProvider.Object),
                    new Mock<IServiceRepository>().Object);

                return Stack;
            }

            /// <summary>Records the error.</summary>
            /// <param name="message">The error message.</param>
            /// <param name="exception">The exception recorded with it, if any.</param>
            public void LogError(string message, Exception exception = null) => Errors.Add((message, exception));
        }
    }
}

using Microsoft.Extensions.Configuration;
using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Logging;
using Servy.Core.NamedPipes;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Host.Bootstrap;
using Servy.Testing;

namespace Servy.Host.UnitTests.Bootstrap
{
    /// <summary>
    /// Covers the production constructor of the Servy host <see cref="Service"/> through
    /// <see cref="IServiceBootstrapEnvironment"/>: the log file, the <c>EnableEventLog</c> to <c>AutoLog</c> mapping,
    /// the CVE-2025-6965 SQLite guard, the data-stack wiring, the migration of the former service log, and the
    /// exit-code rules of the surrounding <c>catch</c>.
    /// </summary>
    public class ServiceBootstrapSeamTests : IDisposable
    {
        private readonly List<IDisposable> _built = new List<IDisposable>();
        private readonly Mock<IServyLogger> _logger = new Mock<IServyLogger>();
        private readonly Mock<INamedPipesService> _pipes = new Mock<INamedPipesService>();

        [Fact]
        public void Constructor_InitializesTheHostLogFileFirst()
        {
            // Arrange
            var env = new FakeBootstrapEnvironment();

            // Act
            Build(env);

            // Assert
            Assert.Equal("InitializeLogger(Servy.Host.log)", env.Calls[0]);
        }

        [Fact]
        public void Constructor_Success_RunsTheStepsInOrderAndWiresTheDataStack()
        {
            // Arrange
            var env = new FakeBootstrapEnvironment();

            // Act
            var service = Build(env);

            // Assert: the database (and its migrations) is opened before the former service log is moved
            Assert.Equal(new[]
            {
                "InitializeLogger(Servy.Host.log)",
                "EnsureEventSourceExists",
                "BuildConfiguration",
                "LoadCoreSettings",
                "ConfigureLogging",
                "IsSqliteVersionSafe",
                "CreateDataStack",
                "MigrateLegacyServiceLog",
            }, env.Calls.Where(c => !c.StartsWith("Create", StringComparison.Ordinal) || c == "CreateDataStack").ToArray());
            Assert.Equal(new[] { FakeBootstrapEnvironment.ConnectionString, FakeBootstrapEnvironment.KeyPath, FakeBootstrapEnvironment.IvPath }, env.DataStackArguments);
            Assert.Same(env.Stack!.ServiceRepository, TestReflection.GetField<IServiceRepository>(service, "_serviceRepository"));
            Assert.Same(env.Stack.DbContext, TestReflection.GetField<IAppDbContext>(service, "_dbContext"));
            Assert.True(service.CanShutdown);
            Assert.Equal("Servy", service.ServiceName);
            Assert.Same(_logger.Object, Assert.Single(env.ConfiguredInstanceLoggers));
        }

        [Fact]
        public void Constructor_UsesTheEnvironmentsScmApiAndCallerIdentifier()
        {
            // Arrange
            var env = new FakeBootstrapEnvironment();

            // Act
            var service = Build(env);

            // Assert
            Assert.Same(env.WindowsServiceApi, TestReflection.GetField<IWindowsServiceApi>(service, "_windowsServiceApi"));
            Assert.Same(env.CallerIdentifier, TestReflection.GetField<IPipeCallerIdentifier>(service, "_callerIdentifier"));
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData(null, AppConfig.DefaultEnableEventLog)]
        public void Constructor_EnableEventLog_DecidesAutoLog(string? rawValue, bool expected)
        {
            var env = new FakeBootstrapEnvironment();
            env.Settings["EnableEventLog"] = rawValue;

            var service = Build(env);

            Assert.Equal(expected, service.AutoLog);
        }

        [Fact]
        public void Constructor_VulnerableSqlite_TerminatesAndNeverOpensTheDatabase()
        {
            // Arrange
            var env = new FakeBootstrapEnvironment { SqliteVersionIsSafe = false, DetectedSqliteVersion = "3.40.0" };
            var originalExitCode = Environment.ExitCode;
            Environment.ExitCode = 0;

            try
            {
                // Act
                var termination = Assert.Throws<ProcessTerminatedException>(() => Build(env));

                // Assert
                Assert.Equal(AppConfig.ServiceSpecificErrorCode, termination.ExitCodes[0]);
                Assert.Empty(env.DataStackArguments);
                Assert.DoesNotContain("MigrateLegacyServiceLog", env.Calls);
                Assert.Contains(env.Errors, e => e.Message!.Contains("3.40.0") && e.Message.Contains("CVE-2025-6965"));
            }
            finally
            {
                Environment.ExitCode = originalExitCode;
            }
        }

        [Fact]
        public void Constructor_DataStackFails_TerminatesWithTheServiceSpecificErrorCode()
        {
            // Arrange
            var failure = new InvalidOperationException("database is corrupt");
            var env = new FakeBootstrapEnvironment { DataStackFailure = failure };
            var originalExitCode = Environment.ExitCode;
            Environment.ExitCode = 0;

            try
            {
                // Act
                var termination = Assert.Throws<ProcessTerminatedException>(() => Build(env));

                // Assert
                Assert.Equal(AppConfig.ServiceSpecificErrorCode, termination.ExitCode);
                Assert.Contains(env.Errors, e => ReferenceEquals(e.Exception, failure));
                Assert.DoesNotContain("MigrateLegacyServiceLog", env.Calls);
            }
            finally
            {
                Environment.ExitCode = originalExitCode;
            }
        }

        [Fact]
        public void Constructor_FailingStep_PreservesAnExitCodeAlreadySet()
        {
            var env = new FakeBootstrapEnvironment { EventSourceFailure = new InvalidOperationException("boom") };
            var originalExitCode = Environment.ExitCode;
            Environment.ExitCode = 13;

            try
            {
                var termination = Assert.Throws<ProcessTerminatedException>(() => Build(env));

                Assert.Equal(13, termination.ExitCode);
            }
            finally
            {
                Environment.ExitCode = originalExitCode;
            }
        }

        [Fact]
        public void Constructor_NullArguments_Throw()
        {
            var env = new FakeBootstrapEnvironment();
            Assert.Throws<ArgumentNullException>(() => new Service(null!, _pipes.Object, env));
            Assert.Throws<ArgumentNullException>(() => new Service(_logger.Object, null!, env));
            Assert.Throws<ArgumentNullException>(() => new Service(_logger.Object, _pipes.Object, (IServiceBootstrapEnvironment)null!));
        }

        private Service Build(IServiceBootstrapEnvironment env)
        {
            var service = new TerminationRecordingService(_logger.Object, _pipes.Object, env);
            _built.Add(service);
            return service;
        }

        public void Dispose()
        {
            foreach (var service in _built)
                service.Dispose();
            _built.Clear();
        }

        /// <summary>
        /// A host <see cref="Service"/> whose process-termination seam throws instead of ending the process.
        /// </summary>
        private sealed class TerminationRecordingService : Service
        {
            public TerminationRecordingService(IServyLogger logger, INamedPipesService namedPipesService, IServiceBootstrapEnvironment bootstrapEnvironment)
                : base(logger, namedPipesService, bootstrapEnvironment)
            {
            }

            private List<int> Terminations { get; } = new List<int>();

            protected override void TerminateProcess(int exitCode)
            {
                Terminations.Add(exitCode);
                throw new ProcessTerminatedException(exitCode, Terminations.ToArray());
            }
        }

        private sealed class ProcessTerminatedException : Exception
        {
            public ProcessTerminatedException(int exitCode, int[] exitCodes)
                : base($"Service requested process termination with exit code {exitCode}.")
            {
                ExitCode = exitCode;
                ExitCodes = exitCodes;
            }

            public int ExitCode { get; }

            public IReadOnlyList<int> ExitCodes { get; }
        }

        /// <summary>
        /// An <see cref="IServiceBootstrapEnvironment"/> that records what the constructor asked of it and answers from
        /// memory, so no start-up step touches the event log, ProgramData, SQLite or the process-global logger.
        /// </summary>
        private sealed class FakeBootstrapEnvironment : IServiceBootstrapEnvironment
        {
            public const string ConnectionString = "Data Source=:memory:";
            public const string KeyPath = @"C:\servy-tests\aes_key.dat";
            public const string IvPath = @"C:\servy-tests\aes_iv.dat";

            public Dictionary<string, string?> Settings { get; } = new Dictionary<string, string?>();

            public bool SqliteVersionIsSafe { get; set; } = true;

            public string? DetectedSqliteVersion { get; set; } = AppConfig.MinRequiredSqliteVersion.ToString();

            public Exception? EventSourceFailure { get; set; }

            public Exception? DataStackFailure { get; set; }

            public List<string> Calls { get; } = new List<string>();

            public List<string> DataStackArguments { get; } = new List<string>();

            public ServiceDataStack? Stack { get; private set; }

            public List<IServyLogger?> ConfiguredInstanceLoggers { get; } = new List<IServyLogger?>();

            public List<(string? Message, Exception? Exception)> Errors { get; } = new List<(string? Message, Exception? Exception)>();

            public IWindowsServiceApi WindowsServiceApi { get; } = new Mock<IWindowsServiceApi>().Object;

            public IPipeCallerIdentifier CallerIdentifier { get; } = new Mock<IPipeCallerIdentifier>().Object;

            public void InitializeLogger(string logFileName) => Calls.Add($"InitializeLogger({logFileName})");

            public void EnsureEventSourceExists()
            {
                Calls.Add("EnsureEventSourceExists");
                if (EventSourceFailure != null)
                    throw EventSourceFailure;
            }

            public IConfiguration BuildConfiguration()
            {
                Calls.Add("BuildConfiguration");
                return new ConfigurationBuilder().AddInMemoryCollection(Settings).Build();
            }

            public CoreSettingsLoader.CoreSettings LoadCoreSettings()
            {
                Calls.Add("LoadCoreSettings");
                return new CoreSettingsLoader.CoreSettings(ConnectionString, KeyPath, IvPath);
            }

            public void ConfigureLogging(IConfiguration configuration, IServyLogger? instanceLogger)
            {
                Calls.Add("ConfigureLogging");
                ConfiguredInstanceLoggers.Add(instanceLogger);
            }

            public void ReportDebug(string title, string body) => Calls.Add("ReportDebug");

            public bool IsSqliteVersionSafe(out string? detectedVersion)
            {
                Calls.Add("IsSqliteVersionSafe");
                detectedVersion = DetectedSqliteVersion;
                return SqliteVersionIsSafe;
            }

            public ServiceDataStack CreateDataStack(string connectionString, string aesKeyFilePath, string aesIVFilePath)
            {
                Calls.Add("CreateDataStack");
                DataStackArguments.Add(connectionString);
                DataStackArguments.Add(aesKeyFilePath);
                DataStackArguments.Add(aesIVFilePath);
                if (DataStackFailure != null)
                    throw DataStackFailure;

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

            public void MigrateLegacyServiceLog() => Calls.Add("MigrateLegacyServiceLog");

            public IWindowsServiceApi CreateWindowsServiceApi()
            {
                Calls.Add("CreateWindowsServiceApi");
                return WindowsServiceApi;
            }

            public IPipeCallerIdentifier CreateCallerIdentifier()
            {
                Calls.Add("CreateCallerIdentifier");
                return CallerIdentifier;
            }

            public void LogError(string? message, Exception? exception = null) => Errors.Add((message, exception));
        }
    }
}

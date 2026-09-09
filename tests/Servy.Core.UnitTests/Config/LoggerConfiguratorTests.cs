using Microsoft.Extensions.Configuration;
using Moq;
using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Core.UnitTests.Logging;

namespace Servy.Core.UnitTests.Config
{
    /// <summary>
    /// Unit tests for <see cref="LoggerConfigurator"/>, the single logging configuration entry point
    /// shared by the CLI, Restarter, Service, Manager and Desktop processes.
    /// </summary>
    /// <remarks>
    /// Shares <see cref="LoggerCollection"/> with the <c>Logger</c> tests: <c>ConfigureFromAppSettings</c>
    /// drives the static <see cref="Logger"/>, so these tests must not run beside them.
    /// </remarks>
    [Collection(LoggerCollection.Name)]
    public class LoggerConfiguratorTests : IDisposable
    {
        private readonly string _testFileName;
        private readonly string _fullLogPath;

        public LoggerConfiguratorTests()
        {
            // Reset the static state so a previous test's writer cannot hold this test's file
            Logger.Shutdown();

            _testFileName = $"LoggerConfiguratorTest_{Guid.NewGuid():N}.log";
            _fullLogPath = Path.Combine(Logger.EffectiveLogsPath, _testFileName);

            CleanupFiles();
        }

        public void Dispose()
        {
            Logger.Shutdown();
            CleanupFiles();
        }

        private void CleanupFiles()
        {
            try { if (File.Exists(_fullLogPath)) File.Delete(_fullLogPath); } catch { /* Best-effort cleanup */ }
        }

        private static IConfiguration BuildConfig(Dictionary<string, string?>? settings = null)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
                .Build();
        }

        [Fact]
        public void ConfigureFromAppSettings_NullConfig_ThrowsArgumentNullException()
        {
            // Arrange & Act
            var ex = Assert.Throws<ArgumentNullException>(() => LoggerConfigurator.ConfigureFromAppSettings(null!));

            // Assert
            Assert.Equal("config", ex.ParamName);
        }

        [Fact]
        public void ConfigureFromAppSettings_WithInstanceLogger_ForwardsParsedLogLevelAndEventLogFlag()
        {
            // Arrange
            // Both values differ from AppConfig.DefaultLogLevel and AppConfig.DefaultEnableEventLog,
            // so a wiring that ignored the configuration could not satisfy the assertions below.
            var config = BuildConfig(new Dictionary<string, string?>
            {
                { "LogLevel", "Warn" },
                { "EnableEventLog", "false" },
            });
            var instanceLogger = new Mock<IServyLogger>();

            // Act
            LoggerConfigurator.ConfigureFromAppSettings(config, instanceLogger: instanceLogger.Object);

            // Assert
            instanceLogger.Verify(l => l.SetLogLevel(LogLevel.Warn), Times.Once);
            instanceLogger.Verify(l => l.SetIsEventLogEnabled(false), Times.Once);
        }

        [Fact]
        public void ConfigureFromAppSettings_WithLogFileName_InitializesThatFileAndReportsTheInstanceEventLogFlag()
        {
            // Arrange
            var config = BuildConfig(new Dictionary<string, string?>
            {
                { "LogLevel", "Debug" },
                { "EnableEventLog", "false" },
            });
            var instanceLogger = new Mock<IServyLogger>();

            // Act
            LoggerConfigurator.ConfigureFromAppSettings(config, logFileName: _testFileName, instanceLogger: instanceLogger.Object);
            Logger.Shutdown();

            // Assert
            Assert.True(File.Exists(_fullLogPath), "The file name should select the overload that initializes the static logger with it.");
            Assert.Contains("EnableEventLog: False", File.ReadAllText(_fullLogPath));
        }

        [Fact]
        public void ConfigureFromAppSettings_NoInstanceLogger_ReportsEventLogAsNotApplicable()
        {
            // Arrange
            var config = BuildConfig(new Dictionary<string, string?>
            {
                { "LogLevel", "Debug" },
                { "EnableEventLog", "true" },
            });

            // Act
            LoggerConfigurator.ConfigureFromAppSettings(config, logFileName: _testFileName);
            Logger.Shutdown();

            // Assert
            // Without an instance logger there is no event-log sink in this process, so the parsed
            // value must not be reported as "Loaded" (the defect closed #4259 fixed).
            Assert.True(File.Exists(_fullLogPath));
            Assert.Contains("EnableEventLog: n/a (no event-log sink in this process)", File.ReadAllText(_fullLogPath));
        }

        [Fact]
        public void ConfigureFromAppSettings_WithRotationSettings_ForwardsEveryParsedValueToTheLogFile()
        {
            // Arrange
            // Every value differs from its AppConfig default, and the two booleans differ from each
            // other as do the two integers, so neither a dropped wiring nor a swapped pair could
            // satisfy every assertion below.
            var config = BuildConfig(new Dictionary<string, string?>
            {
                { "LogLevel", "Debug" },
                { "LogRollingInterval", "Daily" },
                { "EnableSizeRotation", "false" },
                { "LogRotationSizeMB", "42" },
                { "MaxBackupLogFiles", "7" },
                { "UseLocalTimeForRotation", "true" },
            });

            // Act
            LoggerConfigurator.ConfigureFromAppSettings(config, logFileName: _testFileName);
            Logger.Shutdown();

            // Assert
            var logText = File.ReadAllText(_fullLogPath);
            Assert.Contains("EnableSizeRotation: False", logText);
            Assert.Contains("LogRotationSizeMB: 42", logText);
            Assert.Contains("LogRollingInterval: 0 (Daily)", logText);
            Assert.Contains("MaxBackupLogFiles: 7", logText);
            Assert.Contains("UseLocalTimeForRotation: True", logText);
        }
    }
}

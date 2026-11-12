using Moq;
using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.IO;
using Servy.Core.Logging;
using Servy.Core.UnitTests.Logging;
using Servy.Testing;
using System;
using System.Collections.Specialized;
using System.IO;
using Xunit;

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
            // Reset the static state so a previous test's writer cannot hold this test's file, and forget
            // the previous test's file name so a call without a file name does not open that file again
            Logger.Initialize((string)null);

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

        [Fact]
        public void ConfigureFromAppSettings_NullConfig_ThrowsArgumentNullException()
        {
            // Arrange & Act
            var ex = Assert.Throws<ArgumentNullException>(() => LoggerConfigurator.ConfigureFromAppSettings(null));

            // Assert
            Assert.Equal("config", ex.ParamName);
        }

        [Fact]
        public void ConfigureFromAppSettings_WithInstanceLogger_ForwardsParsedLogLevelAndEventLogFlag()
        {
            // Arrange
            // Both values differ from AppConfig.DefaultLogLevel and AppConfig.DefaultEnableEventLog,
            // so a wiring that ignored the configuration could not satisfy the assertions below.
            var config = new NameValueCollection
            {
                { "LogLevel", "Warn" },
                { "EnableEventLog", "false" },
            };
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
            var config = new NameValueCollection
            {
                { "LogLevel", "Debug" },
                { "EnableEventLog", "false" },
            };
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
            var config = new NameValueCollection
            {
                { "LogLevel", "Debug" },
                { "EnableEventLog", "true" },
            };

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
            var config = new NameValueCollection
            {
                { "LogLevel", "Debug" },
                { "LogRollingInterval", "Daily" },
                { "EnableSizeRotation", "false" },
                { "LogRotationSizeMB", "42" },
                { "MaxBackupLogFiles", "7" },
                { "UseLocalTimeForRotation", "true" },
            };

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

        [Theory]
        [InlineData(true)]   // Logger.Initialize(fileName, ...) arm
        [InlineData(false)]  // Logger.Initialize(...) arm
        public void ConfigureFromAppSettings_WithRotationSettings_PassesEveryParsedValueToTheStaticLogger(bool withLogFileName)
        {
            // Arrange
            // Each value differs from its AppConfig default and from the other value of its type.
            var config = new NameValueCollection
            {
                { "LogRollingInterval", "Daily" },
                { "EnableSizeRotation", "false" },
                { "LogRotationSizeMB", "42" },
                { "MaxBackupLogFiles", "7" },
                { "UseLocalTimeForRotation", "true" },
            };

            // Act
            LoggerConfigurator.ConfigureFromAppSettings(config, logFileName: withLogFileName ? _testFileName : null);

            // Assert
            Assert.False(TestReflection.GetFieldStatic<bool>(typeof(Logger), "_enableSizeRotation"));
            Assert.Equal(42, TestReflection.GetFieldStatic<int>(typeof(Logger), "_logRotationSizeMB"));
            Assert.Equal(DateRotationType.Daily, TestReflection.GetFieldStatic<DateRotationType>(typeof(Logger), "_dateRotationType"));
            Assert.Equal(7, TestReflection.GetFieldStatic<int>(typeof(Logger), "_maxBackupLogFiles"));
            Assert.True(TestReflection.GetFieldStatic<bool>(typeof(Logger), "_useLocalTimeForRotation"));

            // Without a log file name no writer exists, so no other test's file was re-opened
            if (!withLogFileName)
                Assert.Null(TestReflection.GetFieldStatic<RotatingStreamWriter>(typeof(Logger), "_writer"));
        }
    }
}

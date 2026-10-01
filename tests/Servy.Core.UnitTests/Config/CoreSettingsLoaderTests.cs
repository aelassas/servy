using Moq;
using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Core.UnitTests.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Servy.Core.UnitTests.Config
{
    public class CoreSettingsLoaderTests
    {
        [Fact]
        public void Load_ReturnsTheAppConfigDefaults()
        {
            // Act
            var result = CoreSettingsLoader.Load();

            // Assert
            Assert.Equal(AppConfig.DefaultConnectionString, result.ConnectionString);
            Assert.Equal(AppConfig.DefaultAESKeyPath, result.AESKeyFilePath);
            Assert.Equal(AppConfig.DefaultAESIVPath, result.AESIVFilePath);
        }

        [Fact]
        public void Load_DatabaseAndKeyLiveInTheHardenedVault()
        {
            // Act
            var result = CoreSettingsLoader.Load();

            // Assert
            // ServyExePermissionsHardener only hardens the vault, so neither may point elsewhere.
            Assert.Contains($"Data Source={Path.Combine(AppConfig.DbFolderPath, AppConfig.DatabaseFileName)};", result.ConnectionString);
            Assert.Equal(Path.Combine(AppConfig.SecurityFolderPath, AppConfig.AESKeyFileName), result.AESKeyFilePath);
            Assert.Equal(AppConfig.SecurityFolderPath, Path.GetDirectoryName(result.AESIVFilePath));
            Assert.StartsWith(AppConfig.ProgramDataPath, AppConfig.DbFolderPath);
            Assert.StartsWith(AppConfig.ProgramDataPath, AppConfig.SecurityFolderPath);
        }

        [Fact]
        public void CoreSettingsLoader_NothingThatProducesTheSettingsTakesTheApplicationSettings()
        {
            // Arrange
            // The settings are read-only: the only public member that produces them takes no
            // appSettings collection (or any other source) that could relocate the database or the key.
            var methods = typeof(CoreSettingsLoader).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            // Assert
            var producers = methods.Where(m => m.ReturnType == typeof(CoreSettings)).ToList();
            var load = Assert.Single(producers);
            Assert.Equal(nameof(CoreSettingsLoader.Load), load.Name);
            Assert.Empty(load.GetParameters());
            Assert.DoesNotContain(methods, m => m.ReturnType != typeof(IReadOnlyList<string>)
                && m.GetParameters().Any(p => typeof(NameValueCollection).IsAssignableFrom(p.ParameterType)));
        }

        [Fact]
        public void WarnAboutIgnoredSettings_NoIgnoredKeySet_WarnsNothing()
        {
            // Arrange
            var logger = new Mock<IServyLogger>();
            var config = new NameValueCollection
            {
                { "LogLevel", "Debug" },
                { "DefaultConnection", "   " },
                { "Security:AESKeyFilePath", "" },
            };

            // Act
            var found = CoreSettingsLoader.WarnAboutIgnoredSettings(config, "Servy.CLI.exe.config", logger.Object);

            // Assert
            Assert.Empty(found);
            logger.Verify(l => l.Warn(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void WarnAboutIgnoredSettings_NullConfig_WarnsNothing()
        {
            // Arrange
            var logger = new Mock<IServyLogger>();

            // Act
            var found = CoreSettingsLoader.WarnAboutIgnoredSettings(null, "Servy.CLI.exe.config", logger.Object);

            // Assert
            Assert.Empty(found);
            logger.Verify(l => l.Warn(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        [Fact]
        public void WarnAboutIgnoredSettings_EveryIgnoredKeySet_WarnsOnceNamingEachKeyAndTheFile()
        {
            // Arrange
            var logger = new Mock<IServyLogger>();
            var config = new NameValueCollection
            {
                { "Security:AESIVFilePath", "D:\\old\\iv.dat" },
                { "DefaultConnection", "Data Source=D:\\old\\Servy.db" },
                { "Security:AESKeyFilePath", "D:\\old\\key.dat" },
            };

            // Act
            var found = CoreSettingsLoader.WarnAboutIgnoredSettings(config, "Servy.Service.Net48.exe.config", logger.Object);

            // Assert
            Assert.Equal(new[] { "DefaultConnection", "Security:AESKeyFilePath", "Security:AESIVFilePath" }, found);
            logger.Verify(l => l.Warn(It.Is<string>(m =>
                m.StartsWith("Servy.Service.Net48.exe.config sets DefaultConnection, Security:AESKeyFilePath, Security:AESIVFilePath, which Servy ignores since v10.2")
                && m.Contains(AppConfig.ProgramDataPath)
                && m.EndsWith("Remove these settings from Servy.Service.Net48.exe.config.")), null), Times.Once);
            logger.VerifyNoOtherCalls();
        }

        [Fact]
        public void WarnAboutIgnoredSettings_OneIgnoredKeySet_NamesOnlyThatKey()
        {
            // Arrange
            var logger = new Mock<IServyLogger>();
            var config = new NameValueCollection { { "Security:AESKeyFilePath", "D:\\old\\key.dat" } };

            // Act
            var found = CoreSettingsLoader.WarnAboutIgnoredSettings(config, "Servy.Restarter.Net48.exe.config", logger.Object);

            // Assert
            Assert.Equal(new[] { "Security:AESKeyFilePath" }, found);
            logger.Verify(l => l.Warn(It.Is<string>(m =>
                m.StartsWith("Servy.Restarter.Net48.exe.config sets Security:AESKeyFilePath, which")
                && m.EndsWith("Remove this setting from Servy.Restarter.Net48.exe.config.")), null), Times.Once);
        }

        [Fact]
        public void Load_TestOverride_IsReturnedUntilCleared()
        {
            // Arrange
            var custom = new CoreSettings("Data Source=custom.db", "C:\\custom\\key.dat", "C:\\custom\\iv.dat");

            try
            {
                // Act
                CoreSettingsLoader.TestOverride = custom;
                var overridden = CoreSettingsLoader.Load();

                CoreSettingsLoader.TestOverride = null;
                var restored = CoreSettingsLoader.Load();

                // Assert
                Assert.Same(custom, overridden);
                Assert.Equal(AppConfig.DefaultConnectionString, restored.ConnectionString);
                Assert.Equal(AppConfig.DefaultAESKeyPath, restored.AESKeyFilePath);
                Assert.Equal(AppConfig.DefaultAESIVPath, restored.AESIVFilePath);
            }
            finally
            {
                CoreSettingsLoader.TestOverride = null;
            }
        }
    }

    /// <summary>
    /// Covers the <see cref="CoreSettingsLoader.WarnAboutIgnoredSettings"/> call that passes no
    /// logger, which warns through the static <see cref="Logger"/>.
    /// </summary>
    /// <remarks>
    /// This is a class of its own so only it joins the sequential logger collection: the static
    /// logger is global state, while the mock-logger tests above stay parallelizable. The CLI, the
    /// desktop app and the manager app all take this path.
    /// </remarks>
    [Collection(LoggerCollection.Name)] // the no-logger call writes through the static Logger
    public class CoreSettingsLoaderStaticLoggerTests
    {
        [Fact]
        public void WarnAboutIgnoredSettings_NoLoggerGiven_WarnsThroughTheStaticLogger()
        {
            // Arrange
            var config = new NameValueCollection
            {
                { "DefaultConnection", @"Data Source=D:\old\Servy.db" },
            };
            var fileName = string.Format("CoreSettingsWarnTestLog_{0:N}.log", Guid.NewGuid());
            var fullPath = Path.Combine(AppConfig.LogsFolderPath, fileName);

            try
            {
                Logger.Shutdown();
                Logger.Initialize(fileName);

                // Act
                var found = CoreSettingsLoader.WarnAboutIgnoredSettings(config, "Servy.CLI.exe.config");
                Logger.Shutdown();

                // Assert
                Assert.Equal(new[] { "DefaultConnection" }, found);
                var log = File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
                Assert.Contains(
                    "Servy.CLI.exe.config sets DefaultConnection, which Servy ignores since v10.2",
                    log,
                    StringComparison.Ordinal);
            }
            finally
            {
                Logger.Shutdown();
                try { if (File.Exists(fullPath)) File.Delete(fullPath); } catch { }
            }
        }
    }
}

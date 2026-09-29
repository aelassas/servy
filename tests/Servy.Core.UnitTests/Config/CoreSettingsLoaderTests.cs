using Microsoft.Extensions.Configuration;
using Moq;
using Servy.Core.Config;
using Servy.Core.Logging;
using System.Reflection;

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
            // IConfiguration (or any other source) that could relocate the database or the key.
            var methods = typeof(CoreSettingsLoader).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            // Assert
            var producers = methods.Where(m => m.ReturnType == typeof(CoreSettingsLoader.CoreSettings)).ToList();
            var load = Assert.Single(producers);
            Assert.Equal(nameof(CoreSettingsLoader.Load), load.Name);
            Assert.Empty(load.GetParameters());
            Assert.DoesNotContain(methods, m => m.ReturnType != typeof(IReadOnlyList<string>)
                && m.GetParameters().Any(p => typeof(IConfiguration).IsAssignableFrom(p.ParameterType)));
        }

        [Fact]
        public void WarnAboutIgnoredSettings_NoIgnoredKeySet_WarnsNothing()
        {
            // Arrange
            var logger = new Mock<IServyLogger>();
            var config = BuildConfig(new Dictionary<string, string?>
            {
                { "LogLevel", "Debug" },
                { "ConnectionStrings:DefaultConnection", "   " },
                { "Security:AESKeyFilePath", "" },
            });

            // Act
            var found = CoreSettingsLoader.WarnAboutIgnoredSettings(config, "appsettings.cli.json", logger.Object);

            // Assert
            Assert.Empty(found);
            logger.Verify(l => l.Warn(It.IsAny<string>(), It.IsAny<Exception?>()), Times.Never);
        }

        [Fact]
        public void WarnAboutIgnoredSettings_NullConfig_WarnsNothing()
        {
            // Arrange
            var logger = new Mock<IServyLogger>();

            // Act
            var found = CoreSettingsLoader.WarnAboutIgnoredSettings(null, "appsettings.cli.json", logger.Object);

            // Assert
            Assert.Empty(found);
            logger.Verify(l => l.Warn(It.IsAny<string>(), It.IsAny<Exception?>()), Times.Never);
        }

        [Fact]
        public void WarnAboutIgnoredSettings_EveryIgnoredKeySet_WarnsOnceNamingEachKeyAndTheFile()
        {
            // Arrange
            var logger = new Mock<IServyLogger>();
            var config = BuildConfig(new Dictionary<string, string?>
            {
                { "Security:AESIVFilePath", @"D:\\old\\iv.dat" },
                { "ConnectionStrings:DefaultConnection", @"Data Source=D:\\old\\Servy.db" },
                { "Security:AESKeyFilePath", @"D:\\old\\key.dat" },
            });

            // Act
            var found = CoreSettingsLoader.WarnAboutIgnoredSettings(config, "appsettings.service.json", logger.Object);

            // Assert
            Assert.Equal(new[] { "ConnectionStrings:DefaultConnection", "Security:AESKeyFilePath", "Security:AESIVFilePath" }, found);
            logger.Verify(l => l.Warn(It.Is<string>(m =>
                m.StartsWith("appsettings.service.json sets ConnectionStrings:DefaultConnection, Security:AESKeyFilePath, Security:AESIVFilePath, which Servy ignores since v10.2")
                && m.Contains(AppConfig.ProgramDataPath)
                && m.EndsWith("Remove these settings from appsettings.service.json.")), null), Times.Once);
            logger.VerifyNoOtherCalls();
        }

        [Fact]
        public void WarnAboutIgnoredSettings_OneIgnoredKeySet_NamesOnlyThatKey()
        {
            // Arrange
            var logger = new Mock<IServyLogger>();
            var config = BuildConfig(new Dictionary<string, string?> { { "Security:AESKeyFilePath", @"D:\\old\\key.dat" } });

            // Act
            var found = CoreSettingsLoader.WarnAboutIgnoredSettings(config, "appsettings.restarter.json", logger.Object);

            // Assert
            Assert.Equal(new[] { "Security:AESKeyFilePath" }, found);
            logger.Verify(l => l.Warn(It.Is<string>(m =>
                m.StartsWith("appsettings.restarter.json sets Security:AESKeyFilePath, which")
                && m.EndsWith("Remove this setting from appsettings.restarter.json.")), null), Times.Once);
        }

        private static IConfiguration BuildConfig(Dictionary<string, string?> settings)
            => new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        [Fact]
        public void Load_TestOverride_IsReturnedUntilCleared()
        {
            // Arrange
            var custom = new CoreSettingsLoader.CoreSettings("Data Source=custom.db", @"C:\custom\key.dat", @"C:\custom\iv.dat");

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
}

using Microsoft.Extensions.Configuration;
using Servy.Core.Config;
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
        public void CoreSettingsLoader_HasNoEntryPointThatTakesTheApplicationSettings()
        {
            // Arrange
            // The settings are read-only: no public member may accept an IConfiguration (or any
            // other source) that could relocate the database or the key.
            var methods = typeof(CoreSettingsLoader).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            // Assert
            var load = Assert.Single(methods);
            Assert.Equal(nameof(CoreSettingsLoader.Load), load.Name);
            Assert.Empty(load.GetParameters());
            Assert.DoesNotContain(methods, m => m.GetParameters().Any(p => typeof(IConfiguration).IsAssignableFrom(p.ParameterType)));
        }

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

using Microsoft.Extensions.Configuration;
using Servy.Core.Config;

namespace Servy.Core.UnitTests.Config
{
    public class CoreSettingsLoaderTests
    {
        private const string SettingsFileName = "appsettings.service.json";

        private static IConfiguration BuildConfig(Dictionary<string, string?>? settings = null)
        {
            var builder = new ConfigurationBuilder();
            if (settings != null)
            {
                builder.AddInMemoryCollection(settings);
            }
            else
            {
                builder.AddInMemoryCollection();
            }
            return builder.Build();
        }

        #region Load Tests

        [Fact]
        public void Load_AllValuesPresent_ReturnsConfiguredValues()
        {
            // Arrange
            var settings = new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Data Source=custom.db" },
                { "Security:AESKeyFilePath", @"C:\custom\key.dat" },
                { "Security:AESIVFilePath", @"C:\custom\iv.dat" },
            };
            var config = BuildConfig(settings);

            // Act
            var result = CoreSettingsLoader.Load(config);

            // Assert
            Assert.Equal("Data Source=custom.db", result.ConnectionString);
            Assert.Equal(@"C:\custom\key.dat", result.AESKeyFilePath);
            Assert.Equal(@"C:\custom\iv.dat", result.AESIVFilePath);
        }

        [Fact]
        public void Load_MissingKeys_FallsBackToAppConfigDefaults()
        {
            // Arrange
            var config = BuildConfig();

            // Act
            var result = CoreSettingsLoader.Load(config);

            // Assert
            Assert.Equal(AppConfig.DefaultConnectionString, result.ConnectionString);
            Assert.Equal(AppConfig.DefaultAESKeyPath, result.AESKeyFilePath);
            Assert.Equal(AppConfig.DefaultAESIVPath, result.AESIVFilePath);
        }

        [Fact]
        public void Load_EmptyStringValues_FallsBackToAppConfigDefaults()
        {
            // Arrange
            // IConfiguration returns "" (not null) for a key present with an empty value, so `??`
            // alone would not fall back here; Load must treat "" as absent too.
            var settings = new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "" },
                { "Security:AESKeyFilePath", "" },
                { "Security:AESIVFilePath", "" },
            };
            var config = BuildConfig(settings);

            // Act
            var result = CoreSettingsLoader.Load(config);

            // Assert
            Assert.Equal(AppConfig.DefaultConnectionString, result.ConnectionString);
            Assert.Equal(AppConfig.DefaultAESKeyPath, result.AESKeyFilePath);
            Assert.Equal(AppConfig.DefaultAESIVPath, result.AESIVFilePath);
        }

        [Fact]
        public void Load_WhitespaceOnlyValues_FallsBackToAppConfigDefaults()
        {
            // Arrange
            var settings = new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "   " },
                { "Security:AESKeyFilePath", "   " },
                { "Security:AESIVFilePath", "   " },
            };
            var config = BuildConfig(settings);

            // Act
            var result = CoreSettingsLoader.Load(config);

            // Assert
            Assert.Equal(AppConfig.DefaultConnectionString, result.ConnectionString);
            Assert.Equal(AppConfig.DefaultAESKeyPath, result.AESKeyFilePath);
            Assert.Equal(AppConfig.DefaultAESIVPath, result.AESIVFilePath);
        }

        [Fact]
        public void Load_PartialOverride_MixesConfiguredAndDefaultValues()
        {
            // Arrange
            var settings = new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Data Source=custom.db" },
            };
            var config = BuildConfig(settings);

            // Act
            var result = CoreSettingsLoader.Load(config);

            // Assert
            Assert.Equal("Data Source=custom.db", result.ConnectionString);
            Assert.Equal(AppConfig.DefaultAESKeyPath, result.AESKeyFilePath);
            Assert.Equal(AppConfig.DefaultAESIVPath, result.AESIVFilePath);
        }

        #endregion

        #region Validate Tests

        [Fact]
        public void Validate_AllValuesPresent_DoesNotThrow()
        {
            // Arrange
            var settings = new CoreSettingsLoader.CoreSettings("Data Source=custom.db", @"C:\key.dat", @"C:\iv.dat");

            // Act & Assert
            var exception = Record.Exception(() => CoreSettingsLoader.Validate(settings, SettingsFileName));
            Assert.Null(exception);
        }

        [Fact]
        public void Validate_EmptyConnectionString_ThrowsInvalidOperationException()
        {
            // Arrange
            var settings = new CoreSettingsLoader.CoreSettings("", @"C:\key.dat", @"C:\iv.dat");

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => CoreSettingsLoader.Validate(settings, SettingsFileName));
            Assert.Contains(SettingsFileName, exception.Message);
        }

        [Fact]
        public void Validate_EmptyAESKeyFilePath_ThrowsInvalidOperationException()
        {
            // Arrange
            var settings = new CoreSettingsLoader.CoreSettings("Data Source=custom.db", "", @"C:\iv.dat");

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => CoreSettingsLoader.Validate(settings, SettingsFileName));
            Assert.Contains(SettingsFileName, exception.Message);
        }

        [Fact]
        public void Validate_EmptyAESIVFilePath_ThrowsInvalidOperationException()
        {
            // Arrange
            var settings = new CoreSettingsLoader.CoreSettings("Data Source=custom.db", @"C:\key.dat", "");

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => CoreSettingsLoader.Validate(settings, SettingsFileName));
            Assert.Contains(SettingsFileName, exception.Message);
        }

        [Fact]
        public void Validate_WhitespaceOnlyValue_ThrowsInvalidOperationException()
        {
            // Arrange
            var settings = new CoreSettingsLoader.CoreSettings("   ", @"C:\key.dat", @"C:\iv.dat");

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => CoreSettingsLoader.Validate(settings, SettingsFileName));
        }

        [Fact]
        public void Validate_AllValuesEmpty_ThrowsInvalidOperationException()
        {
            // Arrange
            var settings = new CoreSettingsLoader.CoreSettings("", "", "");

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => CoreSettingsLoader.Validate(settings, SettingsFileName));
        }

        #endregion

        #region LoadAndValidate Tests

        [Fact]
        public void LoadAndValidate_ValidConfiguration_ReturnsSettings()
        {
            // Arrange
            var settings = new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Data Source=custom.db" },
                { "Security:AESKeyFilePath", @"C:\custom\key.dat" },
                { "Security:AESIVFilePath", @"C:\custom\iv.dat" },
            };
            var config = BuildConfig(settings);

            // Act
            var result = CoreSettingsLoader.LoadAndValidate(config, SettingsFileName);

            // Assert
            Assert.Equal("Data Source=custom.db", result.ConnectionString);
            Assert.Equal(@"C:\custom\key.dat", result.AESKeyFilePath);
            Assert.Equal(@"C:\custom\iv.dat", result.AESIVFilePath);
        }

        [Fact]
        public void LoadAndValidate_MissingConfiguration_FallsBackAndDoesNotThrow()
        {
            // Arrange
            var config = BuildConfig();

            // Act
            var result = CoreSettingsLoader.LoadAndValidate(config, SettingsFileName);

            // Assert
            Assert.Equal(AppConfig.DefaultConnectionString, result.ConnectionString);
            Assert.Equal(AppConfig.DefaultAESKeyPath, result.AESKeyFilePath);
            Assert.Equal(AppConfig.DefaultAESIVPath, result.AESIVFilePath);
        }

        #endregion
    }
}

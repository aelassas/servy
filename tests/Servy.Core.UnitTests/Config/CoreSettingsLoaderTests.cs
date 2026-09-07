using Servy.Core.Config;
using System;
using System.Collections.Specialized;
using Xunit;

namespace Servy.Core.UnitTests.Config
{
    public class CoreSettingsLoaderTests
    {
        #region Load

        [Fact]
        public void Load_AllKeysPresent_UsesConfiguredValues()
        {
            // Arrange
            var config = new NameValueCollection
            {
                { "DefaultConnection", "Data Source=C:\\custom\\Servy.db;" },
                { "Security:AESKeyFilePath", "C:\\custom\\aes_key.dat" },
                { "Security:AESIVFilePath", "C:\\custom\\aes_iv.dat" },
            };

            // Act
            var settings = CoreSettingsLoader.Load(config);

            // Assert
            Assert.Equal("Data Source=C:\\custom\\Servy.db;", settings.ConnectionString);
            Assert.Equal("C:\\custom\\aes_key.dat", settings.AESKeyFilePath);
            Assert.Equal("C:\\custom\\aes_iv.dat", settings.AESIVFilePath);
        }

        [Fact]
        public void Load_NullConfig_FallsBackToDefaultsForAllValues()
        {
            // Act
            var settings = CoreSettingsLoader.Load(null);

            // Assert
            Assert.Equal(AppConfig.DefaultConnectionString, settings.ConnectionString);
            Assert.Equal(AppConfig.DefaultAESKeyPath, settings.AESKeyFilePath);
            Assert.Equal(AppConfig.DefaultAESIVPath, settings.AESIVFilePath);
        }

        [Fact]
        public void Load_EmptyConfig_FallsBackToDefaultsForAllValues()
        {
            // Arrange
            var config = new NameValueCollection();

            // Act
            var settings = CoreSettingsLoader.Load(config);

            // Assert
            Assert.Equal(AppConfig.DefaultConnectionString, settings.ConnectionString);
            Assert.Equal(AppConfig.DefaultAESKeyPath, settings.AESKeyFilePath);
            Assert.Equal(AppConfig.DefaultAESIVPath, settings.AESIVFilePath);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void Load_ConnectionStringIsEmptyOrWhitespace_FallsBackToDefault(string configuredValue)
        {
            // Arrange
            var config = new NameValueCollection { { "DefaultConnection", configuredValue } };

            // Act
            var settings = CoreSettingsLoader.Load(config);

            // Assert
            // `??` alone would not catch this: NameValueCollection returns "" for a present-but-empty
            // key, not null, so the fallback must be triggered by whitespace detection instead.
            Assert.Equal(AppConfig.DefaultConnectionString, settings.ConnectionString);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Load_AESKeyFilePathIsEmptyOrWhitespace_FallsBackToDefault(string configuredValue)
        {
            // Arrange
            var config = new NameValueCollection { { "Security:AESKeyFilePath", configuredValue } };

            // Act
            var settings = CoreSettingsLoader.Load(config);

            // Assert
            Assert.Equal(AppConfig.DefaultAESKeyPath, settings.AESKeyFilePath);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Load_AESIVFilePathIsEmptyOrWhitespace_FallsBackToDefault(string configuredValue)
        {
            // Arrange
            var config = new NameValueCollection { { "Security:AESIVFilePath", configuredValue } };

            // Act
            var settings = CoreSettingsLoader.Load(config);

            // Assert
            Assert.Equal(AppConfig.DefaultAESIVPath, settings.AESIVFilePath);
        }

        [Fact]
        public void Load_ConnectionStringPresentOthersMissing_OnlyMissingValuesFallBack()
        {
            // Arrange
            var config = new NameValueCollection
            {
                { "DefaultConnection", "Data Source=C:\\custom\\Servy.db;" },
            };

            // Act
            var settings = CoreSettingsLoader.Load(config);

            // Assert
            Assert.Equal("Data Source=C:\\custom\\Servy.db;", settings.ConnectionString);
            Assert.Equal(AppConfig.DefaultAESKeyPath, settings.AESKeyFilePath);
            Assert.Equal(AppConfig.DefaultAESIVPath, settings.AESIVFilePath);
        }

        #endregion

        #region Validate

        [Fact]
        public void Validate_AllValuesPresent_DoesNotThrow()
        {
            // Arrange
            var settings = new CoreSettings("Data Source=Servy.db;", "aes_key.dat", "aes_iv.dat");

            // Act & Assert
            var ex = Record.Exception(() => CoreSettingsLoader.Validate(settings, "Servy.Service.config"));
            Assert.Null(ex);
        }

        [Fact]
        public void Validate_NullSettings_Throws()
        {
            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => CoreSettingsLoader.Validate(null, "Servy.Service.exe.config"));
            Assert.Contains("Servy.Service.exe.config", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_ConnectionStringMissingOrWhitespace_Throws(string connectionString)
        {
            // Arrange
            var settings = new CoreSettings(connectionString, "aes_key.dat", "aes_iv.dat");

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => CoreSettingsLoader.Validate(settings, "Servy.Service.config"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_AESKeyFilePathMissingOrWhitespace_Throws(string aesKeyFilePath)
        {
            // Arrange
            var settings = new CoreSettings("Data Source=Servy.db;", aesKeyFilePath, "aes_iv.dat");

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => CoreSettingsLoader.Validate(settings, "Servy.Service.config"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_AESIVFilePathMissingOrWhitespace_Throws(string aesIVFilePath)
        {
            // Arrange
            var settings = new CoreSettings("Data Source=Servy.db;", "aes_key.dat", aesIVFilePath);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => CoreSettingsLoader.Validate(settings, "Servy.Service.config"));
        }

        [Fact]
        public void Validate_ThrowsInvalidOperationException_MessageIncludesSettingsFileName()
        {
            // Arrange
            var settings = new CoreSettings(null, null, null);

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => CoreSettingsLoader.Validate(settings, "Servy.Service.Net48.exe.config"));

            // Assert
            Assert.Contains("Servy.Service.Net48.exe.config", ex.Message);
        }

        #endregion

        #region CoreSettings

        [Fact]
        public void CoreSettings_Constructor_AssignsProperties()
        {
            // Act
            var settings = new CoreSettings("conn", "key", "iv");

            // Assert
            Assert.Equal("conn", settings.ConnectionString);
            Assert.Equal("key", settings.AESKeyFilePath);
            Assert.Equal("iv", settings.AESIVFilePath);
        }

        #endregion
    }
}

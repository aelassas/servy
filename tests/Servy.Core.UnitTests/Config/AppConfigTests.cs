using Servy.Core.Config;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace Servy.Core.UnitTests.Config
{
    public class AppConfigTests
    {
        [Fact]
        public void UpdateCheckTimeouts_AreConsistent()
        {
            // Arrange (Static property validation context)

            // Act & Assert
            Assert.True(AppConfig.UpdateCheckTimeoutSeconds <= AppConfig.UpdateCheckHttpTimeoutSeconds,
                "Cooperative cancellation timeout must not exceed the HTTP client timeout.");
        }

        [Fact]
        public void Version_ShouldNotBeNullOrEmpty()
        {
            // Arrange (Static property validation context)

            // Act
            var version = AppConfig.Version;

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(version));
        }

        [Fact]
        public void ServyServiceUIExe_ShouldBeCorrect()
        {
            // Arrange (Static property validation context)

            // Act
            var exeName = AppConfig.ServyServiceUIExe;

            // Assert
            Assert.Equal("Servy.Service.Net48.exe", exeName);
        }

        [Fact]
        public void ServyServiceCLIExe_ShouldBeCorrect()
        {
            // Arrange (Static property validation context)

            // Act
            var exeName = AppConfig.ServyServiceCLIExe;

            // Assert
            Assert.Equal("Servy.Service.CLI.Net48.exe", exeName);
        }

        [Fact]
        public void DefaultConnectionString_ShouldContainDbFolderPath()
        {
            // Arrange (Static property validation context)

            // Act
            var connectionString = AppConfig.DefaultConnectionString;

            // Assert
            Assert.Contains(AppConfig.DbFolderPath, connectionString);
            Assert.Contains("Servy.db", connectionString);
        }

        [Fact]
        public void DefaultAESKeyPath_ShouldEndWithAesKeyDat()
        {
            // Arrange (Static property validation context)

            // Act
            var keyPath = AppConfig.DefaultAESKeyPath;

            // Assert
            Assert.EndsWith("aes_key.dat", keyPath);
        }

        [Fact]
        public void DefaultAESIVPath_ShouldEndWithAesIvDat()
        {
            // Arrange (Static property validation context)

            // Act
            var ivPath = AppConfig.DefaultAESIVPath;

            // Assert
            Assert.EndsWith("aes_iv.dat", ivPath);
        }

        [Fact]
        public void GetHandleExePath_ShouldReturnFullPath()
        {
            // Arrange
            // Host architecture is resolved dynamically via native environment reflection

            // Act
            var path = AppConfig.GetHandleExePath();

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.EndsWith(AppConfig.HandleExe, path);
        }

        [Fact]
        public void GetServyCLIServicePath_ShouldReturnFullPath()
        {
            // Arrange (Static execution context)

            // Act
            var path = AppConfig.GetServyCLIServicePath();

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(Path.IsPathRooted(path), $"Expected an absolute path but got: {path}");
            Assert.EndsWith("Servy.Service.CLI.Net48.exe", path, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetServyUIServicePath_ShouldReturnFullPath()
        {
            // Arrange (Static execution context)

            // Act
            var path = AppConfig.GetServyUIServicePath();

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(Path.IsPathRooted(path), $"Expected an absolute path but got: {path}");
            Assert.EndsWith("Servy.Service.Net48.exe", path, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ProgramDataPath_ShouldBeUnderCommonApplicationData()
        {
            // Arrange (Static property validation context)

            // Act
            var path = AppConfig.ProgramDataPath;

            // Assert
            var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Servy");
            Assert.Equal(expected, path);
        }

        [Fact]
        public void SecurityFolderPath_ShouldBeTheSecuritySubfolder()
        {
            // Arrange (Static property validation context)

            // Act
            var path = AppConfig.SecurityFolderPath;

            // Assert
            var expected = Path.Combine(AppConfig.ProgramDataPath, "security");
            Assert.Equal(expected, path);
        }
    }
}
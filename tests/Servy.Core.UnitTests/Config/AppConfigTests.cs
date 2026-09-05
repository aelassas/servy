using Servy.Core.Config;
using System.Runtime.InteropServices;

namespace Servy.Core.UnitTests.Config
{
    public class AppConfigTests
    {
        [Fact]
        public void UpdateCheckTimeouts_AreConsistent()
        {
            // Act & Assert
            Assert.True(AppConfig.UpdateCheckTimeoutSeconds <= AppConfig.UpdateCheckHttpTimeoutSeconds,
                "Cooperative cancellation timeout must not exceed the HTTP client timeout.");
        }

        [Fact]
        public void Version_ShouldNotBeNullOrEmpty()
        {
            // Act
            var version = AppConfig.Version;

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(version));
        }

        [Fact]
        public void ServyServiceUIExe_ShouldBeCorrect()
        {
            // Act
            var exeName = AppConfig.ServyServiceUIExe;

            // Assert
            Assert.Equal("Servy.Service.exe", exeName);
        }

        [Fact]
        public void ServyServiceCLIExe_ShouldBeCorrect()
        {
            // Act
            var exeName = AppConfig.ServyServiceCLIExe;

            // Assert
            Assert.Equal("Servy.Service.CLI.exe", exeName);
        }

        [Fact]
        public void DefaultConnectionString_ShouldContainDbFolderPath()
        {
            // Act
            var connectionString = AppConfig.DefaultConnectionString;

            // Assert
            Assert.Contains(AppConfig.DbFolderPath, connectionString);
            Assert.Contains("Servy.db", connectionString);
        }

        [Fact]
        public void DefaultAESKeyPath_ShouldEndWithAesKeyDat()
        {
            // Act
            var keyPath = AppConfig.DefaultAESKeyPath;

            // Assert
            Assert.EndsWith("aes_key.dat", keyPath);
        }

        [Fact]
        public void DefaultAESIVPath_ShouldEndWithAesIvDat()
        {
            // Act
            var ivPath = AppConfig.DefaultAESIVPath;

            // Assert
            Assert.EndsWith("aes_iv.dat", ivPath);
        }

        [Fact]
        public void GetHandleExePath_ShouldReturnFullPath()
        {
            // Act
            var path = AppConfig.GetHandleExePath();

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(Path.IsPathRooted(path), $"Expected an absolute path but got: {path}");

            // Use literal expected values instead of re-evaluating production constants.
            // This breaks the lockstep behavior and guarantees that inverted architectural mappings
            // or swapped internal filenames are successfully caught by the test runner.
            if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
            {
                Assert.EndsWith("handle64a.exe", path, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                Assert.EndsWith("handle64.exe", path, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void GetServyCLIServicePath_ShouldReturnFullPath()
        {
            // Act
            var path = AppConfig.GetServyCLIServicePath();

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(Path.IsPathRooted(path), $"Expected an absolute path but got: {path}");
            Assert.EndsWith("Servy.Service.CLI.exe", path, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetServyUIServicePath_ShouldReturnFullPath()
        {
            // Act
            var path = AppConfig.GetServyUIServicePath();

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(Path.IsPathRooted(path), $"Expected an absolute path but got: {path}");
            Assert.EndsWith("Servy.Service.exe", path, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ProgramDataPath_ShouldBeUnderCommonApplicationData()
        {
            // Act
            var path = AppConfig.ProgramDataPath;

            // Assert
            var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Servy");
            Assert.Equal(expected, path);
        }

        [Fact]
        public void SecurityFolderPath_ShouldBeTheSecuritySubfolder()
        {
            // Act
            var path = AppConfig.SecurityFolderPath;

            // Assert
            var expected = Path.Combine(AppConfig.ProgramDataPath, "security");
            Assert.Equal(expected, path);
        }
    }
}

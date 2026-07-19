using Servy.Core.Config;
using System.Runtime.InteropServices;

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
        public void ServyCoreDllName_ShouldBeCorrect()
        {
            // Arrange (Static property validation context)

            // Act
            var dllName = AppConfig.ServyCoreDllName;

            // Assert
            Assert.Equal("Servy.Core", dllName);
        }

        [Fact]
        public void ServyServiceUIExe_ShouldEndWithExe()
        {
            // Arrange (Static property validation context)

            // Act
            var exeName = AppConfig.ServyServiceUIExe;

            // Assert
            Assert.EndsWith(".exe", exeName);
        }

        [Fact]
        public void ServyServiceCLIExe_ShouldEndWithExe()
        {
            // Arrange (Static property validation context)

            // Act
            var exeName = AppConfig.ServyServiceCLIExe;

            // Assert
            Assert.EndsWith(".exe", exeName);
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
            // Arrange (Static execution context)

            // Act
            var path = AppConfig.GetServyCLIServicePath();

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(Path.IsPathRooted(path), $"Expected an absolute path but got: {path}");
            Assert.EndsWith(AppConfig.ServyServiceCLIExe, path);
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
            Assert.EndsWith(AppConfig.ServyServiceUIExe, path);
        }

        [Fact]
        public void ProgramDataPath_ShouldContainAppFolderName()
        {
            // Arrange (Static property validation context)

            // Act
            var path = AppConfig.ProgramDataPath;

            // Assert
            Assert.Contains(AppConfig.AppFolderName, path);
        }

        [Fact]
        public void SecurityFolderPath_ShouldBeSubfolderOfProgramDataPath()
        {
            // Arrange (Static property validation context)

            // Act
            var path = AppConfig.SecurityFolderPath;

            // Assert
            Assert.StartsWith(AppConfig.ProgramDataPath, path);
        }
    }
}
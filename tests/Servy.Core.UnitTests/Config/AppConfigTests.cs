using Servy.Core.Config;
using Servy.Testing;
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
            // Act & Assert
            // Strictly less, not <=: at equal values the cooperative CancellationTokenSource and the
            // HttpClient transport timeout fire at the same instant and which one wins is
            // scheduler-dependent, which is the generic connection error the remarks on
            // UpdateCheckTimeoutSeconds say this invariant exists to prevent.
            Assert.True(AppConfig.UpdateCheckTimeoutSeconds < AppConfig.UpdateCheckHttpTimeoutSeconds,
                "Cooperative cancellation must fire strictly before the HTTP client timeout.");
        }

        [Fact]
        public void SkipSplashArgument_IsAValueBoolTryParseReadsAsFalse()
        {
            // Act & Assert
            // The consumer (AppBootstrapper.OnStartup) parses the positional argument with
            // bool.TryParse and never reads this constant, so producer and consumer are bound by
            // string value only and nothing but this assertion notices a change to "0" or "no".
            Assert.True(bool.TryParse(AppConfig.SkipSplashArgument, out var skipSplash) && !skipSplash,
                $"SkipSplashArgument must be a string bool.TryParse accepts as false; got '{AppConfig.SkipSplashArgument}'.");
        }

        [Fact]
        public void ChildSleepSeconds_OutlastsChildTimeoutSeconds()
        {
            // Act & Assert
            Assert.True(TestTimeouts.ChildSleepSeconds >= TestTimeouts.ChildTimeoutSeconds,
                "The spawned leaf process must still be alive when the enumeration budget expires.");
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
            Assert.Equal("Servy.Service.Net48.exe", exeName);
        }

        [Fact]
        public void ServyServiceCLIExe_ShouldBeCorrect()
        {
            // Act
            var exeName = AppConfig.ServyServiceCLIExe;

            // Assert
            Assert.Equal("Servy.Service.CLI.Net48.exe", exeName);
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
            Assert.EndsWith(AppConfig.HandleExe, path);
        }

        [Fact]
        public void GetServyCLIServicePath_ShouldReturnFullPath()
        {
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

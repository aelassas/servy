using Servy.Core.Config;
using Xunit;

namespace Servy.Core.UnitTests.Config
{
    public class AppConfigTests
    {
        [Fact]
        public void Version_ShouldNotBeNullOrEmpty()
        {
            Assert.False(string.IsNullOrWhiteSpace(AppConfig.Version));
        }

        [Fact]
        public void ServyServiceUIExe_ShouldEndWithExe()
        {
            Assert.EndsWith(".exe", AppConfig.ServyServiceUIExe);
        }

        [Fact]
        public void DefaultConnectionString_ShouldContainDbFolderPath()
        {
            Assert.Contains(AppConfig.DbFolderPath, AppConfig.DefaultConnectionString);
            Assert.Contains("Servy.db", AppConfig.DefaultConnectionString);
        }

        [Fact]
        public void DefaultAESKeyPath_ShouldEndWithAesKeyDat()
        {
            Assert.EndsWith("aes_key.dat", AppConfig.DefaultAESKeyPath);
        }

        [Fact]
        public void DefaultAESIVPath_ShouldEndWithAesIvDat()
        {
            Assert.EndsWith("aes_iv.dat", AppConfig.DefaultAESIVPath);
        }

        [Fact]
        public void GetHandleExePath_ShouldReturnFullPath()
        {
            var path = AppConfig.GetHandleExePath();
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.EndsWith(AppConfig.HandleExe, path);
        }

        [Fact]
        public void GetServyCLIServicePath_ShouldReturnFullPath()
        {
            var path = AppConfig.GetServyCLIServicePath();
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.EndsWith(AppConfig.ServyServiceCLIExe, path);
        }

        [Fact]
        public void GetServyUIServicePath_ShouldReturnFullPath()
        {
            var path = AppConfig.GetServyUIServicePath();
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.EndsWith(AppConfig.ServyServiceUIExe, path);
        }

        [Fact]
        public void ProgramDataPath_ShouldContainAppFolderName()
        {
            Assert.Contains(AppConfig.AppFolderName, AppConfig.ProgramDataPath);
        }

        [Fact]
        public void SecurityFolderPath_ShouldBeSubfolderOfProgramDataPath()
        {
            Assert.StartsWith(AppConfig.ProgramDataPath, AppConfig.SecurityFolderPath);
        }
    }
}

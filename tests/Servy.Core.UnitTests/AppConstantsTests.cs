using System;
using System.IO;
using Xunit;

namespace Servy.Core.UnitTests
{
    public class AppConstantsTests
    {
        [Fact]
        public void AppFolderName_ShouldBeServy()
        {
            Assert.Equal("Servy", AppConstants.AppFolderName);
        }

        [Fact]
        public void ProgramDataPath_ShouldBeUnderCommonApplicationData()
        {
            var expectedBase = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var expected = Path.Combine(expectedBase, AppConstants.AppFolderName);

            Assert.Equal(expected, AppConstants.ProgramDataPath);
            Assert.StartsWith(expectedBase, AppConstants.ProgramDataPath, StringComparison.OrdinalIgnoreCase);  
        }

        [Fact]
        public void DbFolderPath_ShouldBeUnderProgramDataPath()
        {
            var expected = Path.Combine(AppConstants.ProgramDataPath, "db");
            Assert.Equal(expected, AppConstants.DbFolderPath);
        }

        [Fact]
        public void SecurityFolderPath_ShouldBeUnderProgramDataPath()
        {
            var expected = Path.Combine(AppConstants.ProgramDataPath, "security");
            Assert.Equal(expected, AppConstants.SecurityFolderPath);
        }

        [Fact]
        public void DefaultConnectionString_ShouldPointToServyDbInDbFolder()
        {
            var expectedDbPath = Path.Combine(AppConstants.DbFolderPath, "Servy.db");
            var expectedConnectionString = $@"Data Source={expectedDbPath};";
            Assert.Equal(expectedConnectionString, AppConstants.DefaultConnectionString);
        }

        [Fact]
        public void DefaultAESKeyPath_ShouldPointToAesKeyFile()
        {
            var expected = Path.Combine(AppConstants.SecurityFolderPath, "aes_key.dat");
            Assert.Equal(expected, AppConstants.DefaultAESKeyPath);
        }

        [Fact]
        public void DefaultAESIVPath_ShouldPointToAesIVFile()
        {
            var expected = Path.Combine(AppConstants.SecurityFolderPath, "aes_iv.dat");
            Assert.Equal(expected, AppConstants.DefaultAESIVPath);
        }
    }
}

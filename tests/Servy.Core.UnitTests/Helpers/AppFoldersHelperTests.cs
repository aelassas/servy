using Servy.Core.Helpers;
using System;
using System.IO;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class AppFoldersHelperTests : IDisposable
    {
        private readonly string _tempDir;

        public AppFoldersHelperTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Theory]
        [InlineData(null, "key.aes", "iv.aes")]
        [InlineData("Data Source=db.db;", null, "iv.aes")]
        [InlineData("Data Source=db.db;", "key.aes", null)]
        [InlineData("", "key.aes", "iv.aes")]
        [InlineData("Data Source=db.db;", "", "iv.aes")]
        [InlineData("Data Source=db.db;", "key.aes", "")]
        public void EnsureFolders_NullOrWhitespaceArgs_Throws(string conn, string key, string iv)
        {
            Assert.Throws<ArgumentNullException>(() => AppFoldersHelper.EnsureFolders(conn, key, iv));
        }

        [Fact]
        public void EnsureFolders_ConnectionStringMissingDataSource_ThrowsInvalidOperationException()
        {
            string conn = "Server=myserver;Database=mydb;";
            string key = Path.Combine(_tempDir, "key.aes");
            string iv = Path.Combine(_tempDir, "iv.aes");

            var ex = Assert.Throws<InvalidOperationException>(() => AppFoldersHelper.EnsureFolders(conn, key, iv));
            Assert.Contains("Data Source=", ex.Message);
        }

        [Fact]
        public void EnsureFolders_InvalidDbFilePath_ThrowsInvalidOperationExceptionOrArgumentException()
        {
            string conn = "Data Source=:db:"; // invalid path will fail Path.GetDirectoryName
            string key = Path.Combine(_tempDir, "key.aes");
            string iv = Path.Combine(_tempDir, "iv.aes");

            Assert.ThrowsAny<Exception>(() => AppFoldersHelper.EnsureFolders(conn, key, iv));
        }

        [Fact]
        public void EnsureFolders_ValidPaths_CreatesAllFolders()
        {
            string dbFolder = Path.Combine(_tempDir, "db");
            string keyFolder = Path.Combine(_tempDir, "keys");
            string ivFolder = Path.Combine(_tempDir, "iv");

            string conn = $"Data Source={Path.Combine(dbFolder, "Servy.db")};";
            string key = Path.Combine(keyFolder, "key.aes");
            string iv = Path.Combine(ivFolder, "iv.aes");

            // Ensure folders do not exist, but keep _tempDir intact
            if (Directory.Exists(dbFolder)) Directory.Delete(dbFolder, true);
            if (Directory.Exists(keyFolder)) Directory.Delete(keyFolder, true);
            if (Directory.Exists(ivFolder)) Directory.Delete(ivFolder, true);

            // Call the helper
            AppFoldersHelper.EnsureFolders(conn, key, iv);

            Assert.True(Directory.Exists(dbFolder));
            Assert.True(Directory.Exists(keyFolder));
            Assert.True(Directory.Exists(ivFolder));
        }

        [Fact]
        public void EnsureFolders_DbFilePath_NoDirectory_ThrowsInvalidOperationException()
        {
            // dbFilePath is just a file name, no directory
            string conn = "Data Source=Servy.db;";
            string key = Path.Combine(_tempDir, "key.aes");
            string iv = Path.Combine(_tempDir, "iv.aes");

            var ex = Assert.Throws<InvalidOperationException>(() =>
                AppFoldersHelper.EnsureFolders(conn, key, iv));

            Assert.Equal("Cannot determine database folder path.", ex.Message);
        }

        [Fact]
        public void EnsureFolders_AESKeyPath_NoDirectory_ThrowsInvalidOperationException()
        {
            string conn = $"Data Source={Path.Combine(_tempDir, "db", "Servy.db")};";
            string key = "key.aes"; // no folder -> Path.GetDirectoryName returns null
            string iv = Path.Combine(_tempDir, "iv", "iv.aes");

            var ex = Assert.Throws<InvalidOperationException>(() =>
                AppFoldersHelper.EnsureFolders(conn, key, iv));

            Assert.Equal("Cannot determine AES key folder path.", ex.Message);
        }

        [Fact]
        public void EnsureFolders_AESIVPath_NoDirectory_ThrowsInvalidOperationException()
        {
            string conn = $"Data Source={Path.Combine(_tempDir, "db", "Servy.db")};";
            string key = Path.Combine(_tempDir, "key", "key.aes");
            string iv = "iv.aes"; // no folder -> Path.GetDirectoryName returns null

            var ex = Assert.Throws<InvalidOperationException>(() =>
                AppFoldersHelper.EnsureFolders(conn, key, iv));

            Assert.Equal("Cannot determine AES IV folder path.", ex.Message);
        }


    }
}

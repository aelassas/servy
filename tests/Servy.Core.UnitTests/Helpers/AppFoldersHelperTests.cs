using Servy.Core.Helpers;
using System;
using System.IO;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class AppFoldersHelperTests : IDisposable
    {
        private readonly string _tempDir;
        private const string TempToken = "{tmp}";

        public AppFoldersHelperTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            // Clean up temporary files after each test
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
            catch { /* Prevent teardown exceptions from hiding test results */ }
        }

        [Theory]
        [InlineData(null, "key.aes", "iv.aes")]
        [InlineData("Data Source=db.db;", null, "iv.aes")]
        [InlineData("Data Source=db.db;", "key.aes", null)]
        [InlineData("", "key.aes", "iv.aes")]
        [InlineData("Data Source=db.db;", "", "iv.aes")]
        [InlineData("Data Source=db.db;", "key.aes", "")]
        [InlineData("   ", "key.aes", "iv.aes")]
        [InlineData("Data Source=db.db;", "   ", "iv.aes")]
        [InlineData("Data Source=db.db;", "key.aes", "   ")]
        public void EnsureFolders_NullOrWhitespaceArgs_Throws(string conn, string key, string iv)
        {
            Assert.Throws<ArgumentException>(() => AppFoldersHelper.EnsureFolders(conn, key, iv));
        }

        [Fact]
        public void EnsureFolders_ConnectionStringMissingDataSource_ThrowsInvalidOperationException()
        {
            var conn = "Server=myserver;Database=mydb;";
            var key = Path.Combine(_tempDir, "key.aes");
            var iv = Path.Combine(_tempDir, "iv.aes");

            var ex = Assert.Throws<InvalidOperationException>(() => AppFoldersHelper.EnsureFolders(conn, key, iv));
            Assert.Contains("Data Source", ex.Message);
        }

        [Fact]
        public void EnsureFolders_InvalidDbFilePath_ThrowsInvalidOperationException()
        {
            var conn = "Data Source=:db:"; // invalid path will fail Path.GetDirectoryName
            var key = Path.Combine(_tempDir, "key.aes");
            var iv = Path.Combine(_tempDir, "iv.aes");

            var ex = Assert.Throws<InvalidOperationException>(() => AppFoldersHelper.EnsureFolders(conn, key, iv));
            Assert.Equal("Cannot determine database folder path.", ex.Message);
        }

        [Fact]
        public void EnsureFolders_ValidPaths_CreatesAllFolders()
        {
            var dbFolder = Path.Combine(_tempDir, "db");
            var keyFolder = Path.Combine(_tempDir, "keys");
            var ivFolder = Path.Combine(_tempDir, "iv");

            var conn = $"Data Source={Path.Combine(dbFolder, "Servy.db")};";
            var key = Path.Combine(keyFolder, "key.aes");
            var iv = Path.Combine(ivFolder, "iv.aes");

            // Call the helper
            AppFoldersHelper.EnsureFolders(conn, key, iv);

            Assert.True(Directory.Exists(dbFolder));
            Assert.True(Directory.Exists(keyFolder));
            Assert.True(Directory.Exists(ivFolder));
        }

        [Theory]
        [InlineData("Data Source=Servy.db;", "{tmp}\\key.aes", "{tmp}\\iv.aes", "Cannot determine database folder path.")]
        [InlineData("Data Source={tmp}\\db\\Servy.db;", "key.aes", "{tmp}\\iv\\iv.aes", "Cannot determine AES key folder path.")]
        [InlineData("Data Source={tmp}\\db\\Servy.db;", "{tmp}\\key\\key.aes", "iv.aes", "Cannot determine AES IV folder path.")]
        public void EnsureFolders_PathWithoutDirectory_ThrowsInvalidOperationException(string conn, string key, string iv, string expectedMessage)
        {
            // Arrange
            string resolvedConn = conn.Replace(TempToken, _tempDir);
            string resolvedKey = key.Replace(TempToken, _tempDir);
            string resolvedIv = iv.Replace(TempToken, _tempDir);

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                AppFoldersHelper.EnsureFolders(resolvedConn, resolvedKey, resolvedIv));

            // Assert
            Assert.Equal(expectedMessage, ex.Message);
        }
    }
}
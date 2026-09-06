using Servy.Core.Helpers;

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

        #region EnsureFolders Tests

        [Theory]
        [InlineData(null, "key.aes", "iv.aes", null)]
        [InlineData("Data Source=db.db;", null, "iv.aes", null)]
        [InlineData("Data Source=db.db;", "key.aes", null, null)]
        [InlineData("", "key.aes", "iv.aes", null)]
        [InlineData("Data Source=db.db;", "", "iv.aes", null)]
        [InlineData("Data Source=db.db;", "key.aes", "", null)]
        [InlineData("    ", "key.aes", "iv.aes", null)]
        [InlineData("Data Source=db.db;", "    ", "iv.aes", null)]
        [InlineData("Data Source=db.db;", "key.aes", "    ", null)]
        [InlineData("Data Source=db.db;", "key.aes", "iv.aes", "")]
        [InlineData("Data Source=db.db;", "key.aes", "iv.aes", "    ")]
        public void EnsureFolders_NullOrWhitespaceArgs_Throws(string? conn, string? key, string? iv, string? rootVaultPath)
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => AppFoldersHelper.EnsureFolders(conn!, key!, iv!, rootVaultPath));
        }

        [Fact]
        public void EnsureFolders_MalformedConnectionString_ThrowsInvalidOperationException()
        {
            // Arrange
            var conn = "==;;";
            var key = Path.Combine(_tempDir, "key.aes");
            var iv = Path.Combine(_tempDir, "iv.aes");

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => AppFoldersHelper.EnsureFolders(conn, key, iv, rootVaultPath: _tempDir));

            // Assert
            Assert.Equal("Connection string format is invalid.", ex.Message);
            Assert.IsType<ArgumentException>(ex.InnerException);
        }

        [Fact]
        public void EnsureFolders_ConnectionStringMissingDataSource_ThrowsInvalidOperationException()
        {
            // Arrange
            var conn = "Server=myserver;Database=mydb;";
            var key = Path.Combine(_tempDir, "key.aes");
            var iv = Path.Combine(_tempDir, "iv.aes");

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => AppFoldersHelper.EnsureFolders(conn, key, iv, rootVaultPath: _tempDir));

            // Assert
            Assert.Contains("Data Source", ex.Message);
        }

        [Fact]
        public void EnsureFolders_EmptyDataSourceValue_ThrowsInvalidOperationException()
        {
            // Arrange
            // A quoted blank value is kept by DbConnectionStringBuilder; an unquoted empty
            // one is dropped, which lands on the missing-key guard instead (covered above).
            var conn = "Data Source=\"   \";";
            var key = Path.Combine(_tempDir, "key.aes");
            var iv = Path.Combine(_tempDir, "iv.aes");

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => AppFoldersHelper.EnsureFolders(conn, key, iv, rootVaultPath: _tempDir));

            // Assert
            Assert.Equal("The database path provided in the connection string is empty.", ex.Message);
        }

        [Fact]
        public void EnsureFolders_DataSourceKeySpelling_Succeeds()
        {
            // Arrange
            var dbFolder = Path.Combine(_tempDir, "db");
            var keyFolder = Path.Combine(_tempDir, "keys");
            var ivFolder = Path.Combine(_tempDir, "iv");

            var conn = $"DataSource={Path.Combine(dbFolder, "Servy.db")};";
            var key = Path.Combine(keyFolder, "key.aes");
            var iv = Path.Combine(ivFolder, "iv.aes");

            // Act
            AppFoldersHelper.EnsureFolders(conn, key, iv, rootVaultPath: _tempDir);

            // Assert
            Assert.True(Directory.Exists(dbFolder));
            Assert.True(Directory.Exists(keyFolder));
            Assert.True(Directory.Exists(ivFolder));
        }

        [Fact]
        public void EnsureFolders_ValidPaths_CreatesAllFoldersUnderCustomRoot()
        {
            // Arrange
            var dbFolder = Path.Combine(_tempDir, "db");
            var keyFolder = Path.Combine(_tempDir, "keys");
            var ivFolder = Path.Combine(_tempDir, "iv");

            var conn = $"Data Source={Path.Combine(dbFolder, "Servy.db")};";
            var key = Path.Combine(keyFolder, "key.aes");
            var iv = Path.Combine(ivFolder, "iv.aes");

            // Act: Supply custom rootVaultPath so tests execute deterministically without touching system C:\ProgramData\Servy
            AppFoldersHelper.EnsureFolders(conn, key, iv, rootVaultPath: _tempDir);

            // Assert: Verify all operational directories exist under the scoped test root
            Assert.True(Directory.Exists(dbFolder));
            Assert.True(Directory.Exists(keyFolder));
            Assert.True(Directory.Exists(ivFolder));
            Assert.True(Directory.Exists(Path.Combine(_tempDir, "recovery")));
            Assert.True(Directory.Exists(Path.Combine(_tempDir, "logs")));

            // Assert: Folders nested inside the root vault maintain inheritance
            var dbSecurity = new DirectoryInfo(dbFolder).GetAccessControl();
            Assert.False(dbSecurity.AreAccessRulesProtected); // child of root -> inheritance preserved
        }

        [Fact]
        public void EnsureFolders_ExternalFolder_BreaksInheritance()
        {
            // Arrange: Place root vault and database folder in separate root directories
            var externalTempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                var dbFolder = Path.Combine(externalTempDir, "external_db");
                var keyFolder = Path.Combine(_tempDir, "keys");
                var ivFolder = Path.Combine(_tempDir, "iv");

                var conn = $"Data Source={Path.Combine(dbFolder, "Servy.db")};";
                var key = Path.Combine(keyFolder, "key.aes");
                var iv = Path.Combine(ivFolder, "iv.aes");

                // Act
                AppFoldersHelper.EnsureFolders(conn, key, iv, rootVaultPath: _tempDir);

                // Assert: External folder must break inheritance as its own security root
                Assert.True(Directory.Exists(dbFolder));
                var dbSecurity = new DirectoryInfo(dbFolder).GetAccessControl();
                Assert.True(dbSecurity.AreAccessRulesProtected); // external folder -> inheritance broken
            }
            finally
            {
                try { if (Directory.Exists(externalTempDir)) Directory.Delete(externalTempDir, true); }
                catch { /* Prevent teardown exceptions */ }
            }
        }

        [Theory]
        [InlineData("Data Source=Servy.db;", "{tmp}\\key.aes", "{tmp}\\iv.aes", "Cannot determine database folder path.")]
        [InlineData("Data Source=:db:;", "{tmp}\\key.aes", "{tmp}\\iv.aes", "Cannot determine database folder path.")]
        public void EnsureFolders_PathWithoutDirectory_ThrowsInvalidOperationException(string conn, string key, string iv, string expectedMessage)
        {
            // Arrange
            string resolvedConn = conn.Replace(TempToken, _tempDir);
            string resolvedKey = key.Replace(TempToken, _tempDir);
            string resolvedIv = iv.Replace(TempToken, _tempDir);

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() =>
                AppFoldersHelper.EnsureFolders(resolvedConn, resolvedKey, resolvedIv, rootVaultPath: _tempDir));

            // Assert
            Assert.Equal(expectedMessage, ex.Message);
        }

        [Theory]
        [InlineData("Data Source={tmp}\\db\\Servy.db;", "key.aes", "{tmp}\\iv\\iv.aes", "aesKeyFilePath must be an absolute path")]
        [InlineData("Data Source={tmp}\\db\\Servy.db;", "{tmp}\\key\\key.aes", "iv.aes", "aesIVFilePath must be an absolute path")]
        [InlineData("Data Source=..\\Servy.db;", "{tmp}\\key.aes", "{tmp}\\iv.aes", "dbFolder must be an absolute path")]
        public void EnsureFolders_PathNotRooted_ThrowsArgumentException(string conn, string key, string iv, string expectedMessage)
        {
            // Arrange
            string resolvedConn = conn.Replace(TempToken, _tempDir);
            string resolvedKey = key.Replace(TempToken, _tempDir);
            string resolvedIv = iv.Replace(TempToken, _tempDir);

            // Act
            var ex = Assert.Throws<ArgumentException>(() =>
                AppFoldersHelper.EnsureFolders(resolvedConn, resolvedKey, resolvedIv, rootVaultPath: _tempDir));

            // Assert
            Assert.Contains(expectedMessage, ex.Message);
        }

        #endregion

        #region GetAppDirectory Tests

        [Fact]
        public void GetAppDirectory_ReturnsDirectoryContainingProcessOrBaseDir()
        {
            // Arrange: the SUT prefers the process path and falls back to the base directory
            var expected = !string.IsNullOrEmpty(Environment.ProcessPath)
                ? Path.GetDirectoryName(Environment.ProcessPath)!
                : AppContext.BaseDirectory;

            // Act
            var appDir = AppFoldersHelper.GetAppDirectory();

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(appDir));
            Assert.True(Directory.Exists(appDir));

            // AppContext.BaseDirectory carries a trailing separator and Path.GetDirectoryName does not
            Assert.Equal(
                expected.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                appDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        #endregion
    }
}

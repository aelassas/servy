using Servy.Core.Helpers;

namespace Servy.Core.UnitTests
{
    public class AppFoldersHelperTests : IDisposable
    {
        private readonly string _tempDir;

        public AppFoldersHelperTests()
        {
            // Create a temporary root folder for tests
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        [Fact]
        public void EnsureFolders_NullConnectionString_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                AppFoldersHelper.EnsureFolders(null!, "key.txt", "iv.txt"));
        }

        [Fact]
        public void EnsureFolders_WhitespaceConnectionString_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                AppFoldersHelper.EnsureFolders("   ", "key.txt", "iv.txt"));
        }

        [Fact]
        public void EnsureFolders_NullKeyFile_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                AppFoldersHelper.EnsureFolders("Data Source=db.db;", null!, "iv.txt"));
        }

        [Fact]
        public void EnsureFolders_NullIVFile_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                AppFoldersHelper.EnsureFolders("Data Source=db.db;", "key.txt", null!));
        }

        [Fact]
        public void EnsureFolders_ConnectionStringWithoutDataSource_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                AppFoldersHelper.EnsureFolders("Server=myserver;Database=mydb;", "key.txt", "iv.txt"));
            Assert.Contains("Data Source=", ex.Message);
        }

        [Fact]
        public void EnsureFolders_CreatesAllFolders()
        {
            // Arrange
            var dbFolder = Path.Combine(_tempDir, "db");
            var dbFile = Path.Combine(dbFolder, "Servy.db");
            var keyFolder = Path.Combine(_tempDir, "aes");
            var keyFile = Path.Combine(keyFolder, "key.bin");
            var ivFolder = Path.Combine(_tempDir, "aes");
            var ivFile = Path.Combine(ivFolder, "iv.bin");

            var connectionString = $"Data Source={dbFile};";

            // Act
            AppFoldersHelper.EnsureFolders(connectionString, keyFile, ivFile);

            // Assert
            Assert.True(Directory.Exists(dbFolder));
            Assert.True(Directory.Exists(keyFolder));
            Assert.True(Directory.Exists(ivFolder));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // ignore cleanup exceptions
            }
        }
    }
}

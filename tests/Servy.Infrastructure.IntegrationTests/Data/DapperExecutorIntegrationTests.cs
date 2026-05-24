using Dapper;
using Moq;
using Servy.Core.Data;
using Servy.Infrastructure.Data;
using System.Data.SQLite;

namespace Servy.Infrastructure.IntegrationTests.Data
{
    public class DapperExecutorIntegrationTests : IDisposable
    {
        private readonly Mock<IAppDbContext> _mockDbContext;
        private readonly DapperExecutor _executor;
        private readonly string _tempDbPath;
        private readonly string _connectionString;

        public DapperExecutorIntegrationTests()
        {
            // 1. Create a unique temporary database file for this specific test run
            // This allows the schema to persist cleanly across multiple Open/Close cycles 
            // from the statless executor without colliding with parallel xUnit tests.
            _tempDbPath = Path.GetTempFileName();
            _connectionString = $"Data Source={_tempDbPath};Version=3;";

            _mockDbContext = new Mock<IAppDbContext>();

            // 2. Initialize a baseline schema for data query execution validation
            using (var initConn = new SQLiteConnection(_connectionString))
            {
                initConn.Open();
                initConn.Execute(@"
                    CREATE TABLE TestServices (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ServiceName TEXT NOT NULL,
                        Status INTEGER NOT NULL
                    );
                    INSERT INTO TestServices (ServiceName, Status) VALUES ('ServyEngine', 1);
                    INSERT INTO TestServices (ServiceName, Status) VALUES ('ServyWatcher', 0);
                ");
            }

            // 3. Setup the context to yield fresh, CLOSED connections targeting the temp DB.
            // DapperExecutor is responsible for calling .Open() and .OpenAsync()
            _mockDbContext.Setup(db => db.CreateConnection()).Returns(() =>
            {
                return new SQLiteConnection(_connectionString);
            });

            _executor = new DapperExecutor(_mockDbContext.Object);
        }

        #region Base Integrity Checks

        [Fact]
        public void Constructor_NullDbContext_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new DapperExecutor(null!));
        }

        [Fact]
        public void SynchronousMethods_NullSql_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _executor.ExecuteScalar<int>(null!));
            Assert.Throws<ArgumentNullException>(() => _executor.Execute(null!));
            Assert.Throws<ArgumentNullException>(() => _executor.Query<dynamic>(null!));
            Assert.Throws<ArgumentNullException>(() => _executor.QuerySingleOrDefault<dynamic>(null!));
        }

        #endregion

        #region Synchronous Pipeline Integration Tests

        [Fact]
        public void Synchronous_ExecuteAndScalar_MutatesAndQueriesDatabaseState()
        {
            // Act: Perform a baseline modification pass
            int rowsAffected = _executor.Execute(
                "INSERT INTO TestServices (ServiceName, Status) VALUES (@Name, @Status);",
                new { Name = "ServyCLI", Status = 1 });

            long serviceCount = _executor.ExecuteScalar<long>("SELECT COUNT(*) FROM TestServices;");

            // Assert
            Assert.Equal(1, rowsAffected);
            Assert.Equal(3, serviceCount);
        }

        [Fact]
        public void Synchronous_QueryAndQuerySingle_RetrievesStronglyTypedCollections()
        {
            // Act
            var activeServices = _executor.Query<TestServiceDto>(
                "SELECT ServiceName, Status FROM TestServices WHERE Status = 1;").ToList();

            var specificService = _executor.QuerySingleOrDefault<TestServiceDto>(
                "SELECT ServiceName, Status FROM TestServices WHERE ServiceName = @Name;",
                new { Name = "ServyEngine" });

            // Assert
            Assert.Single(activeServices);
            Assert.Equal("ServyEngine", activeServices[0].ServiceName);
            Assert.NotNull(specificService);
            Assert.Equal(1, specificService.Status);
        }

        #endregion

        #region Asynchronous Pipeline Integration Tests

        [Fact]
        public async Task Asynchronous_ExecuteAndScalarAsync_MutatesStateAsynchronously()
        {
            // Act
            int rowsAffected = await _executor.ExecuteAsync(
                "UPDATE TestServices SET Status = 1 WHERE ServiceName = @Name;",
                new { Name = "ServyWatcher" },
                cancellationToken: CancellationToken.None);

            long activeCount = await _executor.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM TestServices WHERE Status = 1;",
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.Equal(1, rowsAffected);
            Assert.Equal(2, activeCount);
        }

        [Fact]
        public async Task Asynchronous_QueryAndFirstOrDefaultAsync_RetrievesRecords()
        {
            // Act
            var records = await _executor.QueryAsync<TestServiceDto>(
                "SELECT * FROM TestServices;",
                cancellationToken: CancellationToken.None);

            var match = await _executor.QueryFirstOrDefaultAsync<TestServiceDto>(
                "SELECT * FROM TestServices WHERE ServiceName = @Name;",
                new { Name = "NonExistentService" },
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.Equal(2, records.Count());
            Assert.Null(match);
        }

        [Fact]
        public async Task ExecuteAsync_WithCancelledToken_AbortsImmediately()
        {
            // Arrange
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel(); // Pre-trigger cancellation execution branch

                // Act & Assert
                await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                {
                    await _executor.ExecuteAsync("DELETE FROM TestServices;", cancellationToken: cts.Token);
                });
            }
        }

        #endregion

        #region Transaction Lifecycle Integration Tests

        [Fact]
        public void Transaction_CommitScope_PersistsChangesDurable()
        {
            // Act
            using (var tx = _executor.BeginTransaction())
            {
                _executor.Execute(
                    "INSERT INTO TestServices (ServiceName, Status) VALUES ('TxService', 1);",
                    transaction: tx);

                tx.Commit();
            }

            long totalCount = _executor.ExecuteScalar<long>("SELECT COUNT(*) FROM TestServices;");

            // Assert
            Assert.Equal(3, totalCount);
        }

        [Fact]
        public void Transaction_RollbackScope_RevertsChangesSafely()
        {
            // Act
            using (var tx = _executor.BeginTransaction())
            {
                _executor.Execute("DELETE FROM TestServices;", transaction: tx);
                // Exit scope without executing an explicit tx.Commit() mapping
                tx.Rollback();
            }

            long totalCount = _executor.ExecuteScalar<long>("SELECT COUNT(*) FROM TestServices;");

            // Assert
            Assert.Equal(2, totalCount); // Records remain untouched due to automatic cleanup rollback logic
        }

        #endregion

        #region Engine Internal Edge Cases (Reflection Helpers)

        [Theory]
        [InlineData("SELECT * FROM MyTable\r\nWHERE Id = 1", "SELECT * FROM MyTable  WHERE Id = 1")] // Match the double space left by \r\n replacement
        [InlineData("SELECT LongQueryStringThatExceedsTheStandardTruncationLimitForLogs", "SELECT LongQueryStringThatExceedsTheStandardTruncationL...")] // 55 chars + ellipses
        [InlineData("", "Unknown Query")]
        [InlineData(null, "Unknown Query")]
        public void FormatSqlForLog_Variants_EvaluatesCorrectly(string? inputSql, string expectedLoggedSql)
        {
            // Arrange
            var methodInfo = typeof(DapperExecutor).GetMethod("FormatSqlForLog",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            // Act
            var formatted = (string)methodInfo!.Invoke(null, new object[] { inputSql!, 55 })!;

            // Assert
            Assert.Equal(expectedLoggedSql, formatted);
        }

        [Fact]
        public void CalculateBackoff_HighAttemptCount_PreventsIntegerOverflows()
        {
            // Arrange
            var methodInfo = typeof(DapperExecutor).GetMethod("CalculateBackoff",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            // Act: Simulates attempt 40 (which would bit-shift overflow standard 32-bit integers)
            var calculatedDelay = (int)methodInfo!.Invoke(null, new object[] { 40, 100, 0, 30000 })!;

            // Assert
            Assert.Equal(30000, calculatedDelay);
        }

        #endregion

        public void Dispose()
        {
            // Clear SQLite pools so it releases any file locks, then delete the temp DB
            SQLiteConnection.ClearAllPools();

            if (File.Exists(_tempDbPath))
            {
                try { File.Delete(_tempDbPath); } catch { /* Ignore cleanup faults */ }
            }
        }

        // Shared tracking abstraction DTO targeting raw database queries directly
        private class TestServiceDto
        {
            public string ServiceName { get; set; } = string.Empty;
            public int Status { get; set; }
        }
    }
}
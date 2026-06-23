using Dapper;
using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Infrastructure.Data;
using System;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Infrastructure.IntegrationTests.Data
{
    [Collection("SequentialDatabaseTests")]
    public class DapperExecutorIntegrationTests : IDisposable
    {
        #region Nested Test Spies and Stubs

        // Concrete test spy subclassing DbConnection to bypass Moq's non-virtual limitation
        private class DisposeTrackingDbConnection : DbConnection
        {
            public bool WasDisposed { get; private set; }

            public override string ConnectionString
            {
                get { return "Data Source=:memory:;"; }
                set { /* no-op */ }
            }
            public override string Database { get { return "TestDb"; } }
            public override string DataSource { get { return "Memory"; } }
            public override string ServerVersion { get { return "1.0"; } }
            public override ConnectionState State { get { return ConnectionState.Closed; } }

            public override void Open() { throw new InvalidOperationException("Simulated Open Failure"); }
            public override void Close() { }

            protected override DbCommand CreateDbCommand() { throw new NotImplementedException(); }
            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) { throw new NotImplementedException(); }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    WasDisposed = true;
                }
                base.Dispose(disposing);
            }

            public override void ChangeDatabase(string databaseName) { /* no-op */ }
        }

        // Concrete test double tracking async faults, open attempt loops, and state disposals safely
        private class FaultyAsyncDbConnection : DbConnection
        {
            private readonly SQLiteErrorCode _errorCode;
            private readonly string _message;

            public int OpenAttempts { get; private set; }
            public bool WasDisposed { get; private set; }

            public override string ConnectionString
            {
                get { return "Data Source=:memory:;"; }
                set { /* no-op */ }
            }
            public override string Database { get { return "TestDb"; } }
            public override string DataSource { get { return "Memory"; } }
            public override string ServerVersion { get { return "1.0"; } }
            public override ConnectionState State { get { return ConnectionState.Closed; } }

            public FaultyAsyncDbConnection(SQLiteErrorCode errorCode, string message)
            {
                _errorCode = errorCode;
                _message = message;
            }

            public override void Open() { ThrowSQLiteException(); }

            public override Task OpenAsync(CancellationToken cancellationToken)
            {
                OpenAttempts++;
                ThrowSQLiteException();
                return Task.FromResult(0);
            }

            public override void Close() { }

            protected override DbCommand CreateDbCommand() { throw new NotImplementedException(); }
            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) { throw new NotImplementedException(); }

            private void ThrowSQLiteException()
            {
                throw new SQLiteException(_errorCode, _message);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    WasDisposed = true;
                }
                base.Dispose(disposing);
            }

            public override void ChangeDatabase(string databaseName) { /* no-op */ }
        }

        // DbConnection stub designed to gracefully simulate lack of asynchronous transaction support
        private class FlexibleDbConnectionStub : DbConnection
        {
            private readonly bool _forceSyncTransactionPath;

            public bool SyncTransactionWasCalled { get; private set; }

            public override string ConnectionString
            {
                get { return "Data Source=:memory:;"; }
                set { /* no-op */ }
            }
            public override string Database { get { return "TestDb"; } }
            public override string DataSource { get { return "Memory"; } }
            public override string ServerVersion { get { return "1.0"; } }
            public override ConnectionState State { get { return ConnectionState.Closed; } }

            public FlexibleDbConnectionStub(bool forceSyncTransactionPath = false)
            {
                _forceSyncTransactionPath = forceSyncTransactionPath;
            }

            public override void Open() { }
            public override Task OpenAsync(CancellationToken cancellationToken) { return Task.FromResult(0); }
            public override void Close() { }

            protected override DbCommand CreateDbCommand() { return new Mock<DbCommand>().Object; }

            // In net48 ADO.NET, async transaction methods utilize native Task models instead of ValueTask structures
            // Shifting implementation behaviors to throw or complete forces distinct code paths
            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            {
                SyncTransactionWasCalled = true;
                return new Mock<DbTransaction>().Object as DbTransaction;
            }

            public override void ChangeDatabase(string databaseName) { /* no-op */ }
        }

        #endregion

        private readonly Mock<IAppDbContext> _mockDbContext;
        private readonly DapperExecutor _executor;
        private readonly string _tempDbPath;
        private readonly string _connectionString;

        public DapperExecutorIntegrationTests()
        {
            // 1. Create a unique temporary database file for this specific test run
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
            Assert.Throws<ArgumentNullException>(() => new DapperExecutor(null));
        }

        [Fact]
        public void SynchronousMethods_NullSql_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _executor.ExecuteScalar<int>(null));
            Assert.Throws<ArgumentNullException>(() => _executor.Execute(null));
            Assert.Throws<ArgumentNullException>(() => _executor.Query<dynamic>(null));
            Assert.Throws<ArgumentNullException>(() => _executor.QuerySingleOrDefault<dynamic>(null));
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
                tx.Rollback();
            }

            long totalCount = _executor.ExecuteScalar<long>("SELECT COUNT(*) FROM TestServices;");

            // Assert
            Assert.Equal(2, totalCount);
        }

        #endregion

        #region Targeted Requirements Branch Coverage Tests

        [Fact]
        public void BeginTransaction_ExceptionOnOpen_DisposesConnectionAndThrows()
        {
            // Arrange
            var brokenConnectionSpy = new DisposeTrackingDbConnection();
            _mockDbContext.Setup(db => db.CreateConnection()).Returns(brokenConnectionSpy);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _executor.BeginTransaction());
            Assert.True(brokenConnectionSpy.WasDisposed, "The connection was not explicitly closed on an initialization error.");
        }

        [Fact]
        public void ExecuteWithRetry_DatabaseLockedExhausted_ThrowsSQLiteException()
        {
            var busyMockConn = new Mock<DbConnection>();
            busyMockConn.Setup(c => c.Open()).Callback(() =>
            {
                throw new SQLiteException(SQLiteErrorCode.Busy, "Database locked down.");
            });
            _mockDbContext.Setup(db => db.CreateConnection()).Returns(busyMockConn.Object);

            // Act & Assert
            Assert.Throws<SQLiteException>(() => _executor.ExecuteScalar<int>("SELECT COUNT(*) FROM TestServices;"));
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_DatabaseBusyExhausted_ThrowsSQLiteException()
        {
            // Arrange
            var busyConnectionSpy = new FaultyAsyncDbConnection(SQLiteErrorCode.Busy, "Async Busy Lock");
            _mockDbContext.Setup(db => db.CreateConnection()).Returns(busyConnectionSpy);

            // Act & Assert
            await Assert.ThrowsAsync<SQLiteException>(async () =>
            {
                await _executor.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM TestServices;", cancellationToken: CancellationToken.None);
            });

            Assert.Equal(AppConfig.DbAsyncMaxRetries, busyConnectionSpy.OpenAttempts);
        }

        [Fact]
        public void ExecuteScalar_WithActiveTransaction_UsesActiveTxConnection()
        {
            // Act
            using (var tx = _executor.BeginTransaction())
            {
                var result = _executor.ExecuteScalar<long>("SELECT COUNT(*) FROM TestServices;", transaction: tx);
                Assert.Equal(2, result);
            }
        }

        [Fact]
        public void Query_WithActiveTransaction_UsesActiveTxConnection()
        {
            // Act
            using (var tx = _executor.BeginTransaction())
            {
                var result = _executor.Query<TestServiceDto>("SELECT * FROM TestServices;", transaction: tx);
                Assert.Equal(2, result.Count());
            }
        }

        [Fact]
        public void QuerySingleOrDefault_WithActiveTransaction_UsesActiveTxConnection()
        {
            // Act
            using (var tx = _executor.BeginTransaction())
            {
                var result = _executor.QuerySingleOrDefault<TestServiceDto>(
                    "SELECT * FROM TestServices WHERE ServiceName = @Name;",
                    new { Name = "ServyEngine" },
                    transaction: tx);

                Assert.NotNull(result);
                Assert.Equal("ServyEngine", result.ServiceName);
            }
        }

        [Fact]
        public async Task ExecuteScalarAsync_WithActiveTransaction_UsesActiveTxConnection()
        {
            // Act
            using (var tx = _executor.BeginTransaction())
            {
                var result = await _executor.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM TestServices;", transaction: tx, cancellationToken: CancellationToken.None);
                Assert.Equal(2, result);
            }
        }

        [Fact]
        public async Task QueryAsync_WithActiveTransaction_UsesActiveTxConnection()
        {
            // Act
            using (var tx = _executor.BeginTransaction())
            {
                var result = await _executor.QueryAsync<TestServiceDto>("SELECT * FROM TestServices;", transaction: tx, cancellationToken: CancellationToken.None);
                Assert.Equal(2, result.Count());
            }
        }

        [Fact]
        public async Task QueryFirstOrDefaultAsync_WithActiveTransaction_UsesActiveTxConnection()
        {
            // Act
            using (var tx = _executor.BeginTransaction())
            {
                var result = await _executor.QueryFirstOrDefaultAsync<TestServiceDto>(
                    "SELECT * FROM TestServices WHERE ServiceName = @Name;",
                    new { Name = "ServyEngine" },
                    transaction: tx,
                    cancellationToken: CancellationToken.None);

                Assert.NotNull(result);
                Assert.Equal("ServyEngine", result.ServiceName);
            }
        }

        [Fact]
        public async Task QuerySingleOrDefaultAsync_AllBranchesAndVariants_Covered()
        {
            // 1. Valid record variant
            var singleMatch = await _executor.QuerySingleOrDefaultAsync<TestServiceDto>(
                "SELECT * FROM TestServices WHERE ServiceName = @Name;",
                new { Name = "ServyEngine" },
                cancellationToken: CancellationToken.None);

            Assert.NotNull(singleMatch);
            Assert.Equal("ServyEngine", singleMatch.ServiceName);

            // 2. Default/Empty records variant
            var noMatch = await _executor.QuerySingleOrDefaultAsync<TestServiceDto>(
                "SELECT * FROM TestServices WHERE ServiceName = @Name;",
                new { Name = "NonExistent" },
                cancellationToken: CancellationToken.None);

            Assert.Null(noMatch);

            // 3. CommandDefinition/Active Transaction mapping branch variant
            using (var tx = _executor.BeginTransaction())
            {
                var txMatch = await _executor.QuerySingleOrDefaultAsync<TestServiceDto>(
                    "SELECT * FROM TestServices WHERE ServiceName = @Name;",
                    new { Name = "ServyWatcher" },
                    transaction: tx,
                    cancellationToken: CancellationToken.None);

                Assert.NotNull(txMatch);
                Assert.Equal("ServyWatcher", txMatch.ServiceName);
            }

            // 4. Exception input parameters guard validation branch
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await _executor.QuerySingleOrDefaultAsync<TestServiceDto>(sql: null, cancellationToken: CancellationToken.None);
            });
        }

        #endregion

        #region Engine Internal Edge Cases (Reflection Helpers)

        [Theory]
        [InlineData("SELECT * FROM MyTable\r\nWHERE Id = 1", "SELECT * FROM MyTable  WHERE Id = 1")]
        [InlineData("SELECT LongQueryStringThatExceedsTheStandardTruncationLimitForLogs", "SELECT LongQueryStringThatExceedsTheStandardTruncationL...")]
        [InlineData("", "Unknown Query")]
        [InlineData(null, "Unknown Query")]
        public void FormatSqlForLog_Variants_EvaluatesCorrectly(string inputSql, string expectedLoggedSql)
        {
            // Arrange
            var methodInfo = typeof(DapperExecutor).GetMethod("FormatSqlForLog",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            // Act
            var formatted = (string)methodInfo.Invoke(null, new object[] { inputSql, 55 });

            // Assert
            Assert.Equal(expectedLoggedSql, formatted);
        }

        [Fact]
        public void CalculateBackoff_HighAttemptCount_PreventsIntegerOverflows()
        {
            // Arrange
            var methodInfo = typeof(DapperExecutor).GetMethod("CalculateBackoff",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            // Act: Simulates attempt 40
            var calculatedDelay = (int)methodInfo.Invoke(null, new object[] { 40, 100, 0, 30000 });

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
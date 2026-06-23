using Dapper;
using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Infrastructure.Data;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;

namespace Servy.Infrastructure.IntegrationTests.Data
{
    [Collection("SequentialDatabaseTests")]
    public class DapperExecutorIntegrationTests : IDisposable
    {
        // Concrete test spy subclassing DbConnection to bypass Moq's non-virtual limitation
        private class DisposeTrackingDbConnection : DbConnection
        {
            public bool WasDisposed { get; private set; }

            public override string ConnectionString { get; set; } = "Data Source=:memory:;";
            public override string Database => "TestDb";
            public override string DataSource => "Memory";
            public override string ServerVersion => "1.0";
            public override ConnectionState State => ConnectionState.Closed;

            public override void Open() => throw new InvalidOperationException("Simulated Open Failure");
            public override void Close() { }

            protected override DbCommand CreateDbCommand() => throw new NotImplementedException();
            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotImplementedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    WasDisposed = true;
                }
                base.Dispose(disposing);
            }

            public override void ChangeDatabase(string databaseName)
            {
                /* no-op */
            }
        }

        // Concrete test double tracking async faults, open attempt loops, and state disposals safely
        private class FaultyAsyncDbConnection : DbConnection
        {
            private readonly SQLiteErrorCode _errorCode;
            private readonly string _message;

            public int OpenAttempts { get; private set; }
            public bool WasDisposed { get; private set; }

            public override string ConnectionString { get; set; } = "Data Source=:memory:;";
            public override string Database => "TestDb";
            public override string DataSource => "Memory";
            public override string ServerVersion => "1.0";
            public override ConnectionState State => ConnectionState.Closed;

            public FaultyAsyncDbConnection(SQLiteErrorCode errorCode, string message)
            {
                _errorCode = errorCode;
                _message = message;
            }

            public override void Open() => ThrowSQLiteException();

            public override Task OpenAsync(CancellationToken cancellationToken)
            {
                OpenAttempts++;
                ThrowSQLiteException();
                return Task.CompletedTask;
            }

            public override void Close() { }

            protected override DbCommand CreateDbCommand() => throw new NotImplementedException();
            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotImplementedException();

            private void ThrowSQLiteException()
            {
                // Instantiates a true native SQLiteException mapping our targeted error codes accurately
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

            public override void ChangeDatabase(string databaseName)
            {
                /* no-op */
            }
        }

        // NESTED CLASS: Place inside your DapperExecutorIntegrationTests class block
        private class FlexibleDbConnectionStub : DbConnection
        {
            private readonly bool _forceSyncTransactionPath;

            public bool SyncTransactionWasCalled { get; private set; }

            public override string ConnectionString { get; set; } = "Data Source=:memory:;";
            public override string Database => "TestDb";
            public override string DataSource => "Memory";
            public override string ServerVersion => "1.0";
            public override ConnectionState State => ConnectionState.Closed;

            public FlexibleDbConnectionStub(bool forceSyncTransactionPath = false)
            {
                _forceSyncTransactionPath = forceSyncTransactionPath;
            }

            public override void Open() { }
            public override Task OpenAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public override void Close() { }

            protected override DbCommand CreateDbCommand() => new Mock<DbCommand>().Object;

            protected override ValueTask<DbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken)
            {
                if (_forceSyncTransactionPath)
                {
                    // FIX: Hand execution flow back to the base framework layer.
                    // The standard .NET base implementation routes directly down into 
                    // the synchronous BeginDbTransaction method below automatically.
                    return base.BeginDbTransactionAsync(isolationLevel, cancellationToken);
                }

                var mockTx = new Mock<DbTransaction>().Object;
                return new ValueTask<DbTransaction>(mockTx);
            }

            // Fallback synchronous intersection handler called naturally by the base ADO.NET routing layer
            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            {
                SyncTransactionWasCalled = true;
                return new Mock<DbTransaction>().Object as DbTransaction ?? throw new InvalidOperationException();
            }

            public override void ChangeDatabase(string databaseName)
            {
                /* no-op */
            }
        }

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
                tx.Rollback();
            }

            long totalCount = _executor.ExecuteScalar<long>("SELECT COUNT(*) FROM TestServices;");

            // Assert
            Assert.Equal(2, totalCount);
        }

        #endregion

        #region Targeted Requirements Coverage Tests

        [Fact]
        public void BeginTransaction_ExceptionOnOpen_DisposesConnectionAndThrows()
        {
            // Arrange
            var brokenConnectionSpy = new DisposeTrackingDbConnection();

            _mockDbContext.Setup(db => db.CreateConnection()).Returns(brokenConnectionSpy);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _executor.BeginTransaction());

            // Verify disposal was called via our tracking flag instead of Moq .Verify()
            Assert.True(brokenConnectionSpy.WasDisposed, "The connection was not explicitly disposed upon an Open() exception.");
        }

        [Fact]
        public async Task BeginTransactionAsync_ExceptionOnOpenAsync_DisposesConnectionAndThrows()
        {
            // Arrange
            var brokenConnectionSpy = new FaultyAsyncDbConnection(SQLiteErrorCode.Locked, "Database file structurally locked.");
            _mockDbContext.Setup(db => db.CreateConnection()).Returns(brokenConnectionSpy);

            // Act & Assert
            await Assert.ThrowsAsync<SQLiteException>(async () =>
                await _executor.BeginTransactionAsync(CancellationToken.None));

            // Assert that the resource was safely cleaned up following the asynchronous initialization crash
            Assert.True(brokenConnectionSpy.WasDisposed, "The connection was not explicitly disposed upon an OpenAsync() failure.");
        }

        [Fact]
        public async Task BeginTransactionAsync_AllBranchesCovered()
        {
            // Branch 1: Uses a DbConnection stub that explicitly forces the synchronous connection.BeginTransaction() fallback track
            var syncFallbackStub = new FlexibleDbConnectionStub(forceSyncTransactionPath: true);
            _mockDbContext.Setup(db => db.CreateConnection()).Returns(syncFallbackStub);

            using (var tx = await _executor.BeginTransactionAsync(CancellationToken.None))
            {
                Assert.NotNull(tx);
                Assert.True(syncFallbackStub.SyncTransactionWasCalled, "The synchronous .BeginTransaction() fallback path was not executed.");
            }

            // Branch 2: Uses the real SQLite connection object to test the high-performance async driver pipelines natively
            _mockDbContext.Setup(db => db.CreateConnection()).Returns(() => new SQLiteConnection(_connectionString));
            using (var tx2 = await _executor.BeginTransactionAsync(CancellationToken.None))
            {
                Assert.NotNull(tx2);
                Assert.NotNull(tx2.Connection);
                Assert.Equal(ConnectionState.Open, tx2.Connection.State);
            }
        }

        [Fact]
        public void ExecuteWithRetry_DatabaseLockedExhausted_ThrowsSQLiteException()
        {
            // Arrange
            int executedAttempts = 0;

            // Act & Assert
            Assert.Throws<SQLiteException>(() =>
            {
                _executor.ExecuteScalar<int>("SELECT COUNT(*) FROM TestServices;", param: null, transaction: null);

                // Forcing retry execution by mocking connection state to return standard SQLITE_BUSY error codes
                var busyMockConn = new Mock<DbConnection>();
                busyMockConn.Setup(c => c.Open()).Callback(() =>
                {
                    executedAttempts++;
                    throw new SQLiteException(SQLiteErrorCode.Busy, "Database is locked by parallel test thread pool execution.");
                });
                _mockDbContext.Setup(db => db.CreateConnection()).Returns(busyMockConn.Object);
                _executor.ExecuteScalar<int>("SELECT COUNT(*) FROM TestServices;");
            });
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_DatabaseBusyExhausted_ThrowsSQLiteException()
        {
            var busyConnectionSpy = new FaultyAsyncDbConnection(SQLiteErrorCode.Busy, "Database engine is concurrently busy.");
            _mockDbContext.Setup(db => db.CreateConnection()).Returns(busyConnectionSpy);

            // Act & Assert
            await Assert.ThrowsAsync<SQLiteException>(async () =>
            {
                await _executor.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM TestServices;", cancellationToken: CancellationToken.None);
            });

            // Verify the engine systematically retried across the configured loop allocation space
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
            using (var tx = await _executor.BeginTransactionAsync(TestContext.Current.CancellationToken))
            {
                var result = await _executor.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM TestServices;", transaction: tx, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(2, result);
            }
        }

        [Fact]
        public async Task QueryAsync_WithActiveTransaction_UsesActiveTxConnection()
        {
            // Act
            using (var tx = await _executor.BeginTransactionAsync(TestContext.Current.CancellationToken))
            {
                var result = await _executor.QueryAsync<TestServiceDto>("SELECT * FROM TestServices;", transaction: tx, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(2, result.Count());
            }
        }

        [Fact]
        public async Task QueryFirstOrDefaultAsync_WithActiveTransaction_UsesActiveTxConnection()
        {
            // Act
            using (var tx = await _executor.BeginTransactionAsync(TestContext.Current.CancellationToken))
            {
                var result = await _executor.QueryFirstOrDefaultAsync<TestServiceDto>(
                    "SELECT * FROM TestServices WHERE ServiceName = @Name;",
                    new { Name = "ServyEngine" },
                    transaction: tx,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.NotNull(result);
                Assert.Equal("ServyEngine", result.ServiceName);
            }
        }

        [Fact]
        public async Task QuerySingleOrDefaultAsync_AllBranchesAndVariants_Covered()
        {
            // 1. Query with result variant (Single entry found match)
            var singleMatch = await _executor.QuerySingleOrDefaultAsync<TestServiceDto>(
                "SELECT * FROM TestServices WHERE ServiceName = @Name;",
                new { Name = "ServyEngine" },
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(singleMatch);
            Assert.Equal("ServyEngine", singleMatch.ServiceName);

            // 2. Query empty variant (Zero records found returns default / null)
            var noMatch = await _executor.QuerySingleOrDefaultAsync<TestServiceDto>(
                "SELECT * FROM TestServices WHERE ServiceName = @Name;",
                new { Name = "NonExistent" },
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Null(noMatch);

            // 3. Query with active transaction branch verification path
            using (var tx = await _executor.BeginTransactionAsync(TestContext.Current.CancellationToken))
            {
                var txMatch = await _executor.QuerySingleOrDefaultAsync<TestServiceDto>(
                    "SELECT * FROM TestServices WHERE ServiceName = @Name;",
                    new { Name = "ServyWatcher" },
                    transaction: tx,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.NotNull(txMatch);
                Assert.Equal("ServyWatcher", txMatch.ServiceName);
            }

            // 4. Test validation ArgumentNullException path branch on null query string parameters
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await _executor.QuerySingleOrDefaultAsync<TestServiceDto>(sql: null!, cancellationToken: TestContext.Current.CancellationToken);
            });
        }

        #endregion

        #region Engine Internal Edge Cases (Reflection Helpers)

        [Theory]
        [InlineData("SELECT * FROM MyTable\r\nWHERE Id = 1", "SELECT * FROM MyTable  WHERE Id = 1")]
        [InlineData("SELECT LongQueryStringThatExceedsTheStandardTruncationLimitForLogs", "SELECT LongQueryStringThatExceedsTheStandardTruncationL...")]
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

            // Act: Simulates attempt 40
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
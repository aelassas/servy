using Dapper;
using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Infrastructure.Data;
using Servy.Testing;
using System;
using System.Collections.Generic;
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
    public class DapperExecutorIntegrationTests : TempDirectoryTestBase
    {
        #region Shared Test Doubles Base Infrastructure

        /// <summary>
        /// Base test double providing an inert <see cref="DbConnection"/> implementation so the concrete
        /// doubles below only have to express their failure behavior. Tracks disposal via <see cref="WasDisposed"/>.
        /// </summary>
        private abstract class TestDbConnectionBase : DbConnection
        {
            /// <summary>
            /// Gets the number of times this connection container instance has been disposed.
            /// Counted rather than flagged so a test can pin one release per acquisition on the
            /// retry paths, where the executor re-enters the whole action once per attempt.
            /// </summary>
            public int DisposeCount { get; private set; }

            /// <summary>
            /// Gets a value indicating whether this connection container instance has been disposed.
            /// </summary>
            public bool WasDisposed => DisposeCount > 0;

            /// <inheritdoc />
            public override string ConnectionString { get; set; } = "Data Source=:memory:;";

            /// <inheritdoc />
            public override string Database => "TestDb";

            /// <inheritdoc />
            public override string DataSource => "Memory";

            /// <inheritdoc />
            public override string ServerVersion => "1.0";

            /// <inheritdoc />
            public override ConnectionState State => ConnectionState.Closed;

            /// <inheritdoc />
            public override void Close() { }

            /// <inheritdoc />
            public override void ChangeDatabase(string databaseName)
            {
                /* no-op */
            }

            /// <inheritdoc />
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    DisposeCount++;
                }
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Concrete test spy subclassing our connection base to isolate and track synchronous open exceptions.
        /// </summary>
        private class DisposeTrackingDbConnection : TestDbConnectionBase
        {
            /// <inheritdoc />
            public override void Open() => throw new InvalidOperationException("Simulated Open Failure");

            /// <inheritdoc />
            protected override DbCommand CreateDbCommand() => throw new NotImplementedException();

            /// <inheritdoc />
            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotImplementedException();
        }

        /// <summary>
        /// Concrete test double tracking asynchronous faults, open attempt loops, and state disposals safely.
        /// </summary>
        private class FaultyAsyncDbConnection : TestDbConnectionBase
        {
            private readonly SQLiteErrorCode _errorCode;
            private readonly string _message;

            /// <summary>
            /// Gets the total number of connection open operations attempted against this instance.
            /// </summary>
            public int OpenAttempts { get; private set; }

            public FaultyAsyncDbConnection(SQLiteErrorCode errorCode, string message)
            {
                _errorCode = errorCode;
                _message = message;
            }

            /// <inheritdoc />
            public override void Open()
            {
                OpenAttempts++;
                ThrowSQLiteException();
            }

            /// <inheritdoc />
            public override Task OpenAsync(CancellationToken cancellationToken)
            {
                OpenAttempts++;
                ThrowSQLiteException();
                return Task.CompletedTask;
            }

            /// <inheritdoc />
            protected override DbCommand CreateDbCommand() => throw new NotImplementedException();

            /// <inheritdoc />
            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotImplementedException();

            private void ThrowSQLiteException()
            {
                // Instantiates a true native SQLiteException mapping our targeted error codes accurately
                throw new SQLiteException(_errorCode, _message);
            }
        }

        /// <summary>
        /// Concrete test double simulating transient SQLite faults for a configured number of initial attempts
        /// before delegating to a real <see cref="SQLiteConnection"/> to test retry recovery loops.
        /// </summary>
        private class TransientFailureDbConnection : TestDbConnectionBase
        {
            private readonly SQLiteErrorCode _errorCode;
            private readonly int _failuresBeforeSuccess;
            private readonly string _realConnectionString;
            private SQLiteConnection _realConnection;

            /// <summary>
            /// Gets the total number of connection open operations attempted against this instance.
            /// </summary>
            public int OpenAttempts { get; private set; }

            public TransientFailureDbConnection(SQLiteErrorCode errorCode, int failuresBeforeSuccess, string realConnectionString)
            {
                _errorCode = errorCode;
                _failuresBeforeSuccess = failuresBeforeSuccess;
                _realConnectionString = realConnectionString;
            }

            /// <inheritdoc />
            public override void Open()
            {
                OpenAttempts++;
                if (OpenAttempts <= _failuresBeforeSuccess)
                {
                    throw new SQLiteException(_errorCode, "Transient SQLite error.");
                }

                _realConnection = new SQLiteConnection(_realConnectionString);
                _realConnection.Open();
            }

            /// <inheritdoc />
            public override async Task OpenAsync(CancellationToken cancellationToken)
            {
                OpenAttempts++;
                if (OpenAttempts <= _failuresBeforeSuccess)
                {
                    throw new SQLiteException(_errorCode, "Transient SQLite error.");
                }

                _realConnection = new SQLiteConnection(_realConnectionString);
                await _realConnection.OpenAsync(cancellationToken);
            }

            /// <inheritdoc />
            public override ConnectionState State => _realConnection?.State ?? ConnectionState.Closed;

            /// <inheritdoc />
            protected override DbCommand CreateDbCommand() =>
                _realConnection?.CreateCommand() ?? throw new InvalidOperationException("Connection is not open.");

            /// <inheritdoc />
            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
                _realConnection?.BeginTransaction(isolationLevel) ?? throw new InvalidOperationException("Connection is not open.");

            /// <inheritdoc />
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _realConnection?.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Concrete test stub configured to toggle execution flow routing behaviors for transaction pipeline evaluations.
        /// </summary>
        private class FlexibleDbConnectionStub : TestDbConnectionBase
        {
            private readonly bool _forceSyncTransactionPath;

            /// <summary>
            /// Gets a value indicating whether the fallback synchronous transaction initialization tracker path was traversed.
            /// </summary>
            public bool SyncTransactionWasCalled { get; private set; }

            public FlexibleDbConnectionStub(bool forceSyncTransactionPath = false)
            {
                _forceSyncTransactionPath = forceSyncTransactionPath;
            }

            /// <inheritdoc />
            public override void Open() { }

            /// <inheritdoc />
            public override Task OpenAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            /// <inheritdoc />
            protected override DbCommand CreateDbCommand() => new Mock<DbCommand>().Object;

            /// <inheritdoc />
            // In net48 ADO.NET, async transaction methods utilize native Task models instead of ValueTask structures
            // Shifting implementation behaviors to throw or complete forces distinct code paths
            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            {
                SyncTransactionWasCalled = true;
                return new Mock<DbTransaction>().Object;
            }
        }

        #endregion

        private readonly Mock<IAppDbContext> _mockDbContext;
        private readonly DapperExecutor _executor;
        private readonly string _tempDbPath;
        private readonly string _connectionString;

        public DapperExecutorIntegrationTests()
        {
            // 1. Create a unique temporary database file for this specific test run
            _tempDbPath = Path.Combine(TempDirectory, "dapper-tests.db");
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
        public async Task AsynchronousMethods_NullSql_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _executor.ExecuteAsync(null, cancellationToken: CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _executor.ExecuteScalarAsync<int>(null, cancellationToken: CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _executor.QueryAsync<dynamic>(null, cancellationToken: CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentNullException>(() => _executor.QueryFirstOrDefaultAsync<dynamic>(null, cancellationToken: CancellationToken.None));
        }

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

                // The cancelled call must not have mutated state
                long remaining = _executor.ExecuteScalar<long>("SELECT COUNT(*) FROM TestServices;");
                Assert.Equal(2, remaining);
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

            // Verify the engine systematically retried across the configured loop allocation space,
            // releasing the connection its using block acquired on every one of those attempts.
            Assert.Equal(AppConfig.DbAsyncMaxAttempts, busyConnectionSpy.OpenAttempts);
            Assert.Equal(AppConfig.DbAsyncMaxAttempts, busyConnectionSpy.DisposeCount);
        }

        /// <summary>
        /// Registers a <see cref="IAppDbContext.CreateConnection"/> factory that mints a FRESH
        /// <see cref="TransientFailureDbConnection"/> per call, the way production behaves: the connection is
        /// created inside the retried lambda and released by its using block at the end of every attempt.
        /// The first <paramref name="failuresBeforeSuccess"/> connections refuse to open; the next one succeeds.
        /// </summary>
        /// <param name="errorCode">The transient SQLite error code the failing connections raise.</param>
        /// <param name="failuresBeforeSuccess">How many connections fail to open before one succeeds.</param>
        /// <returns>The live list of connections handed out, in creation order.</returns>
        private List<TransientFailureDbConnection> SetupTransientConnectionFactory(SQLiteErrorCode errorCode, int failuresBeforeSuccess)
        {
            var createdConnections = new List<TransientFailureDbConnection>();

            _mockDbContext.Setup(db => db.CreateConnection()).Returns(() =>
            {
                // Each instance is opened at most once, so it fails on its own first (and only) attempt
                // until the configured number of failing connections has been handed out.
                int failuresForThisInstance = createdConnections.Count < failuresBeforeSuccess ? 1 : 0;

                var connection = new TransientFailureDbConnection(errorCode, failuresForThisInstance, _connectionString);
                createdConnections.Add(connection);

                return connection;
            });

            return createdConnections;
        }

        [Fact]
        public void ExecuteWithRetry_TransientBusyThenSuccess_RecoversAndReturnsResult()
        {
            // Arrange
            const int failuresBeforeSuccess = 2;
            var createdConnections = SetupTransientConnectionFactory(SQLiteErrorCode.Busy, failuresBeforeSuccess);

            // Act
            long count = _executor.ExecuteScalar<long>("SELECT COUNT(*) FROM TestServices;");

            // Assert
            Assert.Equal(2, count);

            // A fresh connection per attempt, each opened exactly once. Hoisting CreateConnection() out of
            // the retried lambda would open one connection three times and fail these two assertions.
            Assert.Equal(failuresBeforeSuccess + 1, createdConnections.Count);
            Assert.All(createdConnections, connection => Assert.Equal(1, connection.OpenAttempts));
            Assert.All(createdConnections, connection => Assert.Equal(1, connection.DisposeCount));
        }

        [Fact]
        public void ExecuteWithRetry_TransientLockedThenSuccess_RecoversAndReturnsResult()
        {
            // Arrange
            const int failuresBeforeSuccess = 1;
            var createdConnections = SetupTransientConnectionFactory(SQLiteErrorCode.Locked, failuresBeforeSuccess);

            // Act
            long count = _executor.ExecuteScalar<long>("SELECT COUNT(*) FROM TestServices;");

            // Assert
            Assert.Equal(2, count);
            Assert.Equal(failuresBeforeSuccess + 1, createdConnections.Count);
            Assert.All(createdConnections, connection => Assert.Equal(1, connection.OpenAttempts));
            Assert.All(createdConnections, connection => Assert.Equal(1, connection.DisposeCount));
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_TransientBusyThenSuccess_RecoversAndReturnsResult()
        {
            // Arrange
            const int failuresBeforeSuccess = 2;
            var createdConnections = SetupTransientConnectionFactory(SQLiteErrorCode.Busy, failuresBeforeSuccess);

            // Act
            long count = await _executor.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM TestServices;",
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.Equal(2, count);
            Assert.Equal(failuresBeforeSuccess + 1, createdConnections.Count);
            Assert.All(createdConnections, connection => Assert.Equal(1, connection.OpenAttempts));
            Assert.All(createdConnections, connection => Assert.Equal(1, connection.DisposeCount));
        }

        [Fact]
        public async Task ExecuteWithRetryAsync_TransientLockedThenSuccess_RecoversAndReturnsResult()
        {
            // Arrange
            const int failuresBeforeSuccess = 1;
            var createdConnections = SetupTransientConnectionFactory(SQLiteErrorCode.Locked, failuresBeforeSuccess);

            // Act
            long count = await _executor.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM TestServices;",
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.Equal(2, count);
            Assert.Equal(failuresBeforeSuccess + 1, createdConnections.Count);
            Assert.All(createdConnections, connection => Assert.Equal(1, connection.OpenAttempts));
            Assert.All(createdConnections, connection => Assert.Equal(1, connection.DisposeCount));
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
        [InlineData("SELECT * FROM MyTable\r\nWHERE Id = 1", null, "SELECT * FROM MyTable  WHERE Id = 1")]
        [InlineData("SELECT LongQueryStringThatExceedsTheStandardTruncationLimitForLogs", null, "SELECT LongQueryStringThatExceedsTheStandardTruncationLimitF...")] // Default 60-char limit
        [InlineData("SELECT VeryLongQueryStringThatNeedsCustomTruncationParametersForTesting", 25, "SELECT VeryLongQueryStrin...")] // Sync logger profile bound (25 chars + ...)
        [InlineData("SELECT VeryLongQueryStringThatNeedsCustomTruncationParametersForTesting", 50, "SELECT VeryLongQueryStringThatNeedsCustomTruncatio...")] // Async logger profile bound (50 chars + ...)
        [InlineData("", null, "Unknown Query")]
        [InlineData(null, null, "Unknown Query")]
        public void FormatSqlForLog_Variants_EvaluatesCorrectly(string inputSql, int? maxLength, string expectedLoggedSql)
        {
            // Arrange & Act
            // Pass Type.Missing for null maxLength to exercise the C# optional parameter default (maxLength = 60)
            object[] parameters = maxLength.HasValue
                ? new object[] { inputSql, maxLength.Value }
                : new object[] { inputSql, Type.Missing };

            var formatted = TestReflection.InvokeNonPublicStatic(
                typeof(DapperExecutor),
                "FormatSqlForLog",
                parameters) as string;

            // Assert
            Assert.Equal(expectedLoggedSql, formatted);
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(10, AppConfig.DbBackoffMaxMs)]   // 100 << 10 = 102400 -> clamped
        [InlineData(64, AppConfig.DbBackoffMaxMs)]   // shift clamp via Math.Min(attempt, 30)
        public void CalculateBackoff_ValidatesGrowthAndOverflowGuardRailCeilings(int attempt, int expectedBaseDelay)
        {
            // Arrange & Act
            // Pass four parameters to match CalculateBackoff(attempt, initialDelayMs, maxJitterMs, maxBackoffMs)
            var calculatedDelay = (int)TestReflection.InvokeNonPublicStatic(
                typeof(DapperExecutor),
                "CalculateBackoff",
                attempt,
                AppConfig.DbAsyncInitialDelayMs,
                0,
                AppConfig.DbBackoffMaxMs);

            // Assert
            Assert.Equal(expectedBaseDelay, calculatedDelay);
        }

        [Fact]
        public void CalculateBackoff_WithActiveJitter_AppliesRandomVarianceWithinBounds()
        {
            // Arrange
            const int attempt = 2; // 100 << 2 = 400 base delay
            const int initialDelayMs = 100;
            const int maxJitterMs = 50;
            const int maxDelayMs = 30000;

            var uniqueResults = new HashSet<int>();

            // Act: Sample multiple iterations to check distribution bounds and guarantee randomness
            for (int i = 0; i < 20; i++)
            {
                var delay = (int)TestReflection.InvokeNonPublicStatic(
                    typeof(DapperExecutor),
                    "CalculateBackoff",
                    attempt,
                    initialDelayMs,
                    maxJitterMs,
                    maxDelayMs);

                // Assert: Delay should fluctuate between base (400) and base + maxJitter (450)
                Assert.True(delay >= 400, $"Calculated delay ({delay}) fell below exponential base value.");
                Assert.True(delay <= 450, $"Calculated delay ({delay}) exceeded maximum jitter threshold bounds.");

                uniqueResults.Add(delay);
            }

            // Assert: Ensure variance is actually running (not returning a fixed constant)
            Assert.True(uniqueResults.Count > 1, "Backoff engine returned static values; random jitter execution appears disabled.");
        }

        #endregion

        public override void Dispose()
        {
            // Clear SQLite pools so it releases any file locks, then delete the temp DB
            SQLiteConnection.ClearAllPools();

            base.Dispose(); // then the retrying recursive delete
        }

        // Row type for Dapper materialization in this class.
        private class TestServiceDto
        {
            public string ServiceName { get; set; } = string.Empty;
            public int Status { get; set; }
        }
    }
}

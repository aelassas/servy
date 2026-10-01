using Dapper;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Infrastructure.Data;
using Servy.Testing;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;

namespace Servy.Infrastructure.IntegrationTests.Data
{
    [Collection(DatabaseTestCollection.Name)]
    public class SQLiteStateDbInitializerIntegrationTests : TempDirectoryTestBase
    {
        /// <summary>
        /// The runtime-state database file this test owns, inside its own temporary directory.
        /// A real file rather than an in-memory database, because the repository opens a connection per
        /// operation and every connection to <c>:memory:</c> would be a different, empty database.
        /// </summary>
        private string StateDbPath => Path.Combine(TempDirectory, AppConfig.StateDatabaseFileName);

        private string ConnectionString => $"Data Source={StateDbPath};Busy Timeout=5000;Journal Mode=WAL;Pooling=False;";

        /// <summary>
        /// Opens one connection to this test's runtime-state database file.
        /// </summary>
        private DbConnection OpenConnection()
        {
            var conn = new SQLiteConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        /// <summary>
        /// Initializes the schema and returns a repository over the same file.
        /// </summary>
        private ServiceStateRepository CreateInitializedRepository()
        {
            using (var conn = OpenConnection())
            {
                SQLiteStateDbInitializer.Initialize(conn);
            }

            return new ServiceStateRepository(new DapperExecutor(new AppDbContext(ConnectionString)));
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            // Release the pooled handles SQLite keeps, or the temp directory cannot be deleted.
            SQLiteConnection.ClearAllPools();
            base.Dispose();
        }

        [Fact]
        public void Initialize_FreshDatabase_CreatesTheTableAtTheLatestVersion()
        {
            // Arrange
            using (var conn = OpenConnection())
            {
                // Act
                SQLiteStateDbInitializer.Initialize(conn);

                // Assert
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.Equal(SQLiteStateDbInitializer.LatestSchemaVersion, version);

                var tables = conn.Query<string>("SELECT name FROM sqlite_master WHERE type='table';").ToList();
                Assert.Contains(StateSqlConstants.ServiceStateTableName, tables);
                Assert.Contains("SchemaInfo", tables);

                var columns = conn.Query($"PRAGMA table_info({StateSqlConstants.ServiceStateTableName});")
                    .Select(r => (string)r.name).ToList();
                Assert.Equal(new[] { "Name", "Pid", "ActiveStdoutPath", "ActiveStderrPath", "PreviousStopTimeout" }, columns);
            }
        }

        [Fact]
        public void Initialize_CreatesItsOwnFileSeparateFromTheConfigurationDatabase()
        {
            // Arrange
            using (var conn = OpenConnection())
            {
                // Act
                SQLiteStateDbInitializer.Initialize(conn);
            }

            // Assert
            Assert.True(File.Exists(StateDbPath));
            Assert.Equal("Servy.state.db", Path.GetFileName(StateDbPath));
            Assert.False(File.Exists(Path.Combine(TempDirectory, AppConfig.DatabaseFileName)));
        }

        [Fact]
        public void Initialize_NullConnection_Throws()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => SQLiteStateDbInitializer.Initialize(null!));
        }

        [Fact]
        public void Initialize_RunTwice_IsIdempotentAndKeepsTheData()
        {
            // Arrange
            using (var conn = OpenConnection())
            {
                SQLiteStateDbInitializer.Initialize(conn);
                conn.Execute($"INSERT INTO {StateSqlConstants.ServiceStateTableName} (Name, Pid) VALUES ('svc', 7);");

                // Act
                SQLiteStateDbInitializer.Initialize(conn);

                // Assert
                Assert.Equal(SQLiteStateDbInitializer.LatestSchemaVersion, conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;"));
                Assert.Equal(7, conn.QuerySingle<int>($"SELECT Pid FROM {StateSqlConstants.ServiceStateTableName} WHERE Name = 'svc';"));
            }
        }

        [Fact]
        public void Initialize_ColumnMissingFromAnExistingTable_IsAddedBack()
        {
            // Arrange - a table that predates three of the runtime-state columns (every one after Pid)
            using (var conn = OpenConnection())
            {
                conn.Execute($@"
                    CREATE TABLE {StateSqlConstants.ServiceStateTableName} (
                        Name TEXT NOT NULL PRIMARY KEY,
                        Pid INTEGER
                    );");
                conn.Execute("CREATE TABLE SchemaInfo (Id INTEGER PRIMARY KEY CHECK (Id = 1), Version INTEGER NOT NULL);");
                conn.Execute("INSERT INTO SchemaInfo (Id, Version) VALUES (1, 1);");

                // Act
                SQLiteStateDbInitializer.Initialize(conn);

                // Assert
                var columns = conn.Query($"PRAGMA table_info({StateSqlConstants.ServiceStateTableName});")
                    .Select(r => (string)r.name).ToList();
                Assert.Contains("ActiveStdoutPath", columns);
                Assert.Contains("ActiveStderrPath", columns);
                Assert.Contains("PreviousStopTimeout", columns);
            }
        }

        [Fact]
        public void Initialize_NewerSchemaOnDisk_LeavesItAlone()
        {
            // Arrange
            using (var conn = OpenConnection())
            {
                SQLiteStateDbInitializer.Initialize(conn);
                conn.Execute("UPDATE SchemaInfo SET Version = @v WHERE Id = 1;", new { v = SQLiteStateDbInitializer.LatestSchemaVersion + 5 });
                var before = ReadColumns(conn);

                // Act
                SQLiteStateDbInitializer.Initialize(conn);

                // Assert
                Assert.Equal(SQLiteStateDbInitializer.LatestSchemaVersion + 5, conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;"));
                Assert.Equal(before, ReadColumns(conn));
            }
        }

        [Fact]
        public void Initialize_NewerSchemaOnDisk_DoesNotAddBackAColumnTheFileDropped()
        {
            // Arrange - a file written by a newer Servy that no longer carries three of the columns
            // this build knows about. Reconciling it would ADD COLUMN them back, which is the
            // downgrade the warning says does not happen.
            using (var conn = OpenConnection())
            {
                conn.Execute($@"
                    CREATE TABLE {StateSqlConstants.ServiceStateTableName} (
                        Name TEXT NOT NULL PRIMARY KEY,
                        Pid INTEGER
                    );");
                conn.Execute("CREATE TABLE SchemaInfo (Id INTEGER PRIMARY KEY CHECK (Id = 1), Version INTEGER NOT NULL);");
                conn.Execute("INSERT INTO SchemaInfo (Id, Version) VALUES (1, @v);", new { v = SQLiteStateDbInitializer.LatestSchemaVersion + 5 });

                // Act
                SQLiteStateDbInitializer.Initialize(conn);

                // Assert
                var columns = ReadColumns(conn);
                Assert.Equal(new[] { "Name", "Pid" }, columns);
                Assert.DoesNotContain("ActiveStdoutPath", columns);
                Assert.DoesNotContain("ActiveStderrPath", columns);
            }
        }

        [Fact]
        public void Initialize_MigrationFailsHalfway_RollsBackTheVersionAndRethrows()
        {
            // Arrange - a view squatting on the table name makes ReconcileSchema's ALTER TABLE fail
            //           after version 1 has been written inside the same transaction. CREATE TABLE
            //           IF NOT EXISTS is a silent no-op while the view exists, so ApplyVersion1
            //           bumps the version over a table that was never created.
            using (var conn = OpenConnection())
            {
                conn.Execute($"CREATE VIEW {StateSqlConstants.ServiceStateTableName} AS SELECT 'x' AS Name;");

                // Act
                var ex = Record.Exception(() => SQLiteStateDbInitializer.Initialize(conn));

                // Assert
                Assert.NotNull(ex);
                Assert.Equal(0, conn.QuerySingle<int>("SELECT COUNT(*) FROM SchemaInfo;"));
            }
        }

        /// <summary>
        /// Reads the column names of the runtime-state table, in declaration order.
        /// </summary>
        private static List<string> ReadColumns(DbConnection connection)
        {
            return connection.Query($"PRAGMA table_info({StateSqlConstants.ServiceStateTableName});")
                .Select(r => (string)r.name).ToList();
        }

        /// <summary>
        /// Reads the declared type of one column of the runtime-state table.
        /// </summary>
        /// <remarks>
        /// The declared type is what decides the column's affinity, so it is the only way to tell a
        /// column the upgrade path added with its intended type from one it added as TEXT or untyped.
        /// </remarks>
        private static string ReadColumnType(DbConnection connection, string column)
        {
            return connection.Query($"PRAGMA table_info({StateSqlConstants.ServiceStateTableName});")
                .Where(r => (string)r.name == column)
                .Select(r => (string)r.type)
                .Single();
        }

        [Fact]
        public async Task Repository_RoundTripsTheRuntimeState()
        {
            // Arrange
            var repo = CreateInitializedRepository();
            var state = new ServiceStateDto
            {
                Name = "svc",
                Pid = 1234,
                ActiveStdoutPath = @"C:\logs\out.log",
                ActiveStderrPath = @"C:\logs\err.log",
                PreviousStopTimeout = 45
            };

            // Act
            var written = await repo.UpsertAsync(state, TestContext.Current.CancellationToken);
            var read = await repo.GetAsync("svc", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, written);
            Assert.NotNull(read);
            Assert.Equal("svc", read!.Name);
            Assert.Equal(1234, read.Pid);
            Assert.Equal(@"C:\logs\out.log", read.ActiveStdoutPath);
            Assert.Equal(@"C:\logs\err.log", read.ActiveStderrPath);
            Assert.Equal(45, read.PreviousStopTimeout);
        }

        [Fact]
        public void Initialize_FreshDatabase_DeclaresPreviousStopTimeoutAsInteger()
        {
            // Arrange
            using (var conn = OpenConnection())
            {
                // Act
                SQLiteStateDbInitializer.Initialize(conn);

                // Assert
                var declaredType = conn.Query($"PRAGMA table_info({StateSqlConstants.ServiceStateTableName});")
                    .Where(r => (string)r.name == "PreviousStopTimeout")
                    .Select(r => (string)r.type)
                    .Single();
                Assert.Equal("INTEGER", declaredType);
            }
        }

        [Fact]
        public void Initialize_ExistingTableWithoutPreviousStopTimeout_AddsItAndKeepsTheRows()
        {
            // Arrange - a runtime-state table written before PreviousStopTimeout moved here, with a row
            //           in it, which is what an upgrade over a step 1 file looks like.
            using (var conn = OpenConnection())
            {
                conn.Execute($@"
                    CREATE TABLE {StateSqlConstants.ServiceStateTableName} (
                        Name TEXT NOT NULL PRIMARY KEY,
                        Pid INTEGER,
                        ActiveStdoutPath TEXT,
                        ActiveStderrPath TEXT
                    );");
                conn.Execute("CREATE TABLE SchemaInfo (Id INTEGER PRIMARY KEY CHECK (Id = 1), Version INTEGER NOT NULL);");
                conn.Execute("INSERT INTO SchemaInfo (Id, Version) VALUES (1, 1);");
                conn.Execute($"INSERT INTO {StateSqlConstants.ServiceStateTableName} (Name, Pid) VALUES ('svc', 7);");

                // Act
                SQLiteStateDbInitializer.Initialize(conn);

                // Assert
                Assert.Contains("PreviousStopTimeout", ReadColumns(conn));
                Assert.Equal("INTEGER", ReadColumnType(conn, "PreviousStopTimeout"));
                Assert.Equal(7, conn.QuerySingle<int>($"SELECT Pid FROM {StateSqlConstants.ServiceStateTableName} WHERE Name = 'svc';"));
                Assert.Null(conn.QuerySingleOrDefault<int?>($"SELECT PreviousStopTimeout FROM {StateSqlConstants.ServiceStateTableName} WHERE Name = 'svc';"));
            }
        }

        [Fact]
        public async Task Repository_PreviousStopTimeout_IsReplacedByAnUpsertLikeTheOtherRuntimeValues()
        {
            // Arrange
            var repo = CreateInitializedRepository();
            await repo.UpsertAsync(new ServiceStateDto { Name = "svc", PreviousStopTimeout = 30 }, TestContext.Current.CancellationToken);

            // Act
            await repo.UpsertAsync(new ServiceStateDto { Name = "svc", PreviousStopTimeout = 90 }, TestContext.Current.CancellationToken);

            // Assert
            var all = (await repo.GetAllAsync(TestContext.Current.CancellationToken)).ToList();
            Assert.Single(all);
            Assert.Equal(90, all[0].PreviousStopTimeout);
        }

        [Fact]
        public async Task Repository_PreviousStopTimeout_IsClearedByAnUpsertThatOmitsIt()
        {
            // Arrange - the upsert replaces the whole row, so a state written without the timeout has
            //           to clear a stored one rather than leave the old value behind.
            var repo = CreateInitializedRepository();
            await repo.UpsertAsync(new ServiceStateDto { Name = "svc", PreviousStopTimeout = 30 }, TestContext.Current.CancellationToken);

            // Act
            await repo.UpsertAsync(new ServiceStateDto { Name = "svc", Pid = 11 }, TestContext.Current.CancellationToken);
            var read = await repo.GetAsync("svc", TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(read);
            Assert.Equal(11, read!.Pid);
            Assert.Null(read.PreviousStopTimeout);
        }

        [Fact]
        public async Task Repository_UpsertTwice_ReplacesTheRowInsteadOfAddingOne()
        {
            // Arrange
            var repo = CreateInitializedRepository();
            await repo.UpsertAsync(new ServiceStateDto { Name = "svc", Pid = 1 }, TestContext.Current.CancellationToken);

            // Act
            await repo.UpsertAsync(new ServiceStateDto { Name = "svc", Pid = 2, ActiveStdoutPath = "out" }, TestContext.Current.CancellationToken);

            // Assert
            var all = (await repo.GetAllAsync(TestContext.Current.CancellationToken)).ToList();
            Assert.Single(all);
            Assert.Equal(2, all[0].Pid);
            Assert.Equal("out", all[0].ActiveStdoutPath);
        }

        [Fact]
        public async Task Repository_NullRuntimeValues_RoundTripAsNull()
        {
            // Arrange
            var repo = CreateInitializedRepository();

            // Act
            await repo.UpsertAsync(new ServiceStateDto { Name = "svc" }, TestContext.Current.CancellationToken);
            var read = await repo.GetAsync("svc", TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(read);
            Assert.Null(read!.Pid);
            Assert.Null(read.ActiveStdoutPath);
            Assert.Null(read.ActiveStderrPath);
            Assert.Null(read.PreviousStopTimeout);
        }

        [Fact]
        public async Task Repository_GetMissingRow_ReturnsNull()
        {
            // Arrange
            var repo = CreateInitializedRepository();

            // Act
            var read = await repo.GetAsync("nope", TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(read);
        }

        [Fact]
        public async Task Repository_Delete_RemovesOnlyThatServicesRow()
        {
            // Arrange
            var repo = CreateInitializedRepository();
            await repo.UpsertAsync(new ServiceStateDto { Name = "a", Pid = 1 }, TestContext.Current.CancellationToken);
            await repo.UpsertAsync(new ServiceStateDto { Name = "b", Pid = 2 }, TestContext.Current.CancellationToken);

            // Act
            var removed = await repo.DeleteAsync("a", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, removed);
            Assert.Null(await repo.GetAsync("a", TestContext.Current.CancellationToken));
            Assert.NotNull(await repo.GetAsync("b", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Repository_DeleteMissingRow_ReturnsZero()
        {
            // Arrange
            var repo = CreateInitializedRepository();

            // Act
            var removed = await repo.DeleteAsync("nope", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0, removed);
        }

        [Fact]
        public async Task Repository_PaddedName_ReachesTheSameRowAsTheTrimmedOne()
        {
            // Arrange
            var repo = CreateInitializedRepository();
            await repo.UpsertAsync(new ServiceStateDto { Name = "svc", Pid = 5 }, TestContext.Current.CancellationToken);

            // Act
            await repo.UpsertAsync(new ServiceStateDto { Name = "  svc  ", Pid = 6 }, TestContext.Current.CancellationToken);

            // Assert
            var all = (await repo.GetAllAsync(TestContext.Current.CancellationToken)).ToList();
            Assert.Single(all);
            Assert.Equal(6, all[0].Pid);
        }

        [Fact]
        public async Task Repository_NameDifferingOnlyInCase_ReachesTheSameRow()
        {
            // Arrange
            var repo = CreateInitializedRepository();
            await repo.UpsertAsync(new ServiceStateDto { Name = "MyService", Pid = 11 }, TestContext.Current.CancellationToken);

            // Act
            await repo.UpsertAsync(new ServiceStateDto { Name = "myservice", Pid = 22 }, TestContext.Current.CancellationToken);
            var fetched = await repo.GetAsync("MYSERVICE", TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(fetched);
            Assert.Equal(22, fetched!.Pid);

            var all = (await repo.GetAllAsync(TestContext.Current.CancellationToken)).ToList();
            Assert.Single(all);
        }

        [Fact]
        public async Task Repository_DeleteInAnotherCase_RemovesTheRow()
        {
            // Arrange
            var repo = CreateInitializedRepository();
            await repo.UpsertAsync(new ServiceStateDto { Name = "MyService", Pid = 11 }, TestContext.Current.CancellationToken);

            // Act
            var deleted = await repo.DeleteAsync("myservice", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, deleted);
            Assert.Empty(await repo.GetAllAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Repository_NonAsciiCasingDifference_ReachesTheSameRow()
        {
            // Arrange - built-in NOCASE folds ASCII only; only UNICODE_NOCASE folds 'Ö'/'ö'.
            var repo = CreateInitializedRepository();
            await repo.UpsertAsync(new ServiceStateDto { Name = "ÖffnenService", Pid = 7 }, TestContext.Current.CancellationToken);

            // Act
            var fetched = await repo.GetAsync("öffnenservice", TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(fetched);
            Assert.Equal(7, fetched!.Pid);
        }

        [Fact]
        public async Task Repository_GetAll_OrdersNamesCaseInsensitively()
        {
            // Arrange - under the binary default 'Beta' sorts before 'alpha'.
            var repo = CreateInitializedRepository();
            await repo.UpsertAsync(new ServiceStateDto { Name = "Beta" }, TestContext.Current.CancellationToken);
            await repo.UpsertAsync(new ServiceStateDto { Name = "alpha" }, TestContext.Current.CancellationToken);

            // Act
            var names = (await repo.GetAllAsync(TestContext.Current.CancellationToken)).Select(s => s.Name).ToList();

            // Assert
            Assert.Equal(new[] { "alpha", "Beta" }, names);
        }

        [Fact]
        public void Initialize_NameKey_DeclaresTheCaseInsensitiveCollation()
        {
            // Arrange
            using (var conn = OpenConnection())
            {
                // Act
                SQLiteStateDbInitializer.Initialize(conn);

                // Assert
                var ddl = conn.QuerySingle<string>(
                    "SELECT sql FROM sqlite_master WHERE type='table' AND name=@Name;",
                    new { Name = StateSqlConstants.ServiceStateTableName });

                Assert.Contains("COLLATE UNICODE_NOCASE", ddl);
            }
        }

        [Fact]
        public void StateDatabase_DefaultConnectionStringPointsNextToTheConfigurationDatabase()
        {
            // Arrange
            var dbFolder = AppConfig.DbFolderPath;

            // Act
            var stateConnection = AppConfig.DefaultStateConnectionString;

            // Assert
            Assert.NotEqual(AppConfig.DatabaseFileName, AppConfig.StateDatabaseFileName);
            Assert.Contains(Path.Combine(dbFolder, AppConfig.StateDatabaseFileName), stateConnection);
            Assert.Contains(Path.Combine(dbFolder, AppConfig.DatabaseFileName), AppConfig.DefaultConnectionString);
        }
    }
}

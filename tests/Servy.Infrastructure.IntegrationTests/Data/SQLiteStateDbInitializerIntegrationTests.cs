using Dapper;
using Servy.Core.Config;
using Servy.Core.DTOs;
using Servy.Infrastructure.Data;
using Servy.Testing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

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
                Assert.Equal(new[] { "Name", "Pid", "ActiveStdoutPath", "ActiveStderrPath" }, columns);
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
            Assert.Throws<ArgumentNullException>(() => SQLiteStateDbInitializer.Initialize(null));
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
            // Arrange - a table that predates two of the runtime-state columns
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
            // Arrange - a file written by a newer Servy that no longer carries two of the columns
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

        /// <summary>
        /// Reads the column names of the runtime-state table, in declaration order.
        /// </summary>
        private static List<string> ReadColumns(DbConnection connection)
        {
            return connection.Query($"PRAGMA table_info({StateSqlConstants.ServiceStateTableName});")
                .Select(r => (string)r.name).ToList();
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
                ActiveStderrPath = @"C:\logs\err.log"
            };

            // Act
            var written = await repo.UpsertAsync(state);
            var read = await repo.GetAsync("svc");

            // Assert
            Assert.Equal(1, written);
            Assert.NotNull(read);
            Assert.Equal("svc", read.Name);
            Assert.Equal(1234, read.Pid);
            Assert.Equal(@"C:\logs\out.log", read.ActiveStdoutPath);
            Assert.Equal(@"C:\logs\err.log", read.ActiveStderrPath);
        }

        [Fact]
        public async Task Repository_UpsertTwice_ReplacesTheRowInsteadOfAddingOne()
        {
            // Arrange
            var repo = CreateInitializedRepository();
            await repo.UpsertAsync(new ServiceStateDto { Name = "svc", Pid = 1 });

            // Act
            await repo.UpsertAsync(new ServiceStateDto { Name = "svc", Pid = 2, ActiveStdoutPath = "out" });

            // Assert
            var all = (await repo.GetAllAsync()).ToList();
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
            await repo.UpsertAsync(new ServiceStateDto { Name = "svc" });
            var read = await repo.GetAsync("svc");

            // Assert
            Assert.NotNull(read);
            Assert.Null(read.Pid);
            Assert.Null(read.ActiveStdoutPath);
            Assert.Null(read.ActiveStderrPath);
        }

        [Fact]
        public async Task Repository_GetMissingRow_ReturnsNull()
        {
            // Arrange
            var repo = CreateInitializedRepository();

            // Act
            var read = await repo.GetAsync("nope");

            // Assert
            Assert.Null(read);
        }

        [Fact]
        public async Task Repository_Delete_RemovesOnlyThatServicesRow()
        {
            // Arrange
            var repo = CreateInitializedRepository();
            await repo.UpsertAsync(new ServiceStateDto { Name = "a", Pid = 1 });
            await repo.UpsertAsync(new ServiceStateDto { Name = "b", Pid = 2 });

            // Act
            var removed = await repo.DeleteAsync("a");

            // Assert
            Assert.Equal(1, removed);
            Assert.Null(await repo.GetAsync("a"));
            Assert.NotNull(await repo.GetAsync("b"));
        }

        [Fact]
        public async Task Repository_DeleteMissingRow_ReturnsZero()
        {
            // Arrange
            var repo = CreateInitializedRepository();

            // Act
            var removed = await repo.DeleteAsync("nope");

            // Assert
            Assert.Equal(0, removed);
        }

        [Fact]
        public async Task Repository_PaddedName_ReachesTheSameRowAsTheTrimmedOne()
        {
            // Arrange
            var repo = CreateInitializedRepository();
            await repo.UpsertAsync(new ServiceStateDto { Name = "svc", Pid = 5 });

            // Act
            await repo.UpsertAsync(new ServiceStateDto { Name = "  svc  ", Pid = 6 });

            // Assert
            var all = (await repo.GetAllAsync()).ToList();
            Assert.Single(all);
            Assert.Equal(6, all[0].Pid);
        }

        [Fact]
        public async Task Repository_NameDifferingOnlyInCase_ReachesTheSameRow()
        {
            // Arrange
            var repo = CreateInitializedRepository();
            await repo.UpsertAsync(new ServiceStateDto { Name = "MyService", Pid = 11 });

            // Act
            await repo.UpsertAsync(new ServiceStateDto { Name = "myservice", Pid = 22 });
            var fetched = await repo.GetAsync("MYSERVICE");

            // Assert
            Assert.NotNull(fetched);
            Assert.Equal(22, fetched.Pid);

            var all = (await repo.GetAllAsync()).ToList();
            Assert.Single(all);
        }

        [Fact]
        public async Task Repository_DeleteInAnotherCase_RemovesTheRow()
        {
            // Arrange
            var repo = CreateInitializedRepository();
            await repo.UpsertAsync(new ServiceStateDto { Name = "MyService", Pid = 11 });

            // Act
            var deleted = await repo.DeleteAsync("myservice");

            // Assert
            Assert.Equal(1, deleted);
            Assert.Empty(await repo.GetAllAsync());
        }

        [Fact]
        public async Task Repository_NonAsciiCasingDifference_ReachesTheSameRow()
        {
            // Arrange - built-in NOCASE folds ASCII only; only UNICODE_NOCASE folds 'Ö'/'ö'.
            var repo = CreateInitializedRepository();
            await repo.UpsertAsync(new ServiceStateDto { Name = "ÖffnenService", Pid = 7 });

            // Act
            var fetched = await repo.GetAsync("öffnenservice");

            // Assert
            Assert.NotNull(fetched);
            Assert.Equal(7, fetched.Pid);
        }

        [Fact]
        public async Task Repository_GetAll_OrdersNamesCaseInsensitively()
        {
            // Arrange - under the binary default 'Beta' sorts before 'alpha'.
            var repo = CreateInitializedRepository();
            await repo.UpsertAsync(new ServiceStateDto { Name = "Beta" });
            await repo.UpsertAsync(new ServiceStateDto { Name = "alpha" });

            // Act
            var names = (await repo.GetAllAsync()).Select(s => s.Name).ToList();

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
        public async Task Repository_GetAll_IsOrderedByName()
        {
            // Arrange
            var repo = CreateInitializedRepository();
            await repo.UpsertAsync(new ServiceStateDto { Name = "zeta" });
            await repo.UpsertAsync(new ServiceStateDto { Name = "alpha" });

            // Act
            var all = (await repo.GetAllAsync()).Select(s => s.Name).ToList();

            // Assert
            Assert.Equal(new[] { "alpha", "zeta" }, all);
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

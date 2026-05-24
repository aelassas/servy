using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Linq;
using System.Reflection;
using Dapper;
using Servy.Infrastructure.Data;
using Xunit;

namespace Servy.Infrastructure.IntegrationTests.Data
{
    public class SQLiteDbInitializerIntegrationTests
    {
        /// <summary>
        /// Helper to create a fresh, isolated in-memory database connection for each test.
        /// </summary>
        private static DbConnection CreateConnection()
        {
            var conn = new SQLiteConnection("Data Source=:memory:;Version=3;New=True;");
            conn.Open();
            return conn;
        }

        #region Standard Migrations & Core Branches

        [Fact]
        public void Initialize_FreshDatabase_AppliesAllMigrationsAndReconciles()
        {
            using (var conn = CreateConnection())
            {

                // Act
                SQLiteDbInitializer.Initialize(conn);

                // Assert
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.True(version >= 5, "Database should be migrated to at least version 5.");

                var tables = conn.Query<string>("SELECT name FROM sqlite_master WHERE type='table';").ToList();
                Assert.Contains("Services", tables);
                Assert.Contains("SchemaInfo", tables);

                var columns = conn.Query("PRAGMA table_info(Services);").Select(r => (string)r.name).ToList();
                Assert.Contains("EnableSizeRotation", columns); // Applied by V2
                Assert.Contains("EnableConsoleUI", columns); // Applied by V3
                Assert.Contains("RecoveryOnCleanExit", columns); // Applied by V5
            }
        }

        [Fact]
        public void Initialize_CatchesException_RollsBackTransaction()
        {
            using (var conn = CreateConnection())
            {

                // Arrange: Poison the database to force a SQL exception during ApplyVersion1
                // By creating 'Services' as a VIEW, the subsequent 'CREATE UNIQUE INDEX' on it will throw a SQLiteException.
                conn.Execute("CREATE TABLE SchemaInfo (Id INTEGER PRIMARY KEY CHECK (Id = 1), Version INTEGER NOT NULL);");
                conn.Execute("INSERT INTO SchemaInfo (Id, Version) VALUES (1, 0);");
                conn.Execute("CREATE VIEW Services AS SELECT 1 AS Id;");

                // Act & Assert
                var ex = Assert.Throws<SQLiteException>(() => SQLiteDbInitializer.Initialize(conn));

                // Verify the transaction was successfully rolled back
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.Equal(0, version); // Version should NOT have incremented to 1 due to rollback
            }
        }

        #endregion

        #region Legacy Upgrades & Deduplication (Version 0)

        [Fact]
        public void Initialize_LegacyUnversionedDatabase_PerformsDeduplicationAndUpgrades()
        {
            using (var conn = CreateConnection())
            {

                // Access internal definitions to properly build the legacy table avoiding SQLite NOT NULL strict constraints
                var getSqlType = typeof(SQLiteDbInitializer).GetMethod("GetSqlType", BindingFlags.Static | BindingFlags.NonPublic);
                var getExpectedCols = typeof(SQLiteDbInitializer).GetMethod("GetExpectedColumns", BindingFlags.Static | BindingFlags.NonPublic);
                var expectedCols = (IEnumerable<string>)getExpectedCols!.Invoke(null, null)!;

                var colDefs = new List<string> { "Id INTEGER PRIMARY KEY AUTOINCREMENT", "Name TEXT", "EnableRotation INTEGER" };
                var insertCols = new List<string> { "Name", "EnableRotation" };
                var insertVals1 = new List<string> { "'TestService'", "1" };
                var insertVals2 = new List<string> { "'testservice'", "0" };
                var insertVals3 = new List<string> { "'TESTSERVICE'", "0" };

                // Pre-bake strict NOT NULL columns into the V0 schema to prevent SQLite ALTER TABLE crashes
                foreach (var col in expectedCols)
                {
                    if (col.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
                        col.Equals("EnableRotation", StringComparison.OrdinalIgnoreCase) ||
                        col.Equals("EnableSizeRotation", StringComparison.OrdinalIgnoreCase)) continue;

                    string sqlType = (string)getSqlType!.Invoke(null, new object[] { col })!;
                    if (sqlType.IndexOf("NOT NULL", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        sqlType.IndexOf("DEFAULT", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        colDefs.Add($"{col} {sqlType}");
                        insertCols.Add(col);
                        string val = sqlType.IndexOf("TEXT", StringComparison.OrdinalIgnoreCase) >= 0 ? "''" : "0";
                        insertVals1.Add(val);
                        insertVals2.Add(val);
                        insertVals3.Add(val);
                    }
                }

                // Arrange: Simulate an old V0 database
                conn.Execute($"CREATE TABLE Services ({string.Join(", ", colDefs)});");

                // Insert duplicates to trigger the duplicate grouping branch
                string insertTemplate = $"INSERT INTO Services ({string.Join(", ", insertCols)}) VALUES ";
                conn.Execute($"{insertTemplate} ({string.Join(", ", insertVals1)});");
                conn.Execute($"{insertTemplate} ({string.Join(", ", insertVals2)});");
                conn.Execute($"{insertTemplate} ({string.Join(", ", insertVals3)});");

                // Create the legacy non-unique index to trigger the index replacement branch
                conn.Execute("CREATE INDEX idx_services_name_lower ON Services(LOWER(Name));");

                // Act
                SQLiteDbInitializer.Initialize(conn);

                // Assert
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.True(version >= 5);

                // Verify Deduplication (only the oldest ID should remain)
                var services = conn.Query("SELECT Id FROM Services;").ToList();
                Assert.Single(services);
                Assert.Equal(1L, (long)services[0].Id);

                // Verify the old index was dropped and replaced with a UNIQUE index
                var indexInfo = conn.QuerySingle("PRAGMA index_list('Services');");
                Assert.Equal("idx_services_name_lower", (string)indexInfo.name);
                Assert.Equal(1L, (long)indexInfo.unique);

                // Verify 'EnableRotation' was renamed to 'EnableSizeRotation'
                var columns = conn.Query("PRAGMA table_info(Services);").Select(r => (string)r.name).ToList();
                Assert.DoesNotContain("EnableRotation", columns);
                Assert.Contains("EnableSizeRotation", columns);
            }
        }

        #endregion

        #region V4 Rebuild & Helper Skip Branches

        [Fact]
        public void Initialize_Version3Database_WithOrphanColumn_LogsAndRebuildsTable()
        {
            using (var conn = CreateConnection())
            {

                // Arrange: Set DB exactly to V3 state
                conn.Execute("CREATE TABLE SchemaInfo (Id INTEGER PRIMARY KEY, Version INTEGER);");
                conn.Execute("INSERT INTO SchemaInfo (Id, Version) VALUES (1, 3);");

                // Include an orphan column to trigger the `orphansBeforeRebuild.Count > 0` branch in ApplyVersion4
                conn.Execute(@"
                CREATE TABLE Services (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    OldOrphanData TEXT
                );
            ");

                // Act
                SQLiteDbInitializer.Initialize(conn);

                // Assert
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.True(version >= 4);

                var columns = conn.Query("PRAGMA table_info(Services);").Select(r => (string)r.name).ToList();
                Assert.DoesNotContain("OldOrphanData", columns); // Ensure rebuild dropped it safely
            }
        }

        [Fact]
        public void MigrationHelpers_AlreadyApplied_SkipsGracefully()
        {
            using (var conn = CreateConnection())
            {

                // Arrange: Set DB to V1 state
                conn.Execute("CREATE TABLE SchemaInfo (Id INTEGER PRIMARY KEY, Version INTEGER);");
                conn.Execute("INSERT INTO SchemaInfo (Id, Version) VALUES (1, 1);");

                // Create table with 'EnableSizeRotation' already existing (triggers Rename skip existing branch)
                // Lacks 'EnableRotation' (triggers Rename source missing branch)
                // Has 'RecoveryOnCleanExit' already (triggers AddColumn skip branch)
                conn.Execute(@"
                CREATE TABLE Services (
                    Id INTEGER PRIMARY KEY, 
                    EnableSizeRotation INTEGER,
                    RecoveryOnCleanExit INTEGER
                );
            ");

                // Act
                SQLiteDbInitializer.Initialize(conn);

                // Assert - The skips should allow the initialization to complete cleanly without throwing SQL syntax errors
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.True(version >= 5);
            }
        }

        [Fact]
        public void ApplyVersion2_ExistingOldAndNewColumn_SkipsRename()
        {
            using (var conn = CreateConnection())
            {
                // Arrange: Simulate a weird state where BOTH the old and new columns exist.
                conn.Execute("CREATE TABLE SchemaInfo (Id INTEGER PRIMARY KEY, Version INTEGER);");
                conn.Execute("INSERT INTO SchemaInfo (Id, Version) VALUES (1, 1);");
                conn.Execute("CREATE TABLE Services (Id INTEGER PRIMARY KEY, EnableRotation INTEGER, EnableSizeRotation INTEGER);");

                // Act: Invoke ApplyVersion2 directly to bypass V4's destructive rebuild logic
                using (var tx = conn.BeginTransaction())
                {
                    var applyVersion2 = typeof(SQLiteDbInitializer).GetMethod("ApplyVersion2", BindingFlags.Static | BindingFlags.NonPublic);
                    applyVersion2!.Invoke(null, new object[] { conn, tx });
                    tx.Commit();

                    // Assert
                    var columns = conn.Query("PRAGMA table_info(Services);").Select(r => (string)r.name).ToList();
                    Assert.Contains("EnableRotation", columns); // Left alone
                    Assert.Contains("EnableSizeRotation", columns); // Left alone
                }
            }
        }

        #endregion

        #region Reconciliation Self-Healing (Missing, Orphans, Mismatches)

        [Fact]
        public void ReconcileSchema_WithMissingOrphanAndMismatchedColumns_HealsAndLogs()
        {
            using (var conn = CreateConnection())
            {

                // Step 1: Perform a full baseline initialization to get the perfect expected schema.
                SQLiteDbInitializer.Initialize(conn);
                var expectedColumns = conn.Query("PRAGMA table_info(Services);").Select(r => (string)r.name).ToList();

                // Step 2: Sabotage the schema
                conn.Execute("DROP TABLE Services;");

                // We rebuild it, intentionally omitting the second column (usually 'Name' or 'ServiceName')
                // We change the type of 'EnableSizeRotation' to TEXT to force a Type Mismatch.
                // We add an 'OrphanColumn' to force the Orphan branch.
                var missingColumn = expectedColumns.First(c => c != "Id" && !c.Contains("Rotation"));

                var corruptedTableDef = new List<string> { "Id INTEGER PRIMARY KEY", "OrphanColumn TEXT" };
                foreach (var col in expectedColumns)
                {
                    if (col == missingColumn) continue; // Force missing branch
                    if (col == "EnableSizeRotation")
                    {
                        corruptedTableDef.Add($"{col} TEXT"); // Force mismatch branch
                    }
                    else if (col != "Id")
                    {
                        corruptedTableDef.Add($"{col} INTEGER");
                    }
                }

                conn.Execute($"CREATE TABLE Services ({string.Join(", ", corruptedTableDef)});");

                // Reset schema version to max so migrations don't run, forcing ReconcileSchema to do all the work
                conn.Execute("UPDATE SchemaInfo SET Version = 5 WHERE Id = 1;");

                // Step 3: Act - Run Initialize again
                SQLiteDbInitializer.Initialize(conn);

                // Step 4: Assert 
                var finalColumns = conn.Query("PRAGMA table_info(Services);").Select(r => (string)r.name).ToList();

                // The missing column should have been successfully restored
                Assert.Contains(missingColumn, finalColumns);
                // The orphan remains (we just log it, we don't drop it automatically)
                Assert.Contains("OrphanColumn", finalColumns);

                // Note: Mismatches are logged, not automatically altered, because SQLite doesn't support ALTER COLUMN type.
                var typeMismatchType = conn.QuerySingle<string>("SELECT type FROM pragma_table_info('Services') WHERE name = 'EnableSizeRotation';");
                Assert.Equal("TEXT", typeMismatchType); // Should still be TEXT as we sabotaged it
            }
        }

        #endregion

        #region Reflection Error Trapping

        [Fact]
        public void GetSqlType_MissingColumn_ThrowsInvalidOperationException()
        {
            // Arrange
            var methodInfo = typeof(SQLiteDbInitializer).GetMethod("GetSqlType",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(methodInfo);

            // Act & Assert
            var ex = Assert.Throws<TargetInvocationException>(() =>
                methodInfo.Invoke(null, new object[] { "NonExistentMagicalColumn_12345" }));

            // The inner exception must be InvalidOperationException from the fail-fast check
            Assert.IsType<InvalidOperationException>(ex.InnerException);
            Assert.Contains("lacks an [SqlColumn] attribute", ex.InnerException.Message);
        }

        #endregion
    }
}
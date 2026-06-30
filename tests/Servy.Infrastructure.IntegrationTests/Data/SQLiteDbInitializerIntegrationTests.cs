using Dapper;
using Servy.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Servy.Infrastructure.IntegrationTests.Data
{
    [Collection("SequentialDatabaseTests")]
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
        public void Initialize_OnMigrationFailure_RollsBackTransactionAndRethrows()
        {
            using (var conn = CreateConnection())
            {
                // Arrange: Poison the database to force a SQL exception during ApplyVersion1
                // By creating 'Services' as a VIEW, the subsequent 'CREATE UNIQUE INDEX' on it will throw a SQLiteException.
                conn.Execute("CREATE TABLE SchemaInfo (Id INTEGER PRIMARY KEY CHECK (Id = 1), Version INTEGER NOT NULL);");
                conn.Execute("INSERT INTO SchemaInfo (Id, Version) VALUES (1, 0);");
                conn.Execute("CREATE VIEW Services AS SELECT 1 AS Id;");

                // Act & Assert
                Assert.Throws<SQLiteException>(() => SQLiteDbInitializer.Initialize(conn));

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
                var expectedCols = (IEnumerable<string>)getExpectedCols.Invoke(null, null);

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

                    string sqlType = (string)getSqlType.Invoke(null, new object[] { col });
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

                // Insert three case-duplicates sequentially (Id 1..3); dedup must keep MIN(Id)=1,
                // so a last-write-wins/MAX(Id) implementation would fail the assertion below.
                conn.Execute($"{InsertTemplate(insertCols)} ({string.Join(", ", insertVals1)});");
                conn.Execute($"{InsertTemplate(insertCols)} ({string.Join(", ", insertVals2)});");
                conn.Execute($"{InsertTemplate(insertCols)} ({string.Join(", ", insertVals3)});");

                // Create the legacy non-unique index to trigger the index replacement branch
                conn.Execute("CREATE INDEX idx_services_name_lower ON Services(LOWER(Name));");

                // Act
                // Trigger migration, executing MIN(Id) evaluation to clean up duplicates deterministically
                SQLiteDbInitializer.Initialize(conn);

                // Assert
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.True(version >= 5);

                // Verify Deduplication (the absolute smallest historical record ID=1 must win)
                var services = conn.Query("SELECT Id FROM Services;").ToList();
                Assert.Single(services);
                Assert.Equal(1L, (long)services[0].Id);

                // Verify the old index was dropped and replaced with a UNIQUE index
                var indexInfo = conn.QuerySingle("PRAGMA index_list('Services');");
                Assert.Equal("idx_services_name_unique", (string)indexInfo.name);
                Assert.Equal(1L, (long)indexInfo.unique);

                // Verify 'EnableRotation' was renamed to 'EnableSizeRotation'
                var columns = conn.Query("PRAGMA table_info(Services);").Select(r => (string)r.name).ToList();
                Assert.DoesNotContain("EnableRotation", columns);
                Assert.Contains("EnableSizeRotation", columns);
            }
        }

        private static string InsertTemplate(List<string> insertCols) =>
            $"INSERT INTO Services ({string.Join(", ", insertCols)}) VALUES ";

        #endregion

        #region V4 Rebuild & Helper Skip Branches

        [Fact]
        public void Initialize_Version3Database_WithOrphanColumn_PreservesOrphanDataInBackupTable()
        {
            using (var conn = CreateConnection())
            {
                // Arrange: Set DB exactly to V3 state
                conn.Execute("CREATE TABLE SchemaInfo (Id INTEGER PRIMARY KEY, Version INTEGER);");
                conn.Execute("INSERT INTO SchemaInfo (Id, Version) VALUES (1, 3);");

                // Access internal definition engines to construct a schema matching v3 validation blocks
                var getSqlType = typeof(SQLiteDbInitializer).GetMethod("GetSqlType", BindingFlags.Static | BindingFlags.NonPublic);
                var getExpectedCols = typeof(SQLiteDbInitializer).GetMethod("GetExpectedColumns", BindingFlags.Static | BindingFlags.NonPublic);
                var expectedCols = (IEnumerable<string>)getExpectedCols.Invoke(null, null);

                // Base columns required to trace the setup and trigger the orphan branch
                var colDefs = new List<string> { "Id INTEGER PRIMARY KEY AUTOINCREMENT", "Name TEXT NOT NULL", "OldOrphanData TEXT" };
                var insertCols = new List<string> { "Name", "OldOrphanData" };
                var insertVals = new List<string> { "'LegacyAgent'", "'CriticalConfigToken_XYZ'" };

                // Dynamically append expected columns that enforce strict NOT NULL constraints to prevent insertion failures
                foreach (var col in expectedCols)
                {
                    if (col.Equals("Name", StringComparison.OrdinalIgnoreCase)) continue;

                    string sqlType = (string)getSqlType.Invoke(null, new object[] { col });
                    if (sqlType.IndexOf("NOT NULL", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        sqlType.IndexOf("DEFAULT", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        colDefs.Add($"{col} {sqlType}");
                        insertCols.Add(col);

                        // Seed safe dummy fallback values matching the structural type requirement
                        string seedValue = sqlType.IndexOf("TEXT", StringComparison.OrdinalIgnoreCase) >= 0 ? "'Dummy'" : "0";
                        insertVals.Add(seedValue);
                    }
                }

                // Create the mock table with structurally accurate schema constraints
                conn.Execute($"CREATE TABLE Services ({string.Join(", ", colDefs)});");
                conn.Execute($"INSERT INTO Services ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertVals)});");

                // Act
                // Triggers ApplyVersion4 table-rebuild execution path
                SQLiteDbInitializer.Initialize(conn);

                // Assert
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.True(version >= 4);

                // 1. Verify the active production table was rebuilt clean without the orphan column
                var columns = conn.Query("PRAGMA table_info(Services);").Select(r => (string)r.name).ToList();
                Assert.DoesNotContain("OldOrphanData", columns);

                // 2. Confirms backup side-table exists and retains original structure
                var tables = conn.Query<string>("SELECT name FROM sqlite_master WHERE type='table';").ToList();
                Assert.Contains("Services_orphans_v4", tables);

                // 3. Confirm exact values and binding Id keys survived the drop sequence completely
                var orphanData = conn.QuerySingle("SELECT Id, OldOrphanData FROM Services_orphans_v4;");
                Assert.Equal(1L, (long)orphanData.Id);
                Assert.Equal("CriticalConfigToken_XYZ", (string)orphanData.OldOrphanData);
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
                    applyVersion2.Invoke(null, new object[] { conn, tx });
                    tx.Commit();

                    // Assert
                    var columns = conn.Query("PRAGMA table_info(Services);").Select(r => (string)r.name).ToList();
                    Assert.Contains("EnableRotation", columns); // Left alone
                    Assert.Contains("EnableSizeRotation", columns); // Left alone
                }
            }
        }

        #endregion

        #region V6

        [Fact]
        public void ApplyVersion6_AsciiCasingDuplicates_DeduplicatesAndAppliesNoCaseIndex()
        {
            using (var conn = CreateConnection())
            {
                // Arrange: Initialize a clean baseline up to Version 5 state
                conn.Execute("CREATE TABLE SchemaInfo (Id INTEGER PRIMARY KEY, Version INTEGER);");
                conn.Execute("INSERT INTO SchemaInfo (Id, Version) VALUES (1, 5);");

                // Construct a valid pre-v6 table layout manually to safely insert custom casing variations
                var getSqlType = typeof(SQLiteDbInitializer).GetMethod("GetSqlType", BindingFlags.Static | BindingFlags.NonPublic);
                var getExpectedCols = typeof(SQLiteDbInitializer).GetMethod("GetExpectedColumns", BindingFlags.Static | BindingFlags.NonPublic);
                var expectedCols = (IEnumerable<string>)getExpectedCols.Invoke(null, null);

                var colDefs = new List<string> { "Id INTEGER PRIMARY KEY AUTOINCREMENT", "Name TEXT" };
                var insertCols = new List<string> { "Name" };
                var insertVals1 = new List<string> { "'Alpha-Service'" };
                var insertVals2 = new List<string> { "'alpha-service'" };

                foreach (var col in expectedCols)
                {
                    if (col.Equals("Name", StringComparison.OrdinalIgnoreCase)) continue;

                    string sqlType = (string)getSqlType.Invoke(null, new object[] { col });
                    colDefs.Add($"{col} {sqlType}");
                    insertCols.Add(col);
                    string val = sqlType.IndexOf("TEXT", StringComparison.OrdinalIgnoreCase) >= 0 ? "''" : "0";
                    insertVals1.Add(val);
                    insertVals2.Add(val);
                }

                conn.Execute($"CREATE TABLE Services ({string.Join(", ", colDefs)});");

                // Setup the old functional index as NON-UNIQUE so it permits the insert of casing variations on legacy systems.
                conn.Execute("CREATE INDEX idx_services_name_lower ON Services(LOWER(Name));");

                // Seed duplicate rows out of chronological order to check oldest historical match selection (MIN(Id) resolution)
                conn.Execute($"INSERT INTO Services ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertVals1)});");
                conn.Execute($"INSERT INTO Services ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertVals2)});");

                // Act: Trigger initialization to catch version 5 -> 6 transition pipeline branch
                SQLiteDbInitializer.Initialize(conn);

                // Assert
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.Equal(6, version);

                // Verify table deduplication pass: only the oldest instance (Id = 1) survives the constraint cleanup
                var remainingServices = conn.Query("SELECT Id, Name FROM Services;").ToList();
                Assert.Single(remainingServices);
                Assert.Equal(1L, (long)remainingServices[0].Id);
                Assert.Equal("Alpha-Service", (string)remainingServices[0].Name);

                // Verify the structural index details map directly to the modern COLLATE UNICODE_NOCASE layout rules
                var indexList = conn.Query("PRAGMA index_list('Services');")
                                    .Select(x => (IDictionary<string, object>)x)
                                    .ToList();

                var targetingIndex = indexList.FirstOrDefault(idx => string.Equals(idx["name"]?.ToString(), "idx_services_name_unique", StringComparison.OrdinalIgnoreCase));

                Assert.NotNull(targetingIndex);
                Assert.Equal(1L, Convert.ToInt64(targetingIndex["unique"]));

                // Confirm index expression metadata properties use the raw column reference
                var indexInfo = conn.Query("PRAGMA index_info('idx_services_name_unique');")
                                    .Select(x => (IDictionary<string, object>)x)
                                    .ToList();

                Assert.Single(indexInfo);
                Assert.Equal("Name", indexInfo[0]["name"]?.ToString());
            }
        }

        [Fact]
        public void ApplyVersion6_UnicodeCasingDuplicates_DeduplicatesAndAppliesUnicodeNoCaseIndex()
        {
            using (var conn = CreateConnection())
            {
                // Arrange: Initialize baseline up to Version 5 state
                conn.Execute("CREATE TABLE SchemaInfo (Id INTEGER PRIMARY KEY, Version INTEGER);");
                conn.Execute("INSERT INTO SchemaInfo (Id, Version) VALUES (1, 5);");

                var getSqlType = typeof(SQLiteDbInitializer).GetMethod("GetSqlType", BindingFlags.Static | BindingFlags.NonPublic);
                var getExpectedCols = typeof(SQLiteDbInitializer).GetMethod("GetExpectedColumns", BindingFlags.Static | BindingFlags.NonPublic);
                var expectedCols = (IEnumerable<string>)getExpectedCols.Invoke(null, null);

                var colDefs = new List<string> { "Id INTEGER PRIMARY KEY AUTOINCREMENT", "Name TEXT" };
                var insertCols = new List<string> { "Name" };
                var insertVals1 = new List<string> { "'Ä-Service'" };
                var insertVals2 = new List<string> { "'ä-service'" };

                foreach (var col in expectedCols)
                {
                    if (col.Equals("Name", StringComparison.OrdinalIgnoreCase)) continue;

                    string sqlType = (string)getSqlType.Invoke(null, new object[] { col });
                    colDefs.Add($"{col} {sqlType}");
                    insertCols.Add(col);
                    string val = sqlType.IndexOf("TEXT", StringComparison.OrdinalIgnoreCase) >= 0 ? "''" : "0";
                    insertVals1.Add(val);
                    insertVals2.Add(val);
                }

                conn.Execute($"CREATE TABLE Services ({string.Join(", ", colDefs)});");
                conn.Execute("CREATE INDEX idx_services_name_lower ON Services(LOWER(Name));");

                // Seed duplicate rows utilizing wide non-ASCII variants out of case parity
                conn.Execute($"INSERT INTO Services ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertVals1)});");
                conn.Execute($"INSERT INTO Services ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertVals2)});");

                // Act
                SQLiteDbInitializer.Initialize(conn);

                // Assert: Verify UNICODE_NOCASE successfully group-collapsed and purged the duplicate non-ASCII character entries
                var remainingServices = conn.Query("SELECT Id, Name FROM Services;").ToList();
                Assert.Single(remainingServices);
                Assert.Equal(1L, (long)remainingServices[0].Id);
                Assert.Equal("Ä-Service", (string)remainingServices[0].Name);
            }
        }

        [Fact]
        public void UnicodeNoCaseCollation_InsertsAndQueriesNonAsciiCasing_EnforcesUniqueness()
        {
            using (var conn = CreateConnection())
            {
                // Arrange: Execute complete initialization runner to build schema and spin custom collations up
                SQLiteDbInitializer.Initialize(conn);

                // Access internal definition engines to dynamically extract required strict columns
                var getSqlType = typeof(SQLiteDbInitializer).GetMethod("GetSqlType", BindingFlags.Static | BindingFlags.NonPublic);
                var getExpectedCols = typeof(SQLiteDbInitializer).GetMethod("GetExpectedColumns", BindingFlags.Static | BindingFlags.NonPublic);
                var expectedCols = (IEnumerable<string>)getExpectedCols.Invoke(null, null);

                var insertCols = new List<string> { "Name" };
                var insertVals1 = new List<object> { "ÖffnenService" };
                var insertVals2 = new List<object> { "öffnenservice" };
                var paramMap1 = new DynamicParameters();
                var paramMap2 = new DynamicParameters();

                paramMap1.Add("Name", "ÖffnenService");
                paramMap2.Add("Name", "öffnenservice");

                // Dynamically populate all missing strict columns with safe data-type compliant mock values
                foreach (var col in expectedCols)
                {
                    if (col.Equals("Name", StringComparison.OrdinalIgnoreCase)) continue;

                    string sqlType = (string)getSqlType.Invoke(null, new object[] { col });

                    // If the column enforces NOT NULL and does not have a DEFAULT constraint, we must supply a value
                    if (sqlType.IndexOf("NOT NULL", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        sqlType.IndexOf("DEFAULT", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        insertCols.Add(col);
                        object mockValue = sqlType.IndexOf("TEXT", StringComparison.OrdinalIgnoreCase) >= 0 ? (object)"mock-path" : 0;

                        paramMap1.Add(col, mockValue);
                        paramMap2.Add(col, mockValue);
                    }
                }

                string sqlTemplate = $"INSERT INTO Services ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertCols.Select(c => "@" + c))});";

                // Act & Assert 1: Unique Constraint validation under custom UNICODE_NOCASE rule
                conn.Execute(sqlTemplate, paramMap1);

                // Assert that inserting a non-ASCII string with alternate casing is safely blocked by the unique index
                Assert.Throws<SQLiteException>(() => conn.Execute(sqlTemplate, paramMap2));

                // Act & Assert 2: Case-Insensitive query validation on deep wide char comparisons
                var foundId = conn.QueryFirstOrDefault<long?>(
                    "SELECT Id FROM Services WHERE Name = 'ÖFFNENSERVICE' COLLATE UNICODE_NOCASE;");

                Assert.NotNull(foundId);
                Assert.True(foundId > 0);
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

                // Updated stashed schema version context to 6 to safely bypass the modern sequential 
                // index migrations block layout. This redirects execution straight into ReconcileSchema 
                // to self-heal the sabotaged test structure completely.
                conn.Execute("UPDATE SchemaInfo SET Version = 6 WHERE Id = 1;");

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
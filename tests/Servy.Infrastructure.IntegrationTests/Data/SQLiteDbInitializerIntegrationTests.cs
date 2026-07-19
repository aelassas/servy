using Dapper;
using Servy.Infrastructure.Data;
using Servy.Testing;
using System.Data.Common;
using System.Data.SQLite;

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

        /// <summary>
        /// Shared helper to seed the SchemaInfo table utilizing the exact, production-grade 
        /// constraint definitions to prevent test configuration drift.
        /// </summary>
        private static void SeedSchemaInfo(DbConnection conn, int version)
        {
            // Create table with production constraints (CHECK constraint for Id and NOT NULL on Version)
            conn.Execute("CREATE TABLE SchemaInfo (Id INTEGER PRIMARY KEY CHECK (Id = 1), Version INTEGER NOT NULL);");
            conn.Execute("INSERT INTO SchemaInfo (Id, Version) VALUES (1, @version);", new { version });
        }

        #region Standard Migrations & Core Branches

        [Fact]
        public void Initialize_FreshDatabase_AppliesAllMigrationsAndReconciles()
        {
            // Arrange
            using (var conn = CreateConnection())
            {
                // Act
                SQLiteDbInitializer.Initialize(conn);

                // Assert
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.Equal(SQLiteDbInitializer.LatestSchemaVersion, version);

                var tables = conn.Query<string>("SELECT name FROM sqlite_master WHERE type='table';").ToList();
                Assert.Contains("Services", tables);
                Assert.Contains("SchemaInfo", tables);

                var columns = conn.Query("PRAGMA table_info(Services);").Select(r => (string)r.name).ToList();
                Assert.Contains("EnableSizeRotation", columns); // Applied by V2
                Assert.Contains("EnableConsoleUI", columns); // Applied by V3
                Assert.Contains("RecoveryOnCleanExit", columns); // Applied by V5

                // Verify the structural index details map directly to the modern COLLATE UNICODE_NOCASE layout rules (Applied by V6)
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
        public void Initialize_OnMigrationFailure_RollsBackTransactionAndRethrows()
        {
            // Arrange: Poison the database to force a SQL exception during ApplyVersion1
            // By creating 'Services' as a VIEW, the subsequent 'CREATE UNIQUE INDEX' on it will throw a SQLiteException.
            using (var conn = CreateConnection())
            {
                SeedSchemaInfo(conn, 0);
                conn.Execute($"CREATE VIEW {SqlConstants.ServicesTableName} AS SELECT 1 AS Id;");

                // Act & Assert
                Assert.Throws<SQLiteException>(() => SQLiteDbInitializer.Initialize(conn));

                // Assert
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
            // Arrange: Simulate an old V0 database using the reflection scaffold helper
            using (var conn = CreateConnection())
            {
                var baseColumns = new List<string> { "Id INTEGER PRIMARY KEY AUTOINCREMENT", "Name TEXT", "EnableRotation INTEGER" };
                var seedData = new Dictionary<string, string>
                {
                    { "Name", "'TestService'" },
                    { "EnableRotation", "1" }
                };

                // Build the structural schema with strict column alignments populated
                var insertCols = CreateLegacyServicesTable(conn, baseColumns, seedData, "Name", "EnableRotation", "EnableSizeRotation");

                // Insert two case-duplicates sequentially (Id 2..3); dedup must keep MIN(Id)=1,
                // so a last-write-wins/MAX(Id) implementation would fail the assertion below.
                var duplicateSeed1 = new Dictionary<string, string>(seedData) { ["Name"] = "'testservice'", ["EnableRotation"] = "0" };
                var duplicateSeed2 = new Dictionary<string, string>(seedData) { ["Name"] = "'TESTSERVICE'", ["EnableRotation"] = "0" };

                InsertLegacyRow(conn, insertCols, duplicateSeed1);
                InsertLegacyRow(conn, insertCols, duplicateSeed2);

                // Create the legacy non-unique index to trigger the index replacement branch
                conn.Execute($"CREATE INDEX idx_services_name_lower ON {SqlConstants.ServicesTableName}(LOWER(Name));");

                // Act
                // Trigger migration, executing MIN(Id) evaluation to clean up duplicates deterministically
                SQLiteDbInitializer.Initialize(conn);

                // Assert
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.True(version >= SQLiteDbInitializer.LatestSchemaVersion, $"Database should be migrated to at least the latest schema version ({SQLiteDbInitializer.LatestSchemaVersion}).");

                // Verify Deduplication (the absolute smallest historical record ID=1 must win)
                var services = conn.Query($"SELECT Id FROM {SqlConstants.ServicesTableName};").ToList();
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

        #endregion

        #region V4 Rebuild & Helper Skip Branches

        [Fact]
        public void Initialize_Version3Database_WithOrphanColumn_PreservesOrphanDataInBackupTable()
        {
            // Arrange: Set DB exactly to V3 state using faithful schema constraints
            using (var conn = CreateConnection())
            {
                SeedSchemaInfo(conn, 3);

                var baseColumns = new List<string> { "Id INTEGER PRIMARY KEY AUTOINCREMENT", "Name TEXT NOT NULL", "OldOrphanData TEXT" };
                var seedData = new Dictionary<string, string>
                {
                    { "Name", "'LegacyAgent'" },
                    { "OldOrphanData", "'CriticalConfigToken_XYZ'" }
                };

                // Dynamically append expected strict NOT NULL columns via scaffold helper
                CreateLegacyServicesTable(conn, baseColumns, seedData, "Name");

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
                var orphanData = conn.QuerySingle($"SELECT Id, OldOrphanData FROM {SqlConstants.ServicesTableName}_orphans_v4;");
                Assert.Equal(1L, (long)orphanData.Id);
                Assert.Equal("CriticalConfigToken_XYZ", (string)orphanData.OldOrphanData);
            }
        }

        [Fact]
        public void MigrationHelpers_AlreadyApplied_SkipsGracefully()
        {
            // Arrange: Set DB to V1 state using faithful schema constraints
            using (var conn = CreateConnection())
            {
                SeedSchemaInfo(conn, 1);

                // Create table with 'EnableSizeRotation' already existing (triggers Rename skip existing branch)
                // Lacks 'EnableRotation' (triggers Rename source missing branch)
                // Has 'RecoveryOnCleanExit' already (triggers AddColumn skip branch)
                conn.Execute($@"
                    CREATE TABLE {SqlConstants.ServicesTableName} (
                        Id INTEGER PRIMARY KEY, 
                        EnableSizeRotation INTEGER,
                        RecoveryOnCleanExit INTEGER
                    );
                ");

                // Act
                SQLiteDbInitializer.Initialize(conn);

                // Assert: The skips should allow the initialization to complete cleanly without throwing SQL syntax errors
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.True(version >= SQLiteDbInitializer.LatestSchemaVersion, $"Database should be migrated to at least the latest schema version ({SQLiteDbInitializer.LatestSchemaVersion}).");
            }
        }

        [Fact]
        public void ApplyVersion2_ExistingOldAndNewColumn_SkipsRename()
        {
            // Arrange: Simulate a weird state where BOTH the old and new columns exist.
            using (var conn = CreateConnection())
            {
                SeedSchemaInfo(conn, 1);
                conn.Execute($"CREATE TABLE {SqlConstants.ServicesTableName} (Id INTEGER PRIMARY KEY, EnableRotation INTEGER, EnableSizeRotation INTEGER);");

                // Act: Invoke ApplyVersion2 directly to bypass V4's destructive rebuild logic
                using (var tx = conn.BeginTransaction())
                {
                    TestReflection.InvokeNonPublicStatic(typeof(SQLiteDbInitializer), "ApplyVersion2", conn, tx);
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
            // Arrange: Initialize a clean baseline up to Version 5 state using faithful schema constraints
            using (var conn = CreateConnection())
            {
                SeedSchemaInfo(conn, 5);

                var baseColumns = new List<string> { "Id INTEGER PRIMARY KEY AUTOINCREMENT", "Name TEXT" };
                var seedData = new Dictionary<string, string> { { "Name", "'Alpha-Service'" } };

                // Construct a valid pre-v6 table layout using the centralized factory
                var insertCols = CreateLegacyServicesTable(conn, baseColumns, seedData, "Name");

                // Setup the old functional index as NON-UNIQUE so it permits the insert of casing variations on legacy systems.
                conn.Execute($"CREATE INDEX idx_services_name_lower ON {SqlConstants.ServicesTableName}(LOWER(Name));");

                // Seed duplicate rows out of chronological order to check oldest historical match selection (MIN(Id) resolution)
                var duplicateSeed = new Dictionary<string, string>(seedData) { ["Name"] = "'alpha-service'" };
                InsertLegacyRow(conn, insertCols, duplicateSeed);

                // Act: Trigger initialization to catch version 5 -> 6 transition pipeline branch
                SQLiteDbInitializer.Initialize(conn);

                // Assert
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.Equal(SQLiteDbInitializer.LatestSchemaVersion, version);

                // Verify table deduplication pass: only the oldest instance (Id = 1) survives the constraint cleanup
                var remainingServices = conn.Query($"SELECT Id, Name FROM {SqlConstants.ServicesTableName};").ToList();
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
            // Arrange: Initialize baseline up to Version 5 state using faithful schema constraints
            using (var conn = CreateConnection())
            {
                SeedSchemaInfo(conn, 5);

                var baseColumns = new List<string> { "Id INTEGER PRIMARY KEY AUTOINCREMENT", "Name TEXT" };
                var seedData = new Dictionary<string, string> { { "Name", "'Ä-Service'" } };

                var insertCols = CreateLegacyServicesTable(conn, baseColumns, seedData, "Name");
                conn.Execute($"CREATE INDEX idx_services_name_lower ON {SqlConstants.ServicesTableName}(LOWER(Name));");

                // Seed duplicate rows utilizing wide non-ASCII variants out of case parity
                var duplicateSeed = new Dictionary<string, string>(seedData) { ["Name"] = "'ä-service'" };
                InsertLegacyRow(conn, insertCols, duplicateSeed);

                // Act
                SQLiteDbInitializer.Initialize(conn);

                // Assert: Verify UNICODE_NOCASE successfully group-collapsed and purged the duplicate non-ASCII character entries
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.Equal(SQLiteDbInitializer.LatestSchemaVersion, version);

                var remainingServices = conn.Query($"SELECT Id, Name FROM {SqlConstants.ServicesTableName};").ToList();
                Assert.Single(remainingServices);
                Assert.Equal(1L, (long)remainingServices[0].Id);
                Assert.Equal("Ä-Service", (string)remainingServices[0].Name);

                // Verify the structural unique index details map directly to the modern COLLATE UNICODE_NOCASE configuration rules
                var indexList = conn.Query($"PRAGMA index_list('{SqlConstants.ServicesTableName}');")
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
        public void UnicodeNoCaseCollation_InsertsAndQueriesNonAsciiCasing_EnforcesUniqueness()
        {
            // Arrange: Execute complete initialization runner to build schema and spin custom collations up
            using (var conn = CreateConnection())
            {
                SQLiteDbInitializer.Initialize(conn);

                // Access internal definition engines seamlessly via centralized test reflection helper
                var expectedCols = (IEnumerable<string>)TestReflection.InvokeNonPublicStatic(typeof(SQLiteDbInitializer), "GetExpectedColumns")!;

                var insertCols = new List<string> { "Name" };
                var paramMap1 = new DynamicParameters();
                var paramMap2 = new DynamicParameters();

                paramMap1.Add("Name", "ÖffnenService");
                paramMap2.Add("Name", "öffnenservice");

                // Dynamically populate all missing strict columns with safe data-type compliant mock values
                foreach (var col in expectedCols)
                {
                    if (col.Equals("Name", StringComparison.OrdinalIgnoreCase)) continue;

                    string sqlType = (string)TestReflection.InvokeNonPublicStatic(typeof(SQLiteDbInitializer), "GetSqlType", col)!;

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

                string sqlTemplate = $"INSERT INTO {SqlConstants.ServicesTableName} ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertCols.Select(c => "@" + c))});";

                // Act & Assert 1: Unique Constraint validation under custom UNICODE_NOCASE rule
                conn.Execute(sqlTemplate, paramMap1);

                // Assert that inserting a non-ASCII string with alternate casing is safely blocked by the unique index
                Assert.Throws<SQLiteException>(() => conn.Execute(sqlTemplate, paramMap2));

                // Act & Assert 2: Case-Insensitive query validation on deep wide char comparisons
                var foundId = conn.QueryFirstOrDefault<long?>(
                    $"SELECT Id FROM {SqlConstants.ServicesTableName} WHERE Name = 'ÖFFNENSERVICE' COLLATE UNICODE_NOCASE;");

                Assert.NotNull(foundId);
                Assert.True(foundId > 0);
            }
        }

        #endregion

        #region Reconciliation Self-Healing (Missing, Orphans, Mismatches)

        [Fact]
        public void ReconcileSchema_WithMissingOrphanAndMismatchedColumns_Heals()
        {
            // Arrange
            using (var conn = CreateConnection())
            {
                // Step 1: Perform a full baseline initialization to get the perfect expected schema.
                SQLiteDbInitializer.Initialize(conn);
                var expectedColumns = conn.Query("PRAGMA table_info(Services);").Select(r => (string)r.name).ToList();

                // Step 2: Sabotage the schema
                conn.Execute($"DROP TABLE {SqlConstants.ServicesTableName};");

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

                conn.Execute($"CREATE TABLE {SqlConstants.ServicesTableName} ({string.Join(", ", corruptedTableDef)});");

                // Updated stashed schema version context to the absolute maximum single source of truth value.
                // This reliably redirects execution straight into ReconcileSchema to self-heal the sabotaged test structure completely.
                conn.Execute($"UPDATE SchemaInfo SET Version = {SQLiteDbInitializer.LatestSchemaVersion} WHERE Id = 1;");

                // Act - Run Initialize again
                SQLiteDbInitializer.Initialize(conn);

                // Assert 
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

        #region Reflection Error Trapping & Scaffold Helpers

        [Fact]
        public void GetSqlType_MissingColumn_ThrowsInvalidOperationException()
        {
            // Arrange & Act & Assert
            // TestReflection natively handles unwrapping TargetInvocationException contexts cleanly on static hooks
            var ex = Assert.Throws<InvalidOperationException>(() =>
                TestReflection.InvokeNonPublicStatic(typeof(SQLiteDbInitializer), "GetSqlType", "NonExistentMagicalColumn_12345"));

            // Assert
            Assert.Contains("lacks an [SqlColumn] attribute", ex.Message);
        }

        /// <summary>
        /// Shared abstraction builder to securely initialize legacy table instances 
        /// while automatically aligning strict, un-seeded NOT NULL constraints dynamically.
        /// </summary>
        private static List<string> CreateLegacyServicesTable(
            DbConnection conn,
            List<string> colDefs,
            Dictionary<string, string> seedData,
            params string[] skipColumns)
        {
            // Arrange & Act
            var expectedCols = (IEnumerable<string>)TestReflection.InvokeNonPublicStatic(typeof(SQLiteDbInitializer), "GetExpectedColumns")!;
            var insertCols = seedData.Keys.ToList();
            var insertVals = seedData.Values.ToList();

            foreach (var col in expectedCols)
            {
                if (skipColumns.Contains(col, StringComparer.OrdinalIgnoreCase))
                    continue;

                string sqlType = (string)TestReflection.InvokeNonPublicStatic(typeof(SQLiteDbInitializer), "GetSqlType", col)!;
                if (sqlType.IndexOf("NOT NULL", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    sqlType.IndexOf("DEFAULT", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    colDefs.Add($"{col} {sqlType}");
                    insertCols.Add(col);
                    string defaultSeedLiteral = sqlType.IndexOf("TEXT", StringComparison.OrdinalIgnoreCase) >= 0 ? "''" : "0";
                    insertVals.Add(defaultSeedLiteral);
                }
            }

            // Generate physical table layout and inject first historical baseline row context
            conn.Execute($"CREATE TABLE {SqlConstants.ServicesTableName} ({string.Join(", ", colDefs)});");
            conn.Execute($"INSERT INTO {SqlConstants.ServicesTableName} ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertVals)});");

            // Assert
            return insertCols;
        }

        /// <summary>
        /// Formats and pushes secondary duplicate entries safely leveraging mapped columns tracking templates.
        /// </summary>
        private static void InsertLegacyRow(DbConnection conn, List<string> insertCols, Dictionary<string, string> dynamicSeed)
        {
            var valuesRow = insertCols.Select(col => dynamicSeed.ContainsKey(col) ? dynamicSeed[col] : "0").ToList();
            conn.Execute($"INSERT INTO {SqlConstants.ServicesTableName} ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", valuesRow)});");
        }

        #endregion
    }
}
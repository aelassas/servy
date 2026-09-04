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

                // Assert Version 7 Heartbeat columns exist on a fresh installation
                Assert.Contains("HeartbeatUrl", columns);
                Assert.Contains("HeartbeatUrlTimeoutSeconds", columns);
                Assert.Contains("EnableHeartbeatUrlFlags", columns);

                // Assert Version 8 CpuAffinity column exists on a fresh installation
                Assert.Contains("CpuAffinity", columns);

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
                var context = CreateLegacyServicesTable(conn, baseColumns, seedData, "Name", "EnableRotation", "EnableSizeRotation");

                // Insert two case-duplicates sequentially (Id 2..3); dedup must keep MIN(Id)=1,
                // so a last-write-wins/MAX(Id) implementation would fail the assertion below.
                var duplicateSeed1 = new Dictionary<string, string>(seedData) { ["Name"] = "'testservice'", ["EnableRotation"] = "0" };
                var duplicateSeed2 = new Dictionary<string, string>(seedData) { ["Name"] = "'TESTSERVICE'", ["EnableRotation"] = "0" };

                InsertLegacyRow(conn, context, duplicateSeed1);
                InsertLegacyRow(conn, context, duplicateSeed2);

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
        public void Initialize_Version3Database_WithUserIndexAndTrigger_RestoresDependentsPostRebuild()
        {
            // Arrange: Seed V3 database and attach custom index and trigger to Services table
            using (var conn = CreateConnection())
            {
                SeedSchemaInfo(conn, 3);

                var baseColumns = new List<string> { "Id INTEGER PRIMARY KEY AUTOINCREMENT", "Name TEXT NOT NULL", "DisplayName TEXT" };
                var seedData = new Dictionary<string, string>
                {
                    { "Name", "'ServiceWithDependents'" },
                    { "DisplayName", "'Display Service'" }
                };

                CreateLegacyServicesTable(conn, baseColumns, seedData, "Name", "DisplayName");

                // Attach custom user index and trigger to verify snapshot and restore pipeline during ApplyVersion4
                conn.Execute($"CREATE INDEX idx_custom_displayname ON {SqlConstants.ServicesTableName}(DisplayName);");
                conn.Execute($@"
                    CREATE TRIGGER trg_custom_after_update
                    AFTER UPDATE ON {SqlConstants.ServicesTableName}
                    BEGIN
                        SELECT 1;
                    END;");

                // Act
                SQLiteDbInitializer.Initialize(conn);

                // Assert
                var dependents = conn.Query<(string Type, string Name)>("SELECT type, name FROM sqlite_master WHERE tbl_name='Services' AND type IN ('index', 'trigger');").ToList();

                Assert.Contains(dependents, d => d.Type == "index" && d.Name == "idx_custom_displayname");
                Assert.Contains(dependents, d => d.Type == "trigger" && d.Name == "trg_custom_after_update");
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

        #region V6 Explicit collation index (Name COLLATE UNICODE_NOCASE)

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
                var context = CreateLegacyServicesTable(conn, baseColumns, seedData, "Name");

                // Setup the old functional index as NON-UNIQUE so it permits the insert of casing variations on legacy systems.
                conn.Execute($"CREATE INDEX idx_services_name_lower ON {SqlConstants.ServicesTableName}(LOWER(Name));");

                // Seed one case-duplicate after the baseline row (Ids 1 then 2); dedup must keep MIN(Id)=1,
                // so a last-write-wins/MAX(Id) implementation would fail the assertion below.
                var duplicateSeed = new Dictionary<string, string>(seedData) { ["Name"] = "'alpha-service'" };
                InsertLegacyRow(conn, context, duplicateSeed);

                // Act: Trigger initialization to catch version 5 -> 6 transition pipeline branch
                SQLiteDbInitializer.Initialize(conn);

                // Assert
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.True(version >= 6);

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

                var context = CreateLegacyServicesTable(conn, baseColumns, seedData, "Name");
                conn.Execute($"CREATE INDEX idx_services_name_lower ON {SqlConstants.ServicesTableName}(LOWER(Name));");

                // Seed duplicate rows utilizing wide non-ASCII variants out of case parity
                var duplicateSeed = new Dictionary<string, string>(seedData) { ["Name"] = "'ä-service'" };
                InsertLegacyRow(conn, context, duplicateSeed);

                // Act
                SQLiteDbInitializer.Initialize(conn);

                // Assert: Verify UNICODE_NOCASE successfully group-collapsed and purged the duplicate non-ASCII character entries
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.True(version >= 6);

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

        #region V7 External Heartbeat Migration Branches

        [Fact]
        public void ApplyVersion7_UpgradesFromVersion6_AppendsHeartbeatColumnsCleanly()
        {
            // Arrange: Establish schema explicitly at target Version 6 configuration checkpoint
            using (var conn = CreateConnection())
            {
                SeedSchemaInfo(conn, 6);

                // Build a pristine pre-v7 database using modern collation logic
                var baseColumns = new List<string> { "Id INTEGER PRIMARY KEY AUTOINCREMENT", "Name TEXT COLLATE UNICODE_NOCASE NOT NULL" };
                var seedData = new Dictionary<string, string> { { "Name", "'HeartbeatMonitoredApp'" } };

                CreateLegacyServicesTable(conn, baseColumns, seedData, "Name");

                // Act: Run full initialization loop to trigger the V6 -> V7 ApplyVersion7 schema migration pipeline
                SQLiteDbInitializer.Initialize(conn);

                // Assert
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.Equal(SQLiteDbInitializer.LatestSchemaVersion, version);

                var columns = conn.Query("PRAGMA table_info(Services);").Select(r => (string)r.name).ToList();

                // Confirm the structural migration successfully appended the specific external heartbeat properties
                Assert.Contains("HeartbeatUrl", columns);
                Assert.Contains("HeartbeatUrlTimeoutSeconds", columns);
                Assert.Contains("EnableHeartbeatUrlFlags", columns);

                // Verify that default values for the fresh migration columns resolve safely to NULL for historical records
                var migratedRow = conn.QuerySingle($"SELECT HeartbeatUrl, HeartbeatUrlTimeoutSeconds, EnableHeartbeatUrlFlags FROM {SqlConstants.ServicesTableName} WHERE Id = 1;");
                Assert.Null(migratedRow.HeartbeatUrl);
                Assert.Null(migratedRow.HeartbeatUrlTimeoutSeconds);
                Assert.Null(migratedRow.EnableHeartbeatUrlFlags);
            }
        }

        #endregion

        #region V8 CPU Affinity Migration Branches

        [Fact]
        public void ApplyVersion8_UpgradesFromVersion7_AppendsCpuAffinityColumnCleanly()
        {
            // Arrange: Establish schema explicitly at target Version 7 configuration checkpoint
            using (var conn = CreateConnection())
            {
                SeedSchemaInfo(conn, 7);

                // Build a pristine pre-v8 database using modern collation logic
                var baseColumns = new List<string> { "Id INTEGER PRIMARY KEY AUTOINCREMENT", "Name TEXT COLLATE UNICODE_NOCASE NOT NULL" };
                var seedData = new Dictionary<string, string> { { "Name", "'CpuAffinityMonitoredApp'" } };

                CreateLegacyServicesTable(conn, baseColumns, seedData, "Name");

                // Act: Run full initialization loop to trigger the V7 -> V8 ApplyVersion8 schema migration pipeline
                SQLiteDbInitializer.Initialize(conn);

                // Assert
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.Equal(SQLiteDbInitializer.LatestSchemaVersion, version);

                var columns = conn.Query("PRAGMA table_info(Services);").Select(r => (string)r.name).ToList();

                // Confirm the structural migration successfully appended the CpuAffinity property
                Assert.Contains("CpuAffinity", columns);

                // Verify that default values for the fresh migration column resolve safely to NULL for historical records
                var migratedRow = conn.QuerySingle($"SELECT CpuAffinity FROM {SqlConstants.ServicesTableName} WHERE Id = 1;");
                Assert.Null(migratedRow.CpuAffinity);
            }
        }

        #endregion

        #region V9 UserAccount Normalization Migration Branches

        [Fact]
        public void ApplyVersion9_UpgradesFromVersion8_TrimsPaddedUserAccountsAndLeavesCleanValuesIntact()
        {
            // Arrange: Establish schema explicitly at Version 8 configuration checkpoint
            using (var conn = CreateConnection())
            {
                SeedSchemaInfo(conn, 8);

                var baseColumns = new List<string> { "Id INTEGER PRIMARY KEY AUTOINCREMENT", "Name TEXT COLLATE UNICODE_NOCASE NOT NULL", "UserAccount TEXT" };
                var seedData = new Dictionary<string, string> { { "Name", "'AppWithPaddedAccount'" }, { "UserAccount", "'  domain\\svc_account  '" } };

                var context = CreateLegacyServicesTable(conn, baseColumns, seedData, "Name", "UserAccount");

                // Seed row 2 with clean UserAccount value
                InsertLegacyRow(conn, context, new Dictionary<string, string> { { "Name", "'AppWithCleanAccount'" }, { "UserAccount", "'domain\\clean_svc'" } });

                // Seed row 3 with NULL UserAccount value
                InsertLegacyRow(conn, context, new Dictionary<string, string> { { "Name", "'AppWithNullAccount'" }, { "UserAccount", "NULL" } });

                // Act: Run initialization to trigger V8 -> V9 migration
                SQLiteDbInitializer.Initialize(conn);

                // Assert
                var version = conn.QuerySingle<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
                Assert.Equal(SQLiteDbInitializer.LatestSchemaVersion, version);

                var rows = conn.Query($"SELECT Id, Name, UserAccount FROM {SqlConstants.ServicesTableName} ORDER BY Id;").ToList();
                Assert.Equal(3, rows.Count);

                // Row 1: Whitespace-padded UserAccount should be normalized/trimmed
                Assert.Equal("domain\\svc_account", (string)rows[0].UserAccount);

                // Row 2: Clean UserAccount remains unchanged
                Assert.Equal("domain\\clean_svc", (string)rows[1].UserAccount);

                // Row 3: NULL UserAccount remains NULL
                Assert.Null(rows[2].UserAccount);
            }
        }

        #endregion

        #region Legacy Whitespace Zombie Detection

        [Fact]
        public void Initialize_WhitespacePaddedZombieAndTwin_ExecutesDetectorWithoutDeletingData()
        {
            // Arrange: Initialize schema to latest version and seed both clean and padded rows directly
            using (var conn = CreateConnection())
            {
                SQLiteDbInitializer.Initialize(conn);

                // Insert clean row 'zombietest' and whitespace-padded row ' zombietest ' with required NOT NULL columns
                conn.Execute($"INSERT INTO {SqlConstants.ServicesTableName} (Name, ExecutablePath) VALUES ('zombietest', 'C:\\path\\exe');");
                conn.Execute($"INSERT INTO {SqlConstants.ServicesTableName} (Name, ExecutablePath) VALUES (' zombietest ', 'C:\\path\\exe');");

                // Act: Re-run Initialize on an existing database containing padded zombie collisions
                SQLiteDbInitializer.Initialize(conn);

                // Assert: Verify detector scan leaves both records intact and unharmed in the database
                var names = conn.Query<string>($"SELECT Name FROM {SqlConstants.ServicesTableName} WHERE Name LIKE '%zombietest%';").ToList();

                Assert.Equal(2, names.Count);
                Assert.Contains("zombietest", names);
                Assert.Contains(" zombietest ", names);
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

                // Explicitly target 'Name' as the omitted column to exercise the missing-column reconciliation branch.
                const string missingColumn = "Name";
                Assert.Contains(missingColumn, expectedColumns);

                // Rebuild the table intentionally omitting 'Name', changing 'EnableSizeRotation' to TEXT (type mismatch),
                // and adding an 'OrphanColumn' (orphan branch).
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
        /// Encapsulates the columns and type-aware padding map generated when constructing a legacy table.
        /// </summary>
        private sealed class LegacyTableContext
        {
            public List<string> InsertCols { get; }
            public Dictionary<string, string> Padding { get; }

            public LegacyTableContext(List<string> insertCols, Dictionary<string, string> padding)
            {
                InsertCols = insertCols;
                Padding = padding;
            }
        }

        /// <summary>
        /// Creates a legacy Services table from <paramref name="colDefs"/> and inserts one seed row,
        /// padding any expected NOT NULL column that has no DEFAULT and is not in <paramref name="skipColumns"/>.
        /// Returns a <see cref="LegacyTableContext"/> containing the column list and padding map for reuse by <see cref="InsertLegacyRow"/>.
        /// </summary>
        private static LegacyTableContext CreateLegacyServicesTable(
            DbConnection conn,
            List<string> colDefs,
            Dictionary<string, string> seedData,
            params string[] skipColumns)
        {
            var expectedCols = (IEnumerable<string>)TestReflection.InvokeNonPublicStatic(typeof(SQLiteDbInitializer), "GetExpectedColumns")!;
            var insertCols = seedData.Keys.ToList();
            var insertVals = seedData.Values.ToList();
            var padding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
                    padding[col] = defaultSeedLiteral;
                }
            }

            // Generate physical table layout and inject first historical baseline row context
            conn.Execute($"CREATE TABLE {SqlConstants.ServicesTableName} ({string.Join(", ", colDefs)});");
            conn.Execute($"INSERT INTO {SqlConstants.ServicesTableName} ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertVals)});");

            return new LegacyTableContext(insertCols, padding);
        }

        /// <summary>
        /// Inserts an additional row into the legacy Services table using the column list and padding map from
        /// <see cref="CreateLegacyServicesTable"/>, overriding values present in <paramref name="dynamicSeed"/>.
        /// </summary>
        private static void InsertLegacyRow(DbConnection conn, LegacyTableContext context, Dictionary<string, string> dynamicSeed)
        {
            var valuesRow = context.InsertCols
                .Select(col => dynamicSeed.TryGetValue(col, out var val) ? val : context.Padding.TryGetValue(col, out var padVal) ? padVal : "0")
                .ToList();

            conn.Execute($"INSERT INTO {SqlConstants.ServicesTableName} ({string.Join(", ", context.InsertCols)}) VALUES ({string.Join(", ", valuesRow)});");
        }

        #endregion
    }
}

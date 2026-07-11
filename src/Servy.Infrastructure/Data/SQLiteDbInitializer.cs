using Dapper;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Linq;
using System.Reflection;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Provides helper methods to initialize the SQLite database schema for Servy.
    /// Tracks database versions and applies sequential migrations dynamically based on central constants.
    /// </summary>
    public static class SQLiteDbInitializer
    {
        /// <summary>
        /// Single Source of Truth for the absolute latest schema migration version sequence.
        /// </summary>
        public const int LatestSchemaVersion = 6;

        private static readonly char[] SplitWhitespaceChars = { ' ', '\t' };

        // Static Cache for O(1) lookups and zero reflection overhead during database migrations.
        // It guarantees the DTO is the Single Source of Truth for schema data types.
        private static readonly Dictionary<string, string> SqlTypeMap = BuildSqlTypeMap();

        /// <summary>
        /// Reflects over ServiceDto once at startup to cache the [SqlColumn] mappings.
        /// </summary>
        private static Dictionary<string, string> BuildSqlTypeMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in typeof(ServiceDto).GetProperties())
            {
                var attr = prop.GetCustomAttribute<SqlColumnAttribute>();
                if (attr != null)
                {
                    // Canonicalize to Upper Case to ensure consistent DDL generation
                    map[prop.Name] = attr.SqlType.ToUpperInvariant();
                }
            }

            return map;
        }

        /// <summary>
        /// Analyzes the current database state, applies necessary migrations, 
        /// and ensures all schema requirements match the current application version.
        /// </summary>
        /// <param name="connection">An open database connection to execute commands on.</param>
        public static void Initialize(DbConnection connection)
        {
            // Register the collation sequence BEFORE any DDL or PRAGMA is sent to the SQLite engine.
            // This prevents immediate engine parsing errors if the index already exists on disk from a prior application run.
            SQLiteFunction.RegisterFunction(typeof(UnicodeNoCaseCollation));

            // --- INTENTIONAL ROBUSTNESS SAFETY CHECK ---
            // Scan and report legacy whitespace-padded rows on every initialization pass instead of dropping them.
            // Because unique indexes like 'idx_services_name_unique' permit 'foo' and ' foo ' to coexist under COLLATE UNICODE_NOCASE,
            // lookups that automatically trim keys first will hit the clean row. This satisfies the fallback evaluation predicate 
            // of the read path, which causes the verbatim secondary check to never fire-leaving the padded legacy variant masked and unreachable.
            try
            {
                var legacyCollisions = connection.Query($@"
                    SELECT a.Id AS ZombieId, a.Name AS ZombieName, b.Id AS TwinId, b.Name AS TwinName
                    FROM {SqlConstants.ServicesTableName} a
                    JOIN {SqlConstants.ServicesTableName} b ON TRIM(a.Name) = b.Name COLLATE UNICODE_NOCASE
                    WHERE a.Name <> TRIM(a.Name);").ToList();

                foreach (var collision in legacyCollisions)
                {
                    Logger.Warn($"CRITICAL DATA LIFECYCLE ANOMALY: Legacy whitespace-padded service record '{collision.ZombieName}' (ID {collision.ZombieId}) " +
                                $"coexists with clean twin '{collision.TwinName}' (ID {collision.TwinId}). The padded row is preserved but masked, and remains unreachable by name-based actions.");
                }
            }
            catch (Exception ex)
            {
                // Explicitly check the master schema to distinguish between a benign first-boot 
                // initialization and a genuine runtime query failure.
                if (!TableExists(connection, SqlConstants.ServicesTableName, transaction: null))
                {
                    Logger.Debug($"Defensive validation padding check skipped because the base {SqlConstants.ServicesTableName} table layout has not been instantiated yet.", ex);
                }
                else
                {
                    // Escalated to Warn because the table exists, meaning an infrastructure anomaly 
                    // (e.g., missing UNICODE_NOCASE collation, DB lock, corrupt file) silently disabled the detector.
                    Logger.Warn($"Legacy whitespace-padding anomaly scan failed to execute against the active {SqlConstants.ServicesTableName} table; zombie rows may go undetected this boot.", ex);
                }
            }

            // Disable foreign keys temporarily for the duration of the migration process.
            // SQLite requires this to be set OUTSIDE of an active transaction for table-rebuilds (v4) to work.
            connection.Execute("PRAGMA foreign_keys=OFF;");

            try
            {
                // 1. Ensure the version tracking table exists. 
                // The CHECK constraint guarantees only a single row (Id = 1) can ever exist.
                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS SchemaInfo (
                        Id INTEGER PRIMARY KEY CHECK (Id = 1), 
                        Version INTEGER NOT NULL
                    );");

                using (var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        // 2. Force an immediate write lock (RESERVED state) to prevent multi-process races.
                        // This behaves exactly like `BEGIN IMMEDIATE` but strictly honors the ADO.NET transaction scope.
                        connection.Execute("INSERT OR IGNORE INTO SchemaInfo (Id, Version) VALUES (1, 0);", transaction: transaction);
                        connection.Execute("UPDATE SchemaInfo SET Version = Version WHERE Id = 1;", transaction: transaction);

                        // 3. Get the current database version
                        int currentVersion = GetSchemaVersion(connection, transaction);

                        // 4. Backward Compatibility: Handle unversioned legacy databases
                        if (currentVersion == 0 && TableExists(connection, SqlConstants.ServicesTableName, transaction))
                        {
                            UpgradeLegacyDatabaseToVersion1(connection, transaction);
                            UpdateSchemaVersion(connection, 1, transaction);
                            currentVersion = 1;
                        }

                        // 5. Sequential Migration Runner
                        if (currentVersion < 1)
                        {
                            ApplyVersion1(connection, transaction);
                            UpdateSchemaVersion(connection, 1, transaction);
                            currentVersion = 1; // Kept for future migration logic chaining
                        }

                        // Version 2 Migration to rename the ambiguous EnableRotation column
                        if (currentVersion < 2)
                        {
                            ApplyVersion2(connection, transaction);
                            UpdateSchemaVersion(connection, 2, transaction);
                            currentVersion = 2;
                        }

                        // Version 3 Migration to add EnableConsoleUI for interactive console apps support
                        if (currentVersion < 3)
                        {
                            ApplyVersion3(connection, transaction);
                            UpdateSchemaVersion(connection, 3, transaction);
                            currentVersion = 3;
                        }

                        // Version 4 Migration to drop strict NOT NULL constraints, aligning the schema with ServiceDto
                        if (currentVersion < 4)
                        {
                            ApplyVersion4(connection, transaction);
                            UpdateSchemaVersion(connection, 4, transaction);
                            currentVersion = 4;
                        }

                        // Version 5 Migration to add RecoveryOnCleanExit column to support triggering recovery on successful exits
                        if (currentVersion < 5)
                        {
                            ApplyVersion5(connection, transaction);
                            UpdateSchemaVersion(connection, 5, transaction);
                            currentVersion = 5;
                        }

                        // Version 6 Migration to upgrade the unique index to a Unicode-aware case-insensitive collation (COLLATE UNICODE_NOCASE)
                        if (currentVersion < 6)
                        {
                            ApplyVersion6(connection, transaction);
                            UpdateSchemaVersion(connection, 6, transaction);
                            currentVersion = 6;
                        }

                        // --- FUTURE MIGRATIONS GO HERE ---
                        // if (currentVersion < 7) { ... }

                        // Double check that the final tracked migration index completely aligns with the central declaration
                        if (currentVersion != LatestSchemaVersion)
                        {
                            Logger.Warn($"Migration chain sync warning: Current database version is {currentVersion}, but LatestSchemaVersion is defined as {LatestSchemaVersion}. Ensure future migration blocks are properly wired.");
                        }

                        // 6. Reconciliation safety net
                        // Ensures that any columns added to SqlConstants but missed in migrations are applied.
                        ReconcileSchema(connection, currentVersion, transaction);

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Logger.Error("CRITICAL: Database schema initialization failed. Transaction rolled back to prevent corruption.", ex);
                        throw;
                    }
                }
            }
            finally
            {
                // Always re-enable FK enforcement, even on failure, so the pooled
                // connection is returned in a known-good state.
                try { connection.Execute("PRAGMA foreign_keys=ON;"); }
                catch (Exception ex) { Logger.Error("Failed to restore foreign_keys=ON after migration.", ex); }
            }
        }

        /// <summary>
        /// Final reconciliation step that ensures missing columns from the
        /// Single Source of Truth (SqlConstants) are added to the schema. 
        /// Detects and logs type drift and orphan columns for manual review.
        /// </summary>
        /// <param name="connection">The active database connection.</param>
        /// <param name="currentVersion">The schema version detected after migrations.</param>
        /// <param name="transaction">The active atomic transaction.</param>
        private static void ReconcileSchema(DbConnection connection, int currentVersion, DbTransaction transaction)
        {
            // Fetch full column definitions (name and type) from PRAGMA
            var tableInfo = connection.Query($"PRAGMA table_info({SqlConstants.ServicesTableName});", transaction: transaction).ToList();

            // DRY PASS: Derive hash set from raw metadata rows directly to save an extra query round-trip
            var existingColumns = new HashSet<string>(
                tableInfo.Select(row => (string)row.name),
                StringComparer.OrdinalIgnoreCase);

            // GetExpectedColumns returns every column in SqlConstants.InsertColumns
            // (Name + StandardColumns + PreviousStopTimeout). Only the Id primary key
            // is absent, so add it explicitly to avoid flagging it as an orphan.
            var expectedColumns = new HashSet<string>(GetExpectedColumns(), StringComparer.OrdinalIgnoreCase);
            var coreKeys = new[] { "Id" };
            foreach (var key in coreKeys)
            {
                expectedColumns.Add(key);
            }

            // 1. Detect and Add Missing Columns (Auto-add only)
            var missing = expectedColumns.Where(c => !existingColumns.Contains(c)).ToList();

            if (missing.Count > 0)
            {
                Logger.Warn($"Single-Source-of-Truth drift detected at SchemaVersion={currentVersion}. Adding missing columns: {string.Join(", ", missing)}");

                foreach (var col in missing)
                {
                    // ROBUSTNESS: Ensure column definitions satisfy SQLite ALTER TABLE structural constraints
                    string baseDefinition = EnsureAlterableDefinition(GetSqlType(col));

                    // Core keys shouldn't hit this path dynamically, but we use GetSqlType for standard columns
                    connection.Execute($"ALTER TABLE {SqlConstants.ServicesTableName} ADD COLUMN {col} {baseDefinition};", transaction: transaction);
                    Logger.Info($"Self-healed column: {col} with schema context '{baseDefinition}'");
                }
            }

            // 2. Detect Orphaned Columns (Present in DB, but not in SqlConstants)
            var orphans = existingColumns
                .MakeOrphanFilter(expectedColumns)
                .ToList();

            if (orphans.Count > 0)
            {
                Logger.Warn($"Schema drift: Orphan columns detected (present in DB, but not in SqlConstants). These consume disk space and may indicate a missed migration for a renamed column: {string.Join(", ", orphans)}");
            }

            // 3. Detect Type Mismatches
            foreach (var row in tableInfo)
            {
                string colName = (string)row.name;
                string dbBaseType = (string)row.type;

                // SQLite returns 1 for NOT NULL and 0 for nullable
                bool dbNotNull = Convert.ToInt64(row.notnull) == 1;

                // Only check types for columns managed by GetSqlType (StandardColumns)
                if (expectedColumns.Contains(colName) && !coreKeys.Contains(colName, StringComparer.OrdinalIgnoreCase))
                {
                    string expectedFullType = GetSqlType(colName); // e.g., "TEXT NOT NULL" or "INTEGER DEFAULT 0"

                    // Parse the expected base type and nullability
                    bool expectedNotNull = expectedFullType.IndexOf("NOT NULL", StringComparison.OrdinalIgnoreCase) >= 0;

                    // ROBUSTNESS: Isolate the core data type affinity token from complex metadata extensions (DEFAULT, CHECK, COLLATE, etc.)
                    // PRAGMA table_info().type only returns the bare data type descriptor (e.g. "TEXT", "INTEGER").
                    // Extracting the first whitespace-delimited word prevents false-positive warnings.
                    string expectedAffinity = expectedFullType.Split(SplitWhitespaceChars, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

                    if (!string.Equals(dbBaseType, expectedAffinity, StringComparison.OrdinalIgnoreCase) || dbNotNull != expectedNotNull)
                    {
                        // Reconstruct the actual DB state for a clear log message
                        string actualDbDesc = $"{dbBaseType}{(dbNotNull ? " NOT NULL" : "")}";
                        Logger.Warn($"Schema drift: Type mismatch for column '{colName}'. DB is '{actualDbDesc}', but SqlConstants expects '{expectedFullType}'.");
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves the current schema version from the tracking table.
        /// </summary>
        /// <param name="connection">The active database connection.</param>
        /// <param name="transaction">The active atomic transaction.</param>
        /// <returns>The current version integer, or 0 if the table is empty.</returns>
        private static int GetSchemaVersion(DbConnection connection, DbTransaction transaction)
        {
            return connection.QueryFirstOrDefault<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;", transaction: transaction);
        }

        /// <summary>
        /// Upserts the schema version into the tracking table, ensuring only one row exists.
        /// </summary>
        /// <param name="connection">The active database connection.</param>
        /// <param name="version">The new schema version to record.</param>
        /// <param name="transaction">The active atomic transaction.</param>
        private static void UpdateSchemaVersion(DbConnection connection, int version, DbTransaction transaction)
        {
            var sql = @"
                INSERT INTO SchemaInfo (Id, Version) VALUES (1, @Version)
                ON CONFLICT(Id) DO UPDATE SET Version = excluded.Version;";

            connection.Execute(sql, new { Version = version }, transaction: transaction);
        }

        /// <summary>
        /// Checks if a specific table exists within the SQLite master schema.
        /// </summary>
        /// <param name="connection">The active database connection.</param>
        /// <param name="tableName">The exact name of the table to look for.</param>
        /// <param name="transaction">The active atomic transaction.</param>
        /// <returns><c>true</c> if the table exists; otherwise, <c>false</c>.</returns>
        private static bool TableExists(DbConnection connection, string tableName, DbTransaction transaction)
        {
            var result = connection.QueryFirstOrDefault<int>(
                "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@TableName;",
                new { TableName = tableName },
                transaction: transaction);

            return result == 1;
        }

        /// <summary>
        /// Retrieves the expected columns dynamically from the Single Source of Truth (SqlConstants).
        /// </summary>
        /// <returns>An enumerable of trimmed column names.</returns>
        private static IEnumerable<string> GetExpectedColumns()
        {
            return SqlConstants.InsertColumns
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim());
        }

        /// <summary>
        /// Queries the SQLite engine schema to map and return all current structural column fields for the Services table.
        /// </summary>
        private static HashSet<string> GetExistingColumnNames(DbConnection connection, DbTransaction transaction)
        {
            return new HashSet<string>(
                connection.Query($"PRAGMA table_info({SqlConstants.ServicesTableName});", transaction: transaction)
                          .Select(row => (string)row.name),
                StringComparer.OrdinalIgnoreCase);
        }

        #region Migration Logic

        /// <summary>
        /// Creates the Version 1 schema for a brand new database. 
        /// Consolidates all columns into a single CREATE statement dynamically built from SqlConstants.
        /// </summary>
        /// <param name="connection">The active database connection.</param>
        /// <param name="transaction">The active atomic transaction.</param>
        private static void ApplyVersion1(DbConnection connection, DbTransaction transaction)
        {
            var expectedColumns = GetExpectedColumns();

            var columnDefinitions = new List<string>
            {
                "Id INTEGER PRIMARY KEY AUTOINCREMENT" // PK is not in InsertColumns
            };

            foreach (var col in expectedColumns)
            {
                columnDefinitions.Add($"{col} {GetSqlType(col)}");
            }

            // IF NOT EXISTS to prevent concurrent racer crashes.
            var createTableSql = $"CREATE TABLE IF NOT EXISTS {SqlConstants.ServicesTableName} (\n    {string.Join(",\n    ", columnDefinitions)}\n);";
            connection.Execute(createTableSql, transaction: transaction);

            // Create the UNIQUE functional index (IF NOT EXISTS protects against concurrent creations)
            var createIndexSql = $"CREATE UNIQUE INDEX IF NOT EXISTS idx_services_name_lower ON {SqlConstants.ServicesTableName}(LOWER(Name));";
            connection.Execute(createIndexSql, transaction: transaction);
        }

        /// <summary>
        /// Safely brings an older, unversioned database up to the Version 1 standard
        /// using the legacy ALTER TABLE method, extracting required columns from SqlConstants.
        /// </summary>
        /// <param name="connection">The active database connection.</param>
        /// <param name="transaction">The active atomic transaction.</param>
        private static void UpgradeLegacyDatabaseToVersion1(DbConnection connection, DbTransaction transaction)
        {
            // 1. Fix the legacy non-unique index issue
            var indices = connection.Query($"PRAGMA index_list('{SqlConstants.ServicesTableName}');", transaction: transaction);
            bool needsUpgrade = indices.Any(i =>
                Convert.ToString(i.name) == "idx_services_name_lower" && Convert.ToInt64(i.unique) == 0);

            if (needsUpgrade)
            {
                connection.Execute("DROP INDEX IF EXISTS idx_services_name_lower;", transaction: transaction);
                Logger.Info("Dropped legacy non-unique index 'idx_services_name_lower'.");
            }

            // --- ROBUSTNESS: Defensively resolve duplicate LOWER(Name) groups before enforcing UNIQUE constraints ---
            // This prevents migration failure crashes (SQLite Error 19: UNIQUE constraint failed) when upgrading legacy DBs.
            // Using MIN(Id) ensures deterministic selection of the oldest record, avoiding GROUP_CONCAT non-determinism.
            var duplicates = connection.Query($@"
                SELECT LOWER(Name) AS LowerName, COUNT(*) AS Count, MIN(Id) AS KeepId
                FROM {SqlConstants.ServicesTableName}
                GROUP BY LOWER(Name)
                HAVING COUNT(*) > 1;", transaction: transaction).ToList();

            if (duplicates.Count > 0)
            {
                Logger.Warn($"Database migration warning: Found {duplicates.Count} duplicate service name group(s). Resolving automatically by retaining the oldest entry.");

                foreach (var dup in duplicates)
                {
                    string lowerName = dup.LowerName;
                    long keepId = Convert.ToInt64(dup.KeepId);

                    Logger.Info($"Deduplicating name group '{lowerName}': Keeping primary ID {keepId}, removing redundant variants.");

                    // Prune the redundant records within the transaction scope using parameterized queries
                    connection.Execute(
                        $"DELETE FROM {SqlConstants.ServicesTableName} WHERE LOWER(Name) = @LowerName AND Id <> @KeepId;",
                        new { LowerName = lowerName, KeepId = keepId },
                        transaction: transaction);
                }
            }
            // --------------------------------------------------------------------------------------------------------

            connection.Execute($"CREATE UNIQUE INDEX IF NOT EXISTS idx_services_name_lower ON {SqlConstants.ServicesTableName}(LOWER(Name));", transaction: transaction);

            // DRY PASS: Utilize centralized factory query engine helper
            var existingColumns = GetExistingColumnNames(connection, transaction);

            // --- Intercept ambiguous legacy column and rename before dynamic column mapping ---
            if (existingColumns.Contains("EnableRotation") && !existingColumns.Contains("EnableSizeRotation"))
            {
                Logger.Info("Migrating legacy database: Renaming 'EnableRotation' to 'EnableSizeRotation'.");
                connection.Execute($"ALTER TABLE {SqlConstants.ServicesTableName} RENAME COLUMN EnableRotation TO EnableSizeRotation;", transaction: transaction);
                existingColumns.Remove("EnableRotation");
                existingColumns.Add("EnableSizeRotation");
            }
            // ----------------------------------------------------------------

            // 2. Apply legacy column additions dynamically
            var expectedColumns = GetExpectedColumns();
            var missingColumns = expectedColumns.Where(col => !existingColumns.Contains(col)).ToList();

            foreach (var col in missingColumns)
            {
                Logger.Info($"Migrating database: Adding column '{col}' to '{SqlConstants.ServicesTableName}' table.");

                // ROBUSTNESS: Normalize the DDL definition to ensure backward compatibility for legacy non-empty databases
                string safeType = EnsureAlterableDefinition(GetSqlType(col));
                connection.Execute($"ALTER TABLE {SqlConstants.ServicesTableName} ADD COLUMN {col} {safeType};", transaction: transaction);
            }

            Logger.Info("Legacy database successfully migrated to Version 1.");
        }

        /// <summary>
        /// Applies the Version 2 schema migration, which primarily deals with renaming 
        /// the ambiguous 'EnableRotation' column to 'EnableSizeRotation' for databases
        /// that were already cleanly tracking schema Version 1.
        /// </summary>
        private static void ApplyVersion2(DbConnection connection, DbTransaction transaction) => RenameColumnIfExists(connection, transaction, 2, "EnableRotation", "EnableSizeRotation");

        /// <summary>
        /// Applies the Version 3 schema migration, adding the 'EnableConsoleUI' column 
        /// to the 'Services' table to support allocating a console for interactive apps.
        /// </summary>
        private static void ApplyVersion3(DbConnection connection, DbTransaction transaction) => AddColumnIfMissing(connection, transaction, 3, "EnableConsoleUI");

        /// <summary>
        /// Applies the Version 4 schema migration, removing strict NOT NULL constraints from 13 configuration 
        /// columns to perfectly align the SQLite schema with the nullable definitions in ServiceDto.
        /// Uses the SQLite table-rebuild idiom.
        /// </summary>
        private static void ApplyVersion4(DbConnection connection, DbTransaction transaction)
        {
            Logger.Info($"Migrating database to Version 4: Rebuilding '{SqlConstants.ServicesTableName}' table to drop strict NOT NULL constraints.");

            var expectedColumns = GetExpectedColumns().ToList();
            var columnDefinitions = new List<string>
            {
                "Id INTEGER PRIMARY KEY AUTOINCREMENT"
            };

            // Dynamically construct the v4 table schema matching the exact DTO definition
            foreach (var col in expectedColumns)
            {
                columnDefinitions.Add($"{col} {GetSqlType(col)}");
            }

            var createTableSql = $"CREATE TABLE IF NOT EXISTS Services_v4 (\n    {string.Join(",\n    ", columnDefinitions)}\n);";
            connection.Execute(createTableSql, transaction: transaction);

            // --- Extract only the columns that actually exist in the old table using unified discovery helper ---
            var existingColumns = GetExistingColumnNames(connection, transaction);

            var orphansBeforeRebuild = existingColumns
                .Where(c => !expectedColumns.Contains(c, StringComparer.OrdinalIgnoreCase) && !"Id".Equals(c, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (orphansBeforeRebuild.Count > 0)
            {
                var orphanList = string.Join(", ", orphansBeforeRebuild);
                var idList = string.Join(", ", new[] { "Id" }.Concat(orphansBeforeRebuild));

                connection.Execute(
                    $"CREATE TABLE IF NOT EXISTS Services_orphans_v4 AS SELECT {idList} FROM {SqlConstants.ServicesTableName};",
                    transaction: transaction);

                Logger.Warn($"ApplyVersion4: orphan column(s) '{orphanList}' were preserved in 'Services_orphans_v4' for manual recovery. Drop that table once you have verified the data is no longer needed.");
            }

            // Snapshot dependent indexes and triggers before dropping the table.
            // (Views referencing Services survive DROP TABLE and re-bind by name after the rename,
            //  so they need no snapshot; sqlite_master tbl_name never links views to base tables.)
            var dependents = connection.Query<(string Type, string Name, string Sql)>(
                $@"SELECT type, name, sql FROM sqlite_master 
                  WHERE tbl_name = '{SqlConstants.ServicesTableName}' 
                    AND type IN ('index', 'trigger') 
                    AND sql IS NOT NULL 
                    AND name <> 'idx_services_name_lower';",
                transaction: transaction).ToList();

            var columnsToCopy = new List<string> { "Id" };
            columnsToCopy.AddRange(expectedColumns.Where(existingColumns.Contains));

            string columnList = string.Join(", ", columnsToCopy);
            // --------------------------------------------------------------------------

            string copyDataSql = $"INSERT INTO Services_v4 ({columnList}) SELECT {columnList} FROM {SqlConstants.ServicesTableName};";
            connection.Execute(copyDataSql, transaction: transaction);

            // Swap the tables
            connection.Execute($"DROP TABLE IF EXISTS {SqlConstants.ServicesTableName};", transaction: transaction);
            connection.Execute($"ALTER TABLE Services_v4 RENAME TO {SqlConstants.ServicesTableName};", transaction: transaction);

            // --------------------------------------------------------------------------
            connection.Execute($"CREATE UNIQUE INDEX IF NOT EXISTS idx_services_name_lower ON {SqlConstants.ServicesTableName}(LOWER(Name));", transaction: transaction);

            // Re-create all snapshot dependent indexes and triggers
            foreach (var dependent in dependents)
            {
                try
                {
                    connection.Execute(dependent.Sql, transaction: transaction);
                    Logger.Debug($"Successfully restored dependent {dependent.Type}: '{dependent.Name}' during table rebuild.");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to restore dependent {dependent.Type} '{dependent.Name}'. Statement: {dependent.Sql}", ex);
                    throw;
                }
            }

            Logger.Info("Database successfully migrated to Version 4.");
        }

        /// <summary>
        /// Applies the Version 5 schema migration, adding the 'RecoveryOnCleanExit' column 
        /// to the 'Services' table to support triggering recovery actions even on successful exits (Code 0).
        /// </summary>
        private static void ApplyVersion5(DbConnection connection, DbTransaction transaction) => AddColumnIfMissing(connection, transaction, 5, "RecoveryOnCleanExit");

        /// <summary>
        /// Applies the Version 6 schema migration, dropping the legacy functional LOWER unique index
        /// and creating an explicit collation index (Name COLLATE UNICODE_NOCASE). This aligns the physical index
        /// with the query engine's case-insensitive filters to optimize lookup execution plans while safely handling global charsets.
        /// </summary>
        /// <remarks>
        /// Note: The custom registered UNICODE_NOCASE collation handles wide Unicode characters completely.
        /// </remarks>
        /// <param name="connection">The active database connection.</param>
        /// <param name="transaction">The active atomic transaction.</param>
        private static void ApplyVersion6(DbConnection connection, DbTransaction transaction)
        {
            Logger.Info("Migrating database to Version 6: Aligning unique index standard with native case-insensitive query collation (UNICODE_NOCASE).");

            // Purge legacy functional identifiers and clean up standard unique indexes to transition cleanly to the Unicode collation format.
            connection.Execute("DROP INDEX IF EXISTS idx_services_name_lower;", transaction: transaction);
            connection.Execute("DROP INDEX IF EXISTS idx_services_name_unique;", transaction: transaction);

            // Defensively purge any conflicting full-range Unicode case duplicate records that might cause conflicts during index recreation
            var duplicates = connection.Query($@"
                SELECT Name, COUNT(*) AS Count, MIN(Id) AS KeepId
                FROM {SqlConstants.ServicesTableName}
                GROUP BY Name COLLATE UNICODE_NOCASE
                HAVING COUNT(*) > 1;", transaction: transaction).ToList();

            if (duplicates.Count > 0)
            {
                Logger.Warn($"Version 6 Remediation: Found {duplicates.Count} duplicate casing records. Resolving via oldest instance retention tracking.");

                foreach (var dup in duplicates)
                {
                    string serviceName = dup.Name;
                    long keepId = Convert.ToInt64(dup.KeepId);

                    connection.Execute(
                        $"DELETE FROM {SqlConstants.ServicesTableName} WHERE Name = @Name COLLATE UNICODE_NOCASE AND Id <> @KeepId;",
                        new { Name = serviceName, KeepId = keepId },
                        transaction: transaction);
                }
            }

            // Materialize the index using custom global query collation rules under a precise, maintainable identifier.
            connection.Execute($"CREATE UNIQUE INDEX IF NOT EXISTS idx_services_name_unique ON {SqlConstants.ServicesTableName}(Name COLLATE UNICODE_NOCASE);", transaction: transaction);
            Logger.Info("Database successfully migrated to Version 6.");
        }

        #endregion

        #region Migration Helpers

        /// <summary>
        /// A transactional helper to safely add a new column to the Services table if it doesn't already exist.
        /// </summary>
        private static void AddColumnIfMissing(DbConnection conn, DbTransaction tx, int version, string columnName)
        {
            // DRY PASS: Utilize centralized factory query engine helper
            var existing = GetExistingColumnNames(conn, tx);

            if (!existing.Contains(columnName))
            {
                // ROBUSTNESS: Ensure type-safe defaults are applied defensively via the helper
                var sqlType = EnsureAlterableDefinition(GetSqlType(columnName));   // SSoT via [SqlColumn]
                Logger.Info($"Migrating database to Version {version}: Adding '{columnName}' column.");
                conn.Execute($"ALTER TABLE {SqlConstants.ServicesTableName} ADD COLUMN {columnName} {sqlType};", transaction: tx);
                Logger.Info($"Database successfully migrated to Version {version}.");
            }
            else
            {
                Logger.Info($"Schema already at Version {version}: column '{columnName}' present, no DDL issued.");
            }
        }

        /// <summary>
        /// A transactional helper to safely rename an existing column within the Services table.
        /// </summary>
        private static void RenameColumnIfExists(DbConnection conn, DbTransaction tx, int version, string oldName, string newName)
        {
            // DRY PASS: Utilize centralized factory query engine helper
            var existing = GetExistingColumnNames(conn, tx);

            if (existing.Contains(oldName) && !existing.Contains(newName))
            {
                Logger.Info($"Migrating database to Version {version}: Renaming '{oldName}' to '{newName}'.");
                conn.Execute($"ALTER TABLE {SqlConstants.ServicesTableName} RENAME COLUMN {oldName} TO {newName};", transaction: tx);
                Logger.Info($"Database successfully migrated to Version {version}.");
            }
            else
            {
                if (existing.Contains(newName))
                {
                    Logger.Info($"Migration to Version {version} skipped: Column '{newName}' already exists (migration likely applied previously).");
                }
                else
                {
                    Logger.Info($"Migration to Version {version} skipped: Source column '{oldName}' was not found in the '{SqlConstants.ServicesTableName}' table layout.");
                }
            }
        }

        #endregion

        /// <summary>
        /// Infers the SQLite data type and constraints for a given column name by reading the cached 
        /// [SqlColumn] attributes from the ServiceDto class.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        /// <returns>The SQLite type definition string (e.g., "INTEGER" or "TEXT NOT NULL").</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a column name exists in <see cref="SqlConstants"/> but lacks an [SqlColumn] attribute on the DTO.
        /// </exception>
        private static string GetSqlType(string columnName)
        {
            if (SqlTypeMap.TryGetValue(columnName, out var sqlType))
            {
                return sqlType;
            }

            // Fail-Fast: Prevent wrong-affinity columns in production
            throw new InvalidOperationException(
                $"Column '{columnName}' is defined in SqlConstants but lacks an [SqlColumn] attribute in ServiceDto. " +
                "Add the attribute to the DTO property to prevent schema drift.");
        }

        /// <summary>
        /// Standardizes column schemas intended for execution inside an ALTER TABLE ADD COLUMN statement.
        /// Defends against SQLite restrictions prohibiting NOT NULL column creation on existing non-empty tables by inferring type-safe defaults.
        /// </summary>
        /// <param name="definition">The raw SQL configuration schema string sourced from GetSqlType.</param>
        /// <returns>A validated schema definition string containing safety-net DEFAULT constraints where required.</returns>
        private static string EnsureAlterableDefinition(string definition)
        {
            if (definition.IndexOf("NOT NULL", StringComparison.OrdinalIgnoreCase) >= 0 &&
                definition.IndexOf("DEFAULT", StringComparison.OrdinalIgnoreCase) < 0)
            {
                string affinityToken = definition.Split(SplitWhitespaceChars, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
                string inferredDefault = string.Equals(affinityToken, "TEXT", StringComparison.OrdinalIgnoreCase) ? "DEFAULT ''" : "DEFAULT 0";
                return $"{definition} {inferredDefault}";
            }

            return definition;
        }

        /// <summary>
        /// Extension-like helper logic utilized to filter collection groups cleanly.
        /// </summary>
        private static IEnumerable<string> MakeOrphanFilter(this IEnumerable<string> source, HashSet<string> references)
        {
            return source.Except(references, StringComparer.OrdinalIgnoreCase);
        }
    }
}
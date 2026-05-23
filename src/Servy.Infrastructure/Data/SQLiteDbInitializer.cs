using Dapper;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Provides helper methods to initialize the SQLite database schema for Servy.
    /// Tracks database versions and applies sequential migrations dynamically based on central constants.
    /// </summary>
    public static class SQLiteDbInitializer
    {
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
                        if (currentVersion == 0 && TableExists(connection, "Services", transaction))
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

                        // --- FUTURE MIGRATIONS GO HERE ---
                        // if (currentVersion < 6) { ... }

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
        /// Final reconciliation step that ensures the database schema matches the 
        /// Single Source of Truth (SqlConstants), even if explicit migrations were missed.
        /// </summary>
        /// <param name="connection">The active database connection.</param>
        /// <param name="currentVersion">The schema version detected after migrations.</param>
        /// <param name="transaction">The active atomic transaction.</param>
        private static void ReconcileSchema(DbConnection connection, int currentVersion, DbTransaction transaction)
        {
            // Fetch full column definitions (name and type) from PRAGMA
            var tableInfo = connection.Query("PRAGMA table_info(Services);", transaction: transaction).ToList();

            var existingColumns = new HashSet<string>(
                tableInfo.Select(row => (string)row.name),
                StringComparer.OrdinalIgnoreCase);

            // Assume GetExpectedColumns returns StandardColumns. 
            // We explicitly add primary/special keys to our expected set to prevent them from being flagged as orphans.
            var expectedColumns = new HashSet<string>(GetExpectedColumns(), StringComparer.OrdinalIgnoreCase);
            var coreKeys = new[] { "Id" };
            foreach (var key in coreKeys)
            {
                expectedColumns.Add(key);
            }

            // 1. Detect and Add Missing Columns (Self-Healing)
            var missing = expectedColumns.Where(c => !existingColumns.Contains(c)).ToList();

            if (missing.Count > 0)
            {
                Logger.Warn($"Single-Source-of-Truth drift detected at SchemaVersion={currentVersion}. Adding missing columns: {string.Join(", ", missing)}");

                foreach (var col in missing)
                {
                    string baseDefinition = GetSqlType(col); // e.g., "TEXT NOT NULL" or "INTEGER NOT NULL DEFAULT 0"

                    // ROBUSTNESS: Handle the SQLite ALTER TABLE NOT NULL constraint restriction.
                    // SQLite prohibits adding a NOT NULL column without a default value constraint to an existing table.
                    // If 'NOT NULL' is present but 'DEFAULT' is absent, append an intuitive type-safe default mapping.
                    if (baseDefinition.IndexOf("NOT NULL", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        baseDefinition.IndexOf("DEFAULT", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        string affinityToken = baseDefinition.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
                        string inferredDefault = string.Equals(affinityToken, "TEXT", StringComparison.OrdinalIgnoreCase) ? "DEFAULT ''" : "DEFAULT 0";
                        baseDefinition = $"{baseDefinition} {inferredDefault}";
                    }

                    // Core keys shouldn't hit this path dynamically, but we use GetSqlType for standard columns
                    connection.Execute($"ALTER TABLE Services ADD COLUMN {col} {baseDefinition};", transaction: transaction);
                    Logger.Info($"Self-healed column: {col} with schema context '{baseDefinition}'");
                }
            }

            // 2. Detect Orphaned Columns (Removed or Renamed)
            var orphans = existingColumns
                .Except(expectedColumns, StringComparer.OrdinalIgnoreCase)
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
                    string expectedAffinity = expectedFullType.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

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

            // Added IF NOT EXISTS to prevent concurrent racer crashes.
            var createTableSql = $"CREATE TABLE IF NOT EXISTS Services (\n    {string.Join(",\n    ", columnDefinitions)}\n);";
            connection.Execute(createTableSql, transaction: transaction);

            // Create the UNIQUE functional index (IF NOT EXISTS protects against concurrent creations)
            var createIndexSql = "CREATE UNIQUE INDEX IF NOT EXISTS idx_services_name_lower ON Services(LOWER(Name));";
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
            var indices = connection.Query("PRAGMA index_list('Services');", transaction: transaction);
            bool needsUpgrade = indices.Any(i =>
                Convert.ToString(i.name) == "idx_services_name_lower" && Convert.ToInt64(i.unique) == 0);

            if (needsUpgrade)
            {
                connection.Execute("DROP INDEX IF EXISTS idx_services_name_lower;", transaction: transaction);
                Logger.Info("Dropped legacy non-unique index 'idx_services_name_lower'.");
            }

            // --- ROBUSTNESS: Defensively resolve duplicate LOWER(Name) groups before enforcing UNIQUE constraints ---
            // This prevents migration failure crashes (SQLite Error 19: UNIQUE constraint failed) when upgrading legacy DBs.
            var duplicates = connection.Query(@"
                SELECT LOWER(Name) AS LowerName, COUNT(*) AS Count, GROUP_CONCAT(Id) AS Ids
                FROM Services
                GROUP BY LOWER(Name)
                HAVING COUNT(*) > 1;", transaction: transaction).ToList();

            if (duplicates.Count > 0)
            {
                Logger.Warn($"Database migration warning: Found {duplicates.Count} duplicate service name group(s). Resolving automatically by retaining the oldest entry.");

                foreach (var dup in duplicates)
                {
                    string lowerName = dup.LowerName;
                    string[] allIds = ((string)dup.Ids).Split(',');

                    // Retain the first/lowest ID for historical integrity, mark trailing IDs for deletion
                    string primaryId = allIds[0];
                    var redundantIds = allIds.Skip(1).ToList();
                    string idListString = string.Join(",", redundantIds);

                    Logger.Info($"Deduplicating name group '{lowerName}': Keeping primary ID {primaryId}, removing redundant variant ID(s): {idListString}.");

                    // Prune the redundant records within the transaction scope
                    connection.Execute($"DELETE FROM Services WHERE Id IN ({idListString});", transaction: transaction);
                }
            }
            // --------------------------------------------------------------------------------------------------------

            connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_services_name_lower ON Services(LOWER(Name));", transaction: transaction);

            var existingColumns = new HashSet<string>(
                connection.Query("PRAGMA table_info(Services);", transaction: transaction)
                          .Select(row => (string)row.name),
                StringComparer.OrdinalIgnoreCase
            );

            // --- Intercept ambiguous legacy column and rename before dynamic column mapping ---
            if (existingColumns.Contains("EnableRotation") && !existingColumns.Contains("EnableSizeRotation"))
            {
                Logger.Info("Migrating legacy database: Renaming 'EnableRotation' to 'EnableSizeRotation'.");
                connection.Execute("ALTER TABLE Services RENAME COLUMN EnableRotation TO EnableSizeRotation;", transaction: transaction);
                existingColumns.Remove("EnableRotation");
                existingColumns.Add("EnableSizeRotation");
            }
            // ----------------------------------------------------------------

            // 2. Apply legacy column additions dynamically
            var expectedColumns = GetExpectedColumns();
            var missingColumns = expectedColumns.Where(col => !existingColumns.Contains(col)).ToList();

            foreach (var col in missingColumns)
            {
                Logger.Info($"Migrating database: Adding column '{col}' to 'Services' table.");
                connection.Execute($"ALTER TABLE Services ADD COLUMN {col} {GetSqlType(col)};", transaction: transaction);
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
            Logger.Info("Migrating database to Version 4: Rebuilding 'Services' table to drop strict NOT NULL constraints.");

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

            var createTableSql = $"CREATE TABLE Services_v4 (\n    {string.Join(",\n    ", columnDefinitions)}\n);";
            connection.Execute(createTableSql, transaction: transaction);

            // --- Extract only the columns that actually exist in the old table ---
            // Because 'expectedColumns' contains future columns (like v5's RecoveryOnCleanExit),
            // selecting them from the old table will throw a "no such column" SQL logic error.
            var existingColumns = new HashSet<string>(
                connection.Query("PRAGMA table_info(Services);", transaction: transaction)
                          .Select(row => (string)row.name),
                StringComparer.OrdinalIgnoreCase
            );

            var orphansBeforeRebuild = existingColumns
                .Where(c => !expectedColumns.Contains(c, StringComparer.OrdinalIgnoreCase) && !"Id".Equals(c, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (orphansBeforeRebuild.Count > 0)
            {
                Logger.Error($"ApplyVersion4: about to drop orphan column(s) '{string.Join(", ", orphansBeforeRebuild)}'. Data in these columns will be permanently lost. If this is a renamed column, add a RenameColumnIfExists migration BEFORE v4 to preserve the data.");
            }

            var columnsToCopy = new List<string> { "Id" };
            columnsToCopy.AddRange(expectedColumns.Where(existingColumns.Contains));

            string columnList = string.Join(", ", columnsToCopy);
            // --------------------------------------------------------------------------

            string copyDataSql = $"INSERT INTO Services_v4 ({columnList}) SELECT {columnList} FROM Services;";
            connection.Execute(copyDataSql, transaction: transaction);

            // Swap the tables
            connection.Execute("DROP TABLE Services;", transaction: transaction);
            connection.Execute("ALTER TABLE Services_v4 RENAME TO Services;", transaction: transaction);

            // Re-create the functional unique index because SQLite drops indexes when the parent table is dropped
            connection.Execute("CREATE UNIQUE INDEX idx_services_name_lower ON Services(LOWER(Name));", transaction: transaction);

            Logger.Info("Database successfully migrated to Version 4.");
        }

        /// <summary>
        /// Applies the Version 5 schema migration, adding the 'RecoveryOnCleanExit' column 
        /// to the 'Services' table to support triggering recovery actions even on successful exits (Code 0).
        /// </summary>
        private static void ApplyVersion5(DbConnection connection, DbTransaction transaction) => AddColumnIfMissing(connection, transaction, 5, "RecoveryOnCleanExit");

        #endregion

        #region Migration Helpers

        /// <summary>
        /// A transactional helper to safely add a new column to the Services table if it doesn't already exist.
        /// </summary>
        private static void AddColumnIfMissing(DbConnection conn, DbTransaction tx, int version, string columnName)
        {
            var existing = new HashSet<string>(
                conn.Query("PRAGMA table_info(Services);", transaction: tx).Select(r => (string)r.name),
                StringComparer.OrdinalIgnoreCase);

            if (!existing.Contains(columnName))
            {
                var sqlType = GetSqlType(columnName);   // SSoT via [SqlColumn]
                Logger.Info($"Migrating database to Version {version}: Adding '{columnName}' column.");
                conn.Execute($"ALTER TABLE Services ADD COLUMN {columnName} {sqlType};", transaction: tx);
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
            var existing = new HashSet<string>(
                conn.Query("PRAGMA table_info(Services);", transaction: tx).Select(r => (string)r.name),
                StringComparer.OrdinalIgnoreCase);

            if (existing.Contains(oldName) && !existing.Contains(newName))
            {
                Logger.Info($"Migrating database to Version {version}: Renaming '{oldName}' to '{newName}'.");
                conn.Execute($"ALTER TABLE Services RENAME COLUMN {oldName} TO {newName};", transaction: tx);
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
                    Logger.Info($"Migration to Version {version} skipped: Source column '{oldName}' was not found in the 'Services' table layout.");
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
    }
}
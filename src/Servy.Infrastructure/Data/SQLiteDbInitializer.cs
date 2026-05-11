using Dapper;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
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
            // 1. Ensure the version tracking table exists. 
            // The CHECK constraint guarantees only a single row (Id = 1) can ever exist.
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS SchemaInfo (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1), 
                    Version INTEGER NOT NULL
                );");

            // 2. Get the current database version
            int currentVersion = GetSchemaVersion(connection);

            // 3. Backward Compatibility: Handle unversioned legacy databases
            if (currentVersion == 0 && TableExists(connection, "Services"))
            {
                UpgradeLegacyDatabaseToVersion1(connection);
                UpdateSchemaVersion(connection, 1);
                currentVersion = 1;
            }

            // 4. Sequential Migration Runner
            if (currentVersion < 1)
            {
                ApplyVersion1(connection);
                UpdateSchemaVersion(connection, 1);
                currentVersion = 1; // Kept for future migration logic chaining
            }

            // Version 2 Migration to rename the ambiguous EnableRotation column
            if (currentVersion < 2)
            {
                ApplyVersion2(connection);
                UpdateSchemaVersion(connection, 2);
                currentVersion = 2;
            }

            // Version 3 Migration to add EnableConsoleUI for interactive console apps support
            if (currentVersion < 3)
            {
                ApplyVersion3(connection);
                UpdateSchemaVersion(connection, 3);
                currentVersion = 3;
            }

            // Version 4 Migration to drop strict NOT NULL constraints, aligning the schema with ServiceDto
            if (currentVersion < 4)
            {
                ApplyVersion4(connection);
                UpdateSchemaVersion(connection, 4);
                currentVersion = 4;
            }

            // Version 5 Migration to add RecoveryOnCleanExit column to support triggering recovery on successful exits
            if (currentVersion < 5)
            {
                ApplyVersion5(connection);
                UpdateSchemaVersion(connection, 5);
                currentVersion = 5;
            }

            // --- FUTURE MIGRATIONS GO HERE ---
            // if (currentVersion < 6) { ... }

            // 5. Reconciliation safety net
            // Ensures that any columns added to SqlConstants but missed in migrations are applied.
            ReconcileSchema(connection, currentVersion);
        }

        /// <summary>
        /// Final reconciliation step that ensures the database schema matches the 
        /// Single Source of Truth (SqlConstants), even if explicit migrations were missed.
        /// </summary>
        /// <param name="connection">The active database connection.</param>
        /// <param name="currentVersion">The schema version detected after migrations.</param>
        private static void ReconcileSchema(DbConnection connection, int currentVersion)
        {
            var existingColumns = new HashSet<string>(
                connection.Query("PRAGMA table_info(Services);").Select(row => (string)row.name),
                StringComparer.OrdinalIgnoreCase);

            var expectedColumns = GetExpectedColumns();
            var missing = expectedColumns.Where(c => !existingColumns.Contains(c)).ToList();

            if (missing.Count > 0)
            {
                // Convert silent failure into a logged self-healing event
                Logger.Warn($"Single-Source-of-Truth drift detected at SchemaVersion={currentVersion}. Adding missing columns: {string.Join(", ", missing)}");

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var col in missing)
                        {
                            connection.Execute($"ALTER TABLE Services ADD COLUMN {col} {GetSqlType(col)};", transaction: transaction);
                            Logger.Info($"Self-healed column: {col}");
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Logger.Error("Schema reconciliation failed; rolling back partial column additions.", ex);
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves the current schema version from the tracking table.
        /// </summary>
        /// <param name="connection">The active database connection.</param>
        /// <returns>The current version integer, or 0 if the table is empty.</returns>
        private static int GetSchemaVersion(DbConnection connection)
        {
            return connection.QueryFirstOrDefault<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;");
        }

        /// <summary>
        /// Upserts the schema version into the tracking table, ensuring only one row exists.
        /// </summary>
        /// <param name="connection">The active database connection.</param>
        /// <param name="version">The new schema version to record.</param>
        private static void UpdateSchemaVersion(DbConnection connection, int version)
        {
            var sql = @"
                INSERT INTO SchemaInfo (Id, Version) VALUES (1, @Version)
                ON CONFLICT(Id) DO UPDATE SET Version = excluded.Version;";

            connection.Execute(sql, new { Version = version });
        }

        /// <summary>
        /// Checks if a specific table exists within the SQLite master schema.
        /// </summary>
        /// <param name="connection">The active database connection.</param>
        /// <param name="tableName">The exact name of the table to look for.</param>
        /// <returns><c>true</c> if the table exists; otherwise, <c>false</c>.</returns>
        private static bool TableExists(DbConnection connection, string tableName)
        {
            var result = connection.QueryFirstOrDefault<int>(
                "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@TableName;",
                new { TableName = tableName });

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
        /// Creates the Version 1 schema for a brand new database. 
        /// Consolidates all columns into a single CREATE statement dynamically built from SqlConstants.
        /// </summary>
        /// <param name="connection">The active database connection.</param>
        private static void ApplyVersion1(DbConnection connection)
        {
            using (var transaction = connection.BeginTransaction())
            {
                try
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

                    var createTableSql = $"CREATE TABLE Services (\n    {string.Join(",\n    ", columnDefinitions)}\n);";
                    connection.Execute(createTableSql, transaction: transaction);

                    // Create the UNIQUE functional index
                    var createIndexSql = "CREATE UNIQUE INDEX idx_services_name_lower ON Services(LOWER(Name));";
                    connection.Execute(createIndexSql, transaction: transaction);

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Logger.Error("CRITICAL: Version 1 database migration failed. Transaction rolled back.", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Safely brings an older, unversioned database up to the Version 1 standard
        /// using the legacy ALTER TABLE method, extracting required columns from SqlConstants.
        /// </summary>
        /// <param name="connection">The active database connection.</param>
        private static void UpgradeLegacyDatabaseToVersion1(DbConnection connection)
        {
            using (var transaction = connection.BeginTransaction())
            {
                try
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

                    transaction.Commit();
                    Logger.Info("Legacy database successfully migrated to Version 1.");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Logger.Error("CRITICAL: Database migration failed. The transaction has been rolled back to prevent schema corruption.", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Applies the Version 2 schema migration, which primarily deals with renaming 
        /// the ambiguous 'EnableRotation' column to 'EnableSizeRotation' for databases
        /// that were already cleanly tracking schema Version 1.
        /// </summary>
        private static void ApplyVersion2(DbConnection connection)
        {
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    var existingColumns = new HashSet<string>(
                        connection.Query("PRAGMA table_info(Services);", transaction: transaction)
                                  .Select(row => (string)row.name),
                        StringComparer.OrdinalIgnoreCase
                    );

                    if (existingColumns.Contains("EnableRotation") && !existingColumns.Contains("EnableSizeRotation"))
                    {
                        Logger.Info("Migrating database to Version 2: Renaming 'EnableRotation' to 'EnableSizeRotation'.");
                        connection.Execute("ALTER TABLE Services RENAME COLUMN EnableRotation TO EnableSizeRotation;", transaction: transaction);
                    }

                    transaction.Commit();
                    Logger.Info("Database successfully migrated to Version 2.");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Logger.Error("CRITICAL: Version 2 database migration failed. Transaction rolled back.", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Applies the Version 3 schema migration, adding the 'EnableConsoleUI' column 
        /// to the 'Services' table to support allocating a console for interactive apps.
        /// </summary>
        private static void ApplyVersion3(DbConnection connection)
        {
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    var existingColumns = new HashSet<string>(
                        connection.Query("PRAGMA table_info(Services);", transaction: transaction)
                                  .Select(row => (string)row.name),
                        StringComparer.OrdinalIgnoreCase
                    );

                    if (!existingColumns.Contains("EnableConsoleUI"))
                    {
                        Logger.Info("Migrating database to Version 3: Adding 'EnableConsoleUI' column.");
                        connection.Execute("ALTER TABLE Services ADD COLUMN EnableConsoleUI INTEGER;", transaction: transaction);
                    }

                    transaction.Commit();
                    Logger.Info("Database successfully migrated to Version 3.");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Logger.Error("CRITICAL: Version 3 database migration failed. Transaction rolled back.", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Applies the Version 4 schema migration, removing strict NOT NULL constraints from 13 configuration 
        /// columns to perfectly align the SQLite schema with the nullable definitions in ServiceDto.
        /// Uses the SQLite table-rebuild idiom.
        /// </summary>
        private static void ApplyVersion4(DbConnection connection)
        {
            // Disable foreign keys temporarily during table rebuild
            connection.Execute("PRAGMA foreign_keys=OFF;");

            try
            {
                using (var transaction = connection.BeginTransaction())
                {
                    try
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

                        transaction.Commit();
                        Logger.Info("Database successfully migrated to Version 4.");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Logger.Error("CRITICAL: Version 4 database migration failed. Transaction rolled back.", ex);
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
        /// Applies the Version 5 schema migration, adding the 'RecoveryOnCleanExit' column 
        /// to the 'Services' table to support triggering recovery actions even on successful exits (Code 0).
        /// </summary>
        private static void ApplyVersion5(DbConnection connection)
        {
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    var existingColumns = new HashSet<string>(
                        connection.Query("PRAGMA table_info(Services);", transaction: transaction)
                                  .Select(row => (string)row.name),
                        StringComparer.OrdinalIgnoreCase
                    );

                    if (!existingColumns.Contains("RecoveryOnCleanExit"))
                    {
                        Logger.Info("Migrating database to Version 5: Adding 'RecoveryOnCleanExit' column.");
                        connection.Execute("ALTER TABLE Services ADD COLUMN RecoveryOnCleanExit INTEGER;", transaction: transaction);
                    }

                    transaction.Commit();
                    Logger.Info("Database successfully migrated to Version 5.");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Logger.Error("CRITICAL: Version 5 database migration failed. Transaction rolled back.", ex);
                    throw;
                }
            }
        }

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
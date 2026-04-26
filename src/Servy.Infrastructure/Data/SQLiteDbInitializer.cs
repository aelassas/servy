using Dapper;
using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Provides helper methods to initialize the SQLite database schema for Servy.
    /// Tracks database versions and applies sequential migrations dynamically based on central constants.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class SQLiteDbInitializer
    {
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

            // --- FUTURE MIGRATIONS GO HERE ---
            // if (currentVersion < 4) { ... }
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
            connection.Execute(createTableSql);

            // Create the UNIQUE functional index
            var createIndexSql = "CREATE UNIQUE INDEX idx_services_name_lower ON Services(LOWER(Name));";
            connection.Execute(createIndexSql);
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
        /// Infers the SQLite data type and constraints for a given column name.
        /// </summary>
        /// <param name="columnName">The name of the column.</param>
        /// <returns>The SQLite type definition string (e.g., "INTEGER" or "TEXT NOT NULL").</returns>
        private static string GetSqlType(string columnName)
        {
            if (columnName.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
                columnName.Equals("ExecutablePath", StringComparison.OrdinalIgnoreCase))
            {
                return "TEXT NOT NULL";
            }

            var originalNotNullInts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "StartupType", "Priority", "EnableSizeRotation", "RotationSize", // RENAMED IN LIST
                "EnableHealthMonitoring", "HeartbeatInterval", "MaxFailedChecks",
                "RecoveryAction", "MaxRestartAttempts", "RunAsLocalSystem",
                "PreLaunchTimeoutSeconds", "PreLaunchRetryAttempts", "PreLaunchIgnoreFailure"
            };

            var nullableInts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Pid", "EnableDebugLogs", "MaxRotations", "EnableDateRotation",
                "DateRotationType", "StartTimeout", "StopTimeout", "PreviousStopTimeout",
                "PreStopTimeoutSeconds", "PreStopLogAsError", "UseLocalTimeForRotation",
                "EnableConsoleUI"
            };

            if (originalNotNullInts.Contains(columnName))
            {
                return "INTEGER NOT NULL";
            }

            if (nullableInts.Contains(columnName))
            {
                return "INTEGER";
            }

            return "TEXT";
        }
    }
}
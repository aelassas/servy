using Dapper;
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

            // --- FUTURE MIGRATIONS GO HERE ---
            // if (currentVersion < 2) { ... }
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
            // 1. Fix the legacy non-unique index issue
            var indices = connection.Query("PRAGMA index_list('Services');");
            bool needsUpgrade = indices.Any(i =>
                Convert.ToString(i.name) == "idx_services_name_lower" && Convert.ToInt64(i.unique) == 0);

            if (needsUpgrade)
            {
                connection.Execute("DROP INDEX IF EXISTS idx_services_name_lower;");
            }

            connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_services_name_lower ON Services(LOWER(Name));");

            // 2. Apply legacy column additions dynamically
            var existingColumns = new HashSet<string>(
                connection.Query("PRAGMA table_info(Services);")
                          .Select(row => (string)row.name),
                StringComparer.OrdinalIgnoreCase
            );

            var expectedColumns = GetExpectedColumns();

            foreach (var col in expectedColumns)
            {
                if (!existingColumns.Contains(col))
                {
                    connection.Execute($"ALTER TABLE Services ADD COLUMN {col} {GetSqlType(col)};");
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

            // Original columns that require NOT NULL constraints to match legacy schema
            var originalNotNullInts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "StartupType", "Priority", "EnableRotation", "RotationSize",
                "EnableHealthMonitoring", "HeartbeatInterval", "MaxFailedChecks",
                "RecoveryAction", "MaxRestartAttempts", "RunAsLocalSystem",
                "PreLaunchTimeoutSeconds", "PreLaunchRetryAttempts", "PreLaunchIgnoreFailure"
            };

            // Columns added later that are nullable
            var nullableInts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Pid", "EnableDebugLogs", "MaxRotations", "EnableDateRotation",
                "DateRotationType", "StartTimeout", "StopTimeout", "PreviousStopTimeout",
                "PreStopTimeoutSeconds", "PreStopLogAsError", "UseLocalTimeForRotation"
            };

            if (originalNotNullInts.Contains(columnName))
            {
                // Note: These will only ever hit during a fresh CREATE TABLE (ApplyVersion1). 
                // They already exist in legacy DBs, so ALTER TABLE will never execute against them.
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
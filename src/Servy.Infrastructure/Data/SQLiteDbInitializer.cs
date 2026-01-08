using Dapper;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Provides helper methods to initialize the SQLite database schema for Servy.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class SQLiteDbInitializer
    {
        /// <summary>
        /// Creates or updates the <c>Services</c> table and necessary indexes.
        /// </summary>
        /// <param name="connection">An open database connection to execute commands on.</param>
        public static void Initialize(IDbConnection connection)
        {
            var createTableSql = @"
            CREATE TABLE IF NOT EXISTS Services (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT,
                ExecutablePath TEXT NOT NULL,
                StartupDirectory TEXT,
                Parameters TEXT,
                StartupType INTEGER NOT NULL,
                Priority INTEGER NOT NULL,
                StdoutPath TEXT,
                StderrPath TEXT,
                EnableRotation INTEGER NOT NULL,
                RotationSize INTEGER NOT NULL,
                EnableHealthMonitoring INTEGER NOT NULL,
                HeartbeatInterval INTEGER NOT NULL,
                MaxFailedChecks INTEGER NOT NULL,
                RecoveryAction INTEGER NOT NULL,
                MaxRestartAttempts INTEGER NOT NULL,
                EnvironmentVariables TEXT,
                ServiceDependencies TEXT,
                RunAsLocalSystem INTEGER NOT NULL,
                UserAccount TEXT,
                Password TEXT,
                PreLaunchExecutablePath TEXT,
                PreLaunchStartupDirectory TEXT,
                PreLaunchParameters TEXT,
                PreLaunchEnvironmentVariables TEXT,
                PreLaunchStdoutPath TEXT,
                PreLaunchStderrPath TEXT,
                PreLaunchTimeoutSeconds INTEGER NOT NULL,
                PreLaunchRetryAttempts INTEGER NOT NULL,
                PreLaunchIgnoreFailure INTEGER NOT NULL
            );";

            connection.Execute(createTableSql);

            var createIndexSql = "CREATE INDEX IF NOT EXISTS idx_services_name_lower ON Services(LOWER(Name));";
            connection.Execute(createIndexSql);

            EnsureColumns(connection);
        }

        /// <summary>
        /// Ensures that all expected columns exist in the Services table.
        /// Adds missing columns for backward compatibility.
        /// </summary>
        private static void EnsureColumns(IDbConnection connection)
        {
            // Use dynamic since PRAGMA returns multiple columns
            var existingColumns = new HashSet<string>(
                connection.Query("PRAGMA table_info(Services);")
                          .Select(row => (string)row.name) // 'row' is dynamic
            );

            // Use KeyValuePair instead of tuples
            var expectedColumns = new List<KeyValuePair<string, string>>
            {
                // Failure program columns
                new KeyValuePair<string, string>("FailureProgramPath", "ALTER TABLE Services ADD COLUMN FailureProgramPath TEXT;"),
                new KeyValuePair<string, string>("FailureProgramStartupDirectory", "ALTER TABLE Services ADD COLUMN FailureProgramStartupDirectory TEXT;"),
                new KeyValuePair<string, string>("FailureProgramParameters", "ALTER TABLE Services ADD COLUMN FailureProgramParameters TEXT;"),

                // Post-launch script columns
                new KeyValuePair<string, string>("PostLaunchExecutablePath", "ALTER TABLE Services ADD COLUMN PostLaunchExecutablePath TEXT;"),
                new KeyValuePair<string, string>("PostLaunchStartupDirectory", "ALTER TABLE Services ADD COLUMN PostLaunchStartupDirectory TEXT;"),
                new KeyValuePair<string, string>("PostLaunchParameters", "ALTER TABLE Services ADD COLUMN PostLaunchParameters TEXT;"),

                // PID
                new KeyValuePair<string, string>("Pid", "ALTER TABLE Services ADD COLUMN Pid INTEGER;"),

                // Debug Logs
                new KeyValuePair<string, string>("EnableDebugLogs", "ALTER TABLE Services ADD COLUMN EnableDebugLogs INTEGER;"),

                // DisplayName
                new KeyValuePair<string, string>("DisplayName", "ALTER TABLE Services ADD COLUMN DisplayName TEXT;"),

                // MaxRotations
                new KeyValuePair<string, string>("MaxRotations", "ALTER TABLE Services ADD COLUMN MaxRotations INTEGER;"),

                // Date Rotation
                new KeyValuePair<string, string>("EnableDateRotation", "ALTER TABLE Services ADD COLUMN EnableDateRotation INTEGER;"),
                new KeyValuePair<string, string>("DateRotationType", "ALTER TABLE Services ADD COLUMN DateRotationType INTEGER;"),

                // Start and Stop timeouts
                new KeyValuePair<string, string>("StartTimeout", "ALTER TABLE Services ADD COLUMN StartTimeout INTEGER;"),
                new KeyValuePair<string, string>("StopTimeout", "ALTER TABLE Services ADD COLUMN StopTimeout INTEGER;"),
            };

            foreach (var column in expectedColumns.Where(c => !existingColumns.Contains(c.Key)))
            {
                connection.Execute(column.Value);
            }
        }
    }
}
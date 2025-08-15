using Dapper;
using System.Data;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Provides helper methods to initialize the SQLite database schema for Servy.
/// </summary>
[ExcludeFromCodeCoverage]
public static class SQLiteDbInitializer
{
    /// <summary>
    /// Creates the <c>Services</c> table and necessary indexes if they do not exist.
    /// </summary>
    /// <param name="connection">An open database connection to execute commands on.</param>
    /// <remarks>
    /// This method uses Dapper to execute raw SQL commands. It ensures that the
    /// <c>Services</c> table is created with all expected columns and that an index
    /// on the lowercase <c>Name</c> column exists for case-insensitive lookups.
    /// </remarks>
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
    }
}

using System.Linq;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Centralized source of truth for all Service table operations.
    /// Dynamically builds SQL clauses to prevent column divergence using inline initialization.
    /// </summary>
    public static class SqlConstants
    {
        // SINGLE SOURCE OF TRUTH: Add standard columns here.
        // Do NOT add 'Name' or 'PreviousStopTimeout' here, as they require special handling below.
        private static readonly string[] StandardColumns =
        {
            // Main Tab
            "DisplayName",
            "Description",
            "ExecutablePath",
            "StartupDirectory",
            "Parameters",
            "StartupType",
            "Priority",
            "StartTimeout",
            "StopTimeout",
            "EnableConsoleUI",
            
            // Logging Tab
            "StdoutPath",
            "StderrPath",
            "EnableSizeRotation",
            "RotationSize",
            "EnableDateRotation",
            "DateRotationType",
            "MaxRotations",
            "UseLocalTimeForRotation",
            "EnableDebugLogs",
            
            // Recovery Tab
            "EnableHealthMonitoring",
            "HeartbeatInterval",
            "MaxFailedChecks",
            "RecoveryAction",
            "MaxRestartAttempts",
            "FailureProgramPath",
            "FailureProgramStartupDirectory",
            "FailureProgramParameters",
            
            // Advanced Tab
            "EnvironmentVariables",
            "ServiceDependencies",
            
            // LogOn Tab
            "RunAsLocalSystem",
            "UserAccount",
            "Password",
            
            // Pre-Launch Tab
            "PreLaunchExecutablePath",
            "PreLaunchStartupDirectory",
            "PreLaunchParameters",
            "PreLaunchEnvironmentVariables",
            "PreLaunchStdoutPath",
            "PreLaunchStderrPath",
            "PreLaunchTimeoutSeconds",
            "PreLaunchRetryAttempts",
            "PreLaunchIgnoreFailure",
            
            // Post-Launch Tab
            "PostLaunchExecutablePath",
            "PostLaunchStartupDirectory",
            "PostLaunchParameters",
            
            // Pre-Stop Tab
            "PreStopExecutablePath",
            "PreStopStartupDirectory",
            "PreStopParameters",
            "PreStopTimeoutSeconds",
            "PreStopLogAsError",
            
            // Post-Stop Tab
            "PostStopExecutablePath",
            "PostStopStartupDirectory",
            "PostStopParameters",
            
            // System / Active State
            "Pid",
            "ActiveStdoutPath",
            "ActiveStderrPath"
        };

        // 1. INSERT COLUMNS
        public static readonly string InsertColumns =
            "Name, " + string.Join(", ", StandardColumns) + ", PreviousStopTimeout";

        // 2. INSERT VALUES
        public static readonly string InsertValues =
            "@Name, " + string.Join(", ", StandardColumns.Select(c => $"@{c}")) + ", @PreviousStopTimeout";

        // 3. UPDATE SET
        // Includes 'Name' and specialized COALESCE logic for PreviousStopTimeout
        public static readonly string UpdateSet =
            "Name = @Name, " +
            string.Join(", ", StandardColumns.Select(c => $"{c} = @{c}")) +
            ", PreviousStopTimeout = COALESCE(@PreviousStopTimeout, PreviousStopTimeout)";

        // 4. UPSERT SET
        // Excludes 'Name' (as it's the conflict target) and specialized COALESCE logic for PreviousStopTimeout
        public static readonly string UpsertSet =
            string.Join(", ", StandardColumns.Select(c => $"{c} = excluded.{c}")) +
            ", PreviousStopTimeout = COALESCE(excluded.PreviousStopTimeout, Services.PreviousStopTimeout)";
    }
}
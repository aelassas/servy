namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Centralized source of truth for all Service table operations.
    /// Dynamically builds SQL clauses to prevent column divergence using inline initialization.
    /// </summary>
    public static class SqlConstants
    {
        /// <summary>
        /// Name of the table every statement in this class targets.
        /// </summary>
        public const string ServicesTableName = "Services";

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
            "CpuAffinity",
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
            "RecoveryOnCleanExit",
            "MaxRestartAttempts",
            "HeartbeatUrl",
            "HeartbeatUrlTimeoutSeconds",
            "EnableHeartbeatUrlFlags",
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

        /// <summary>
        /// Column list for an INSERT: Name, every standard column, then PreviousStopTimeout.
        /// </summary>
        public static readonly string InsertColumns =
            "Name, " + string.Join(", ", StandardColumns) + ", PreviousStopTimeout";

        /// <summary>
        /// Parameter list matching <see cref="InsertColumns"/>, in the same order.
        /// </summary>
        public static readonly string InsertValues =
            "@Name, " + string.Join(", ", StandardColumns.Select(c => $"@{c}")) + ", @PreviousStopTimeout";

        /// <summary>
        /// SET clause for an UPDATE. Includes Name, and assigns PreviousStopTimeout through COALESCE
        /// so a null incoming value preserves the stored one.
        /// </summary>
        public static readonly string UpdateSet =
            "Name = @Name, " +
            string.Join(", ", StandardColumns.Select(c => $"{c} = @{c}")) +
            ", PreviousStopTimeout = COALESCE(@PreviousStopTimeout, PreviousStopTimeout)";

        /// <summary>
        /// SET clause for the ON CONFLICT upsert. Excludes Name (the conflict target), and assigns
        /// PreviousStopTimeout through COALESCE so a null incoming value preserves the stored one.
        /// </summary>
        public static readonly string UpsertSet =
            string.Join(", ", StandardColumns.Select(c => $"{c} = excluded.{c}")) +
            ", PreviousStopTimeout = COALESCE(excluded.PreviousStopTimeout, Services.PreviousStopTimeout)";
    }
}

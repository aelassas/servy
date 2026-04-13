using System.Collections.Generic;
using System.Linq;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Centralized source of truth for all Service table operations.
    /// Dynamically builds SQL clauses to prevent column divergence.
    /// </summary>
    public static class SqlConstants
    {
        public static readonly string InsertColumns;
        public static readonly string InsertValues;
        public static readonly string UpdateSet;
        public static readonly string UpsertSet;

        static SqlConstants()
        {
            // SINGLE SOURCE OF TRUTH: Add standard columns here.
            // Do NOT add 'Name' or 'PreviousStopTimeout' here, as they require special handling below.
            string[] standardColumns =
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
                
                // Logging Tab
                "StdoutPath",
                "StderrPath",
                "EnableRotation",
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
            var insertCols = new List<string> { "Name" };
            insertCols.AddRange(standardColumns);
            insertCols.Add("PreviousStopTimeout");

            InsertColumns = string.Join(", ", insertCols);

            // 2. INSERT VALUES
            InsertValues = string.Join(", ", insertCols.Select(c => $"@{c}"));

            // 3. UPDATE SET
            // Includes 'Name' and specialized COALESCE logic for PreviousStopTimeout
            var updateCols = new List<string> { "Name = @Name" };
            updateCols.AddRange(standardColumns.Select(c => $"{c} = @{c}"));
            updateCols.Add("PreviousStopTimeout = COALESCE(@PreviousStopTimeout, PreviousStopTimeout)");

            UpdateSet = string.Join(", ", updateCols);

            // 4. UPSERT SET
            // Excludes 'Name' (as it's the conflict target) and specialized COALESCE logic for PreviousStopTimeout
            var upsertCols = new List<string>();
            upsertCols.AddRange(standardColumns.Select(c => $"{c} = excluded.{c}"));
            upsertCols.Add("PreviousStopTimeout = COALESCE(excluded.PreviousStopTimeout, Services.PreviousStopTimeout)");

            UpsertSet = string.Join(", ", upsertCols);
        }
    }
}
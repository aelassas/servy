using Servy.Core.DTOs;

namespace Servy.Core.UnitTests.Helpers
{
    /// <summary>
    /// Centralized factory for creating pre-configured ServiceDto fixtures to eliminate test duplication.
    /// </summary>
    public static class ServiceDtoFactory
    {
        /// <summary>
        /// Creates a fully populated DTO containing non-default values for every property context.
        /// </summary>
        /// <param name="suffix">An optional string suffix used to vary property text values and numbers for specialized provider lookups (e.g., "Xml").</param>
        /// <returns>A completely populated <see cref="ServiceDto"/> instance with specific non-default configuration criteria.</returns>
        public static ServiceDto CreateFull(string suffix = "")
        {
            return new ServiceDto
            {
                Name = $"Full{(string.IsNullOrEmpty(suffix) ? "Service" : suffix)}",
                DisplayName = $"Full {suffix} Display".Trim(),
                Description = $"{(string.IsNullOrEmpty(suffix) ? "" : suffix + " ")}Description".Trim(),
                ExecutablePath = string.IsNullOrEmpty(suffix) ? @"C:\App\exe.exe" : $@"C:\App\bin\{suffix.ToLower()}.exe",
                StartupDirectory = string.IsNullOrEmpty(suffix) ? @"C:\App" : @"C:\App\bin",
                Parameters = string.IsNullOrEmpty(suffix) ? "--arg" : "/start --verbose",
                StartupType = 2,
                Priority = string.IsNullOrEmpty(suffix) ? 128 : 32,
                StdoutPath = string.IsNullOrEmpty(suffix) ? "out.log" : @"C:\logs\out.log",
                StderrPath = string.IsNullOrEmpty(suffix) ? "err.log" : @"C:\logs\err.log",
                EnableSizeRotation = true,
                RotationSize = string.IsNullOrEmpty(suffix) ? 50 : 25,
                EnableDateRotation = true,
                DateRotationType = string.IsNullOrEmpty(suffix) ? 1 : 2,
                MaxRotations = string.IsNullOrEmpty(suffix) ? 10 : 5,
                UseLocalTimeForRotation = true,
                EnableHealthMonitoring = true,
                HeartbeatInterval = string.IsNullOrEmpty(suffix) ? 60 : 45,
                MaxFailedChecks = string.IsNullOrEmpty(suffix) ? 5 : 10,
                RecoveryAction = 1,
                MaxRestartAttempts = string.IsNullOrEmpty(suffix) ? 10 : 5,
                FailureProgramPath = string.IsNullOrEmpty(suffix) ? "fail.exe" : "reboot.exe",
                FailureProgramStartupDirectory = string.IsNullOrEmpty(suffix) ? "fail_dir" : @"C:\",
                FailureProgramParameters = string.IsNullOrEmpty(suffix) ? "fail_args" : "-f",
                EnvironmentVariables = string.IsNullOrEmpty(suffix) ? "VAR=1" : "PORT=8080;NODE_ENV=prod",
                ServiceDependencies = string.IsNullOrEmpty(suffix) ? "s1;s2" : "LanmanWorkstation;W32Time",
                RunAsLocalSystem = false,
                UserAccount = string.IsNullOrEmpty(suffix) ? "User" : @"DOMAIN\ServiceAccount",
                Password = string.IsNullOrEmpty(suffix) ? "Password" : "EncryptedPasswordString",
                PreLaunchExecutablePath = string.IsNullOrEmpty(suffix) ? "pre.exe" : "setup.exe",
                PreLaunchStartupDirectory = string.IsNullOrEmpty(suffix) ? "pre_dir" : @"C:\Temp",
                PreLaunchParameters = string.IsNullOrEmpty(suffix) ? "pre_args" : "--quiet",
                PreLaunchEnvironmentVariables = string.IsNullOrEmpty(suffix) ? "PVAR=1" : "SETUP=1",
                PreLaunchStdoutPath = string.IsNullOrEmpty(suffix) ? "pre_out.log" : "setup_out.log",
                PreLaunchStderrPath = string.IsNullOrEmpty(suffix) ? "pre_err.log" : "setup_err.log",
                PreLaunchTimeoutSeconds = string.IsNullOrEmpty(suffix) ? 45 : 120,
                PreLaunchRetryAttempts = string.IsNullOrEmpty(suffix) ? 2 : 3,
                PreLaunchIgnoreFailure = true,
                PostLaunchExecutablePath = string.IsNullOrEmpty(suffix) ? "post.exe" : "notify.exe",
                PostLaunchStartupDirectory = string.IsNullOrEmpty(suffix) ? "post_dir" : @"C:\",
                PostLaunchParameters = string.IsNullOrEmpty(suffix) ? "post_args" : "--started",
                EnableDebugLogs = true,
                StartTimeout = string.IsNullOrEmpty(suffix) ? 20 : 45,
                StopTimeout = string.IsNullOrEmpty(suffix) ? 25 : 60,
                PreStopExecutablePath = string.IsNullOrEmpty(suffix) ? "pre_stop.exe" : "cleanup.exe",
                PreStopStartupDirectory = string.IsNullOrEmpty(suffix) ? "pre_stop_dir" : @"C:\App",
                PreStopParameters = string.IsNullOrEmpty(suffix) ? "pre_stop_args" : "--force",
                PreStopTimeoutSeconds = string.IsNullOrEmpty(suffix) ? 15 : 30,
                PreStopLogAsError = true,
                PostStopExecutablePath = string.IsNullOrEmpty(suffix) ? "post_stop.exe" : "final.exe",
                PostStopStartupDirectory = string.IsNullOrEmpty(suffix) ? "post_stop_dir" : @"C:\",
                PostStopParameters = string.IsNullOrEmpty(suffix) ? "post_stop_args" : "--done"
            };
        }

        /// <summary>
        /// Creates a sample base service configuration optimized for export rules.
        /// </summary>
        /// <returns>A fully-populated sample <see cref="ServiceDto"/> target containing runtime properties, environment blocks, and explicit dependency strings.</returns>
        public static ServiceDto CreateSampleExport()
        {
            return new ServiceDto
            {
                Id = 1,
                Name = "MyService",
                Description = "Test service",
                ExecutablePath = "C:\\service.exe",
                StartupDirectory = "C:\\",
                Parameters = "-arg1 -arg2",
                StartupType = 2,
                Priority = 1,
                StdoutPath = "stdout.log",
                StderrPath = "stderr.log",
                EnableSizeRotation = true,
                RotationSize = 1024,
                EnableHealthMonitoring = true,
                HeartbeatInterval = 10,
                MaxFailedChecks = 3,
                RecoveryAction = 1,
                MaxRestartAttempts = 5,
                EnvironmentVariables = "VAR1=VAL1;VAR2=VAL2",
                ServiceDependencies = "dep1;dep2",
                RunAsLocalSystem = true,
                UserAccount = "user",
                Password = "pass",
                PreLaunchExecutablePath = "pre.exe",
                PreLaunchStartupDirectory = "C:\\pre",
                PreLaunchParameters = "-preArg",
                PreLaunchEnvironmentVariables = "PREVAR=VAL",
                PreLaunchStdoutPath = "preout.log",
                PreLaunchStderrPath = "preerr.log",
                PreLaunchTimeoutSeconds = 30,
                PreLaunchRetryAttempts = 2,
                PreLaunchIgnoreFailure = true
            };
        }

        /// <summary>
        /// Creates a structurally minimal DTO validated to successfully pass all core runtime rules.
        /// </summary>
        /// <returns>A lightweight <see cref="ServiceDto"/> passing standard mandatory validation layout parameters.</returns>
        public static ServiceDto CreateValidValidationBase()
        {
            return new ServiceDto
            {
                Name = "ValidService",
                ExecutablePath = "C:\\Windows\\System32\\notepad.exe",
                DisplayName = "Valid Display Name",
                Description = "A valid description",
                StartupDirectory = "C:\\Windows",
                StartTimeout = 30,
                StopTimeout = 30,
                EnableHealthMonitoring = false,
                RunAsLocalSystem = true
            };
        }
    }
}
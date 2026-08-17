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
            if (string.IsNullOrEmpty(suffix))
            {
                return new ServiceDto
                {
                    Name = "FullService",
                    DisplayName = "Full Display",
                    Description = "Description",
                    ExecutablePath = @"C:\App\exe.exe",
                    StartupDirectory = @"C:\App",
                    Parameters = "--arg",
                    StartupType = 2,
                    Priority = 128,
                    CpuAffinity = "0-3",
                    StdoutPath = "out.log",
                    StderrPath = "err.log",
                    EnableSizeRotation = true,
                    RotationSize = 50,
                    EnableDateRotation = true,
                    DateRotationType = 1,
                    MaxRotations = 10,
                    UseLocalTimeForRotation = true,
                    EnableHealthMonitoring = true,
                    HeartbeatInterval = 60,
                    MaxFailedChecks = 5,
                    RecoveryAction = 1,
                    RecoveryOnCleanExit = true,
                    MaxRestartAttempts = 10,
                    FailureProgramPath = "fail.exe",
                    FailureProgramStartupDirectory = "fail_dir",
                    FailureProgramParameters = "fail_args",
                    HeartbeatUrl = "http://localhost:9000/health",
                    HeartbeatUrlTimeoutSeconds = 5,
                    EnableHeartbeatUrlFlags = true,
                    EnvironmentVariables = "VAR=1",
                    ServiceDependencies = "s1;s2",
                    RunAsLocalSystem = false,
                    UserAccount = "User",
                    Password = "Password",
                    PreLaunchExecutablePath = "pre.exe",
                    PreLaunchStartupDirectory = "pre_dir",
                    PreLaunchParameters = "pre_args",
                    PreLaunchEnvironmentVariables = "PVAR=1",
                    PreLaunchStdoutPath = "pre_out.log",
                    PreLaunchStderrPath = "pre_err.log",
                    PreLaunchTimeoutSeconds = 45,
                    PreLaunchRetryAttempts = 2,
                    PreLaunchIgnoreFailure = true,
                    PostLaunchExecutablePath = "post.exe",
                    PostLaunchStartupDirectory = "post_dir",
                    PostLaunchParameters = "post_args",
                    EnableConsoleUI = true,
                    EnableDebugLogs = true,
                    StartTimeout = 20,
                    StopTimeout = 25,
                    PreStopExecutablePath = "pre_stop.exe",
                    PreStopStartupDirectory = "pre_stop_dir",
                    PreStopParameters = "pre_stop_args",
                    PreStopTimeoutSeconds = 15,
                    PreStopLogAsError = true,
                    PostStopExecutablePath = "post_stop.exe",
                    PostStopStartupDirectory = "post_stop_dir",
                    PostStopParameters = "post_stop_args"
                };
            }

            return new ServiceDto
            {
                Name = $"Full{suffix}",
                DisplayName = $"Full {suffix} Display",
                Description = $"{suffix} Description",
                ExecutablePath = $@"C:\App\bin\{suffix.ToLower()}.exe",
                StartupDirectory = @"C:\App\bin",
                Parameters = "/start --verbose",
                StartupType = 2,
                Priority = 32,
                CpuAffinity = "0,2,4",
                StdoutPath = @"C:\logs\out.log",
                StderrPath = @"C:\logs\err.log",
                EnableSizeRotation = true,
                RotationSize = 25,
                EnableDateRotation = true,
                DateRotationType = 2,
                MaxRotations = 5,
                UseLocalTimeForRotation = true,
                EnableHealthMonitoring = true,
                HeartbeatInterval = 45,
                MaxFailedChecks = 10,
                RecoveryAction = 1,
                RecoveryOnCleanExit = true,
                MaxRestartAttempts = 5,
                FailureProgramPath = "reboot.exe",
                FailureProgramStartupDirectory = @"C:\",
                FailureProgramParameters = "-f",
                HeartbeatUrl = "https://health.example.com/probe",
                HeartbeatUrlTimeoutSeconds = 15,
                EnableHeartbeatUrlFlags = true,
                EnvironmentVariables = "PORT=8080;NODE_ENV=prod",
                ServiceDependencies = "LanmanWorkstation;W32Time",
                RunAsLocalSystem = false,
                UserAccount = @"DOMAIN\ServiceAccount",
                Password = "EncryptedPasswordString",
                PreLaunchExecutablePath = "setup.exe",
                PreLaunchStartupDirectory = @"C:\Temp",
                PreLaunchParameters = "--quiet",
                PreLaunchEnvironmentVariables = "SETUP=1",
                PreLaunchStdoutPath = "setup_out.log",
                PreLaunchStderrPath = "setup_err.log",
                PreLaunchTimeoutSeconds = 120,
                PreLaunchRetryAttempts = 3,
                PreLaunchIgnoreFailure = true,
                PostLaunchExecutablePath = "notify.exe",
                PostLaunchStartupDirectory = @"C:\",
                PostLaunchParameters = "--started",
                EnableConsoleUI = true,
                EnableDebugLogs = true,
                StartTimeout = 45,
                StopTimeout = 60,
                PreStopExecutablePath = "cleanup.exe",
                PreStopStartupDirectory = @"C:\App",
                PreStopParameters = "--force",
                PreStopTimeoutSeconds = 30,
                PreStopLogAsError = true,
                PostStopExecutablePath = "final.exe",
                PostStopStartupDirectory = @"C:\",
                PostStopParameters = "--done"
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

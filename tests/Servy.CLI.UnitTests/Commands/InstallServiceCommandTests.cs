using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Models;
using Servy.CLI.Resources;
using Servy.CLI.Validation;
using Servy.Core.Common;
using Servy.Core.Config;
using Servy.Core.Enums;
using Servy.Core.Services;

namespace Servy.CLI.UnitTests.Commands
{
    [Collection("SequentialElevationTests")]
    public class InstallServiceCommandTests : IDisposable
    {
        private readonly Mock<IServiceManager> _mockServiceManager;
        private readonly Mock<IServiceInstallValidator> _mockValidator;
        private readonly InstallServiceCommand _command;
        private readonly string _wrapperExePath;
        private readonly string? _backupPath;

        /// <summary>
        /// The SERVY_* variables GetSecureValue consults, and the host values saved on entry.
        /// </summary>
        private static readonly string[] SecureEnvVarNames =
        {
            AppConfig.PasswordEnvVarName,
            AppConfig.ProcessParametersEnvVarName,
            AppConfig.EnvironmentVariablesEnvVarName,
            AppConfig.FailureProgramParametersEnvVarName,
            AppConfig.PreLaunchParametersEnvVarName,
            AppConfig.PreLaunchEnvironmentVariablesEnvVarName,
            AppConfig.PostLaunchParametersEnvVarName,
            AppConfig.PreStopParametersEnvVarName,
            AppConfig.PostStopParametersEnvVarName
        };

        private readonly Dictionary<string, string?> _savedEnvVars = new Dictionary<string, string?>();

        public InstallServiceCommandTests()
        {
            BaseCommand.BypassElevationCheck = true;

            // GetSecureValue lets a SERVY_* variable win over the command-line option, so a host
            // or CI machine that happens to have one set would silently change what the validator
            // and the service manager receive. Isolate the whole class from the host environment.
            foreach (var name in SecureEnvVarNames)
            {
                _savedEnvVars[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, null);
            }

            _mockServiceManager = new Mock<IServiceManager>();
            _mockValidator = new Mock<IServiceInstallValidator>();
            _command = new InstallServiceCommand(_mockServiceManager.Object, _mockValidator.Object);

            // Create a dummy Servy.Service.CLI.exe for the tests
            _wrapperExePath = AppConfig.GetServyCLIServicePath();
            Directory.CreateDirectory(Path.GetDirectoryName(_wrapperExePath)!);

            // RELEASE BUILD SAFETY GUARD: If a live installation binary already occupies the target directory,
            // squirrel it away safely inside a temporary backup file profile to prevent clobbering.
            if (File.Exists(_wrapperExePath))
            {
                _backupPath = Path.Combine(Path.GetTempPath(), $"Servy_Backup_CLI_{Guid.NewGuid():N}.bak");
                File.Copy(_wrapperExePath, _backupPath, overwrite: true);
            }

            File.WriteAllText(_wrapperExePath, "dummy content");
        }

        [Fact]
        public void Constructor_NullServiceManager_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceManager? nullManager = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>("serviceManager", () => new InstallServiceCommand(nullManager!, _mockValidator.Object));
        }

        [Fact]
        public void Constructor_NullValidator_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceInstallValidator? nullValidator = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>("validator", () => new InstallServiceCommand(_mockServiceManager.Object, nullValidator!));
        }

        [Fact]
        public async Task Execute_ValidOptions_ReturnsSuccess()
        {
            // Arrange
            var options = new CLI.Options.InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));

            _mockServiceManager.Setup(sm => sm.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success());

            // Act
            var result = await _command.ExecuteAsync(options, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(string.Format(Strings.Msg_InstallSuccess, options.ServiceName), result.Message);
        }

        [Fact]
        public async Task Execute_ValidationFails_ReturnsFailure()
        {
            // Arrange
            var options = new CLI.Options.InstallServiceOptions();
            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Fail("Validation error."));

            // Act
            var result = await _command.ExecuteAsync(options, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("Validation error.", result.Message);
        }

        [Fact]
        public async Task Execute_MissingWrapperExecutable_ReturnsFailure()
        {
            // Arrange
            var options = new CLI.Options.InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };

            // Validation passes, guiding the runtime flow straight down into the wrapper file check block
            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));

            // Interrupt-Driven Modification: Safely delete the dummy fixture file to emulate a clean missing binary state
            if (File.Exists(_wrapperExePath))
            {
                File.Delete(_wrapperExePath);
            }

            // Act
            var result = await _command.ExecuteAsync(options, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(string.Format(Strings.Msg_WrapperNotFound, _wrapperExePath), result.Message);
        }

        [Fact]
        public async Task Execute_ServiceManagerFails_ReturnsFailure()
        {
            // Arrange
            var options = new CLI.Options.InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));

            _mockServiceManager.Setup(sm => sm.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Failure("Failed to install service."));

            // Act
            var result = await _command.ExecuteAsync(options, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("Failed to install service.", result.Message);
        }

        [Fact]
        public async Task Execute_UnauthorizedAccessException_ReturnsFailure()
        {
            // Arrange
            var options = new CLI.Options.InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));

            _mockServiceManager.Setup(sm => sm.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .Throws<UnauthorizedAccessException>();

            // Act
            var result = await _command.ExecuteAsync(options, TestContext.Current.CancellationToken);

            // Assert
            // Assert the resource rather than the first two English words of its value, and pin
            // the {0} verb argument the substring form left unchecked.
            Assert.False(result.IsSuccess);
            Assert.Equal(string.Format(Strings.Msg_AdminPrivilegesRequired, "install"), result.Message);
        }

        [Fact]
        public async Task Execute_GenericException_ReturnsFailure()
        {
            // Arrange
            var options = new CLI.Options.InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));

            _mockServiceManager.Setup(sm => sm.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .Throws<Exception>();

            // Act
            var result = await _command.ExecuteAsync(options, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains(string.Format(Strings.Msg_InstallServiceAction, options.ServiceName), result.Message);
        }

        [Fact]
        public async Task Execute_ValidOptions_MapsEveryOptionOntoInstallServiceOptions()
        {
            // Arrange
            // Every string input gets a distinct value so a transposition between two adjacent
            // same-typed fields is observable, and every bool is set to true because all the
            // AppConfig defaults are false, so a dropped assignment is observable too.
            var options = new CLI.Options.InstallServiceOptions
            {
                ServiceName = "MappingService",
                ServiceDisplayName = "Mapping Display Name",
                ServiceDescription = "Mapping description",
                ProcessPath = "C:\\map\\app.exe",
                StartupDirectory = "C:\\map\\workdir",
                ProcessParameters = "--real-args",
                ServiceStartType = "Manual",
                ProcessPriority = "High",
                CpuAffinity = "3",
                StartTimeout = "31",
                StopTimeout = "32",
                EnableConsoleUI = true,
                StdoutPath = "C:\\map\\out.log",
                StderrPath = "C:\\map\\err.log",
                EnableSizeRotation = true,
                RotationSize = "7",
                EnableDateRotation = true,
                DateRotationType = "Weekly",
                MaxRotations = "33",
                UseLocalTimeForRotation = true,
                EnableDebugLogs = true,
                EnableHealthMonitoring = true,
                HeartbeatInterval = "34",
                MaxFailedChecks = "35",
                RecoveryAction = "RestartProcess",
                RecoveryOnCleanExit = true,
                MaxRestartAttempts = "36",
                HeartbeatUrl = "https://mapping.example/health",
                HeartbeatUrlTimeoutSeconds = "17",
                EnableHeartbeatUrlFlags = true,
                FailureProgramPath = "C:\\map\\failure.exe",
                FailureProgramStartupDir = "C:\\map\\failure-dir",
                FailureProgramParameters = "--failure-args",
                EnvironmentVariables = "MAP_ENV=1",
                ServiceDependencies = "MapDependency",
                User = "  MapUser  ",
                Password = "MapPassword",
                PreLaunchPath = "C:\\map\\pre-launch.exe",
                PreLaunchStartupDir = "C:\\map\\pre-launch-dir",
                PreLaunchParameters = "--pre-launch-args",
                PreLaunchEnvironmentVariables = "MAP_PRE_LAUNCH_ENV=1",
                PreLaunchStdoutPath = "C:\\map\\pre-launch-out.log",
                PreLaunchStderrPath = "C:\\map\\pre-launch-err.log",
                PreLaunchTimeout = "38",
                PreLaunchRetryAttempts = "39",
                PreLaunchIgnoreFailure = true,
                PostLaunchPath = "C:\\map\\post-launch.exe",
                PostLaunchStartupDir = "C:\\map\\post-launch-dir",
                PostLaunchParameters = "--post-launch-args",
                PreStopPath = "C:\\map\\pre-stop.exe",
                PreStopStartupDir = "C:\\map\\pre-stop-dir",
                PreStopParameters = "--pre-stop-args",
                PreStopTimeout = "40",
                PreStopLogAsError = true,
                PostStopPath = "C:\\map\\post-stop.exe",
                PostStopStartupDir = "C:\\map\\post-stop-dir",
                PostStopParameters = "--post-stop-args"
            };

            InstallServiceOptions? captured = null;

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));

            _mockServiceManager.Setup(sm => sm.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .Callback<InstallServiceOptions, CancellationToken>((o, _) => captured = o)
                .ReturnsAsync(OperationResult.Success());

            // Act
            var result = await _command.ExecuteAsync(options, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(captured);

            Assert.Equal("MappingService", captured!.ServiceName);
            Assert.Equal("Mapping Display Name", captured.DisplayName);
            Assert.Equal("Mapping description", captured.Description);
            Assert.Equal(_wrapperExePath, captured.WrapperExePath);
            Assert.Equal("C:\\map\\app.exe", captured.RealExePath);
            Assert.Equal("C:\\map\\workdir", captured.StartupDirectory);
            Assert.Equal("--real-args", captured.RealArgs);
            Assert.Equal(ServiceStartType.Manual, captured.StartType);
            Assert.Equal(ProcessPriority.High, captured.ProcessPriority);
            Assert.Equal("3", captured.CpuAffinity);
            Assert.Equal(31, captured.StartTimeout);
            Assert.Equal(32, captured.StopTimeout);
            Assert.True(captured.EnableConsoleUI);

            // Log paths and rotation
            Assert.Equal("C:\\map\\out.log", captured.StdoutPath);
            Assert.Equal("C:\\map\\err.log", captured.StderrPath);
            Assert.True(captured.EnableSizeRotation);
            Assert.Equal(AppConfig.ToBytes(7), captured.RotationSizeInBytes);
            Assert.True(captured.EnableDateRotation);
            Assert.Equal(DateRotationType.Weekly, captured.DateRotationType);
            Assert.Equal(33, captured.MaxRotations);
            Assert.True(captured.UseLocalTimeForRotation);
            Assert.True(captured.EnableDebugLogs);

            // Health monitoring
            Assert.True(captured.EnableHealthMonitoring);
            Assert.Equal(34, captured.HeartbeatInterval);
            Assert.Equal(35, captured.MaxFailedChecks);
            Assert.Equal(RecoveryAction.RestartProcess, captured.RecoveryAction);
            Assert.True(captured.RecoveryOnCleanExit);
            Assert.Equal(36, captured.MaxRestartAttempts);
            Assert.Equal("https://mapping.example/health", captured.HeartbeatUrl);
            Assert.Equal(17, captured.HeartbeatUrlTimeoutSeconds);
            Assert.True(captured.EnableHeartbeatUrlFlags);

            // Failure program
            Assert.Equal("C:\\map\\failure.exe", captured.FailureProgramPath);
            Assert.Equal("C:\\map\\failure-dir", captured.FailureProgramStartupDirectory);
            Assert.Equal("--failure-args", captured.FailureProgramExecutableArgs);

            Assert.Equal("MAP_ENV=1", captured.EnvironmentVariables);
            Assert.Equal("MapDependency", captured.ServiceDependencies);

            // The account name is trimmed on the way through (#5949)
            Assert.Equal("MapUser", captured.Username);
            Assert.Equal("MapPassword", captured.Password);

            // Pre-Launch
            Assert.Equal("C:\\map\\pre-launch.exe", captured.PreLaunchExePath);
            Assert.Equal("C:\\map\\pre-launch-dir", captured.PreLaunchStartupDirectory);
            Assert.Equal("--pre-launch-args", captured.PreLaunchArgs);
            Assert.Equal("MAP_PRE_LAUNCH_ENV=1", captured.PreLaunchEnvironmentVariables);
            Assert.Equal("C:\\map\\pre-launch-out.log", captured.PreLaunchStdoutPath);
            Assert.Equal("C:\\map\\pre-launch-err.log", captured.PreLaunchStderrPath);
            Assert.Equal(38, captured.PreLaunchTimeout);
            Assert.Equal(39, captured.PreLaunchRetryAttempts);
            Assert.True(captured.PreLaunchIgnoreFailure);

            // Post-Launch
            Assert.Equal("C:\\map\\post-launch.exe", captured.PostLaunchExePath);
            Assert.Equal("C:\\map\\post-launch-dir", captured.PostLaunchStartupDirectory);
            Assert.Equal("--post-launch-args", captured.PostLaunchArgs);

            // Pre-Stop
            Assert.Equal("C:\\map\\pre-stop.exe", captured.PreStopExePath);
            Assert.Equal("C:\\map\\pre-stop-dir", captured.PreStopStartupDirectory);
            Assert.Equal("--pre-stop-args", captured.PreStopArgs);
            Assert.Equal(40, captured.PreStopTimeout);
            Assert.True(captured.PreStopLogAsError);

            // Post-Stop
            Assert.Equal("C:\\map\\post-stop.exe", captured.PostStopExePath);
            Assert.Equal("C:\\map\\post-stop-dir", captured.PostStopStartupDirectory);
            Assert.Equal("--post-stop-args", captured.PostStopArgs);
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, true, true)]
        public async Task Execute_RotationFlags_EnableSizeRotationIsTheOrOfBothFlags(bool enableRotation, bool enableSizeRotation, bool expected)
        {
            // Arrange
            var options = new CLI.Options.InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe",
                EnableRotation = enableRotation,
                EnableSizeRotation = enableSizeRotation
            };

            InstallServiceOptions? captured = null;

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));

            _mockServiceManager.Setup(sm => sm.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .Callback<InstallServiceOptions, CancellationToken>((o, _) => captured = o)
                .ReturnsAsync(OperationResult.Success());

            // Act
            await _command.ExecuteAsync(options, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(captured);
            Assert.Equal(expected, captured!.EnableSizeRotation);
        }

        [Theory]
        [InlineData(AppConfig.PasswordEnvVarName, nameof(CLI.Options.InstallServiceOptions.Password))]
        [InlineData(AppConfig.ProcessParametersEnvVarName, nameof(CLI.Options.InstallServiceOptions.ProcessParameters))]
        [InlineData(AppConfig.EnvironmentVariablesEnvVarName, nameof(CLI.Options.InstallServiceOptions.EnvironmentVariables))]
        [InlineData(AppConfig.FailureProgramParametersEnvVarName, nameof(CLI.Options.InstallServiceOptions.FailureProgramParameters))]
        [InlineData(AppConfig.PreLaunchParametersEnvVarName, nameof(CLI.Options.InstallServiceOptions.PreLaunchParameters))]
        [InlineData(AppConfig.PreLaunchEnvironmentVariablesEnvVarName, nameof(CLI.Options.InstallServiceOptions.PreLaunchEnvironmentVariables))]
        [InlineData(AppConfig.PostLaunchParametersEnvVarName, nameof(CLI.Options.InstallServiceOptions.PostLaunchParameters))]
        [InlineData(AppConfig.PreStopParametersEnvVarName, nameof(CLI.Options.InstallServiceOptions.PreStopParameters))]
        [InlineData(AppConfig.PostStopParametersEnvVarName, nameof(CLI.Options.InstallServiceOptions.PostStopParameters))]
        public async Task Execute_SecureEnvVarSet_OverridesCommandLineOption(string envVarName, string propertyName)
        {
            // Arrange
            var property = typeof(CLI.Options.InstallServiceOptions).GetProperty(propertyName);
            Assert.NotNull(property);

            var options = new CLI.Options.InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe"
            };
            property!.SetValue(options, "from-command-line");

            Environment.SetEnvironmentVariable(envVarName, "from-environment");

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));
            _mockServiceManager.Setup(sm => sm.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success());

            // Act
            var result = await _command.ExecuteAsync(options, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("from-environment", property.GetValue(options));
        }

        [Fact]
        public async Task Execute_SecureEnvVarIsWhitespaceOnly_KeepsCommandLineOption()
        {
            // Arrange
            var options = new CLI.Options.InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe",
                Password = "from-command-line"
            };

            Environment.SetEnvironmentVariable(AppConfig.PasswordEnvVarName, "   ");

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));
            _mockServiceManager.Setup(sm => sm.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success());

            // Act
            var result = await _command.ExecuteAsync(options, TestContext.Current.CancellationToken);

            // Assert
            // A whitespace-only variable is warned about and ignored, never used as the value.
            Assert.True(result.IsSuccess);
            Assert.Equal("from-command-line", options.Password);
        }

        [Fact]
        public async Task Execute_SecureEnvVarUnset_KeepsCommandLineOption()
        {
            // Arrange
            var options = new CLI.Options.InstallServiceOptions
            {
                ServiceName = "TestService",
                ProcessPath = "C:\\path\\to\\app.exe",
                Password = "from-command-line"
            };

            // The constructor already cleared every SERVY_* variable, so this is the unset branch.
            Assert.Null(Environment.GetEnvironmentVariable(AppConfig.PasswordEnvVarName));

            _mockValidator.Setup(v => v.Validate(options)).Returns(CommandResult.Ok(""));
            _mockServiceManager.Setup(sm => sm.InstallServiceAsync(It.IsAny<InstallServiceOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(OperationResult.Success());

            // Act
            var result = await _command.ExecuteAsync(options, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("from-command-line", options.Password);
        }

        public void Dispose()
        {
            // Clean up the dummy file
            if (File.Exists(_wrapperExePath))
            {
                try
                {
                    File.Delete(_wrapperExePath);
                }
                catch
                {
                    // Prevent disposal failures from masking core runtime assertions
                }
            }

            // RELEASE BUILD SAFETY GUARD: Restore original, verified production binary
            // if a backup transaction was initialized during the arrangement phase.
            if (!string.IsNullOrEmpty(_backupPath) && File.Exists(_backupPath))
            {
                try
                {
                    File.Copy(_backupPath, _wrapperExePath, overwrite: true);
                    File.Delete(_backupPath);
                }
                catch
                {
                    // Best-effort recovery catch
                }
            }

            // Restores the host's SERVY_* variables saved during the arrangement phase.
            foreach (var saved in _savedEnvVars)
            {
                Environment.SetEnvironmentVariable(saved.Key, saved.Value);
            }

            // Resets process-wide static state altered during test execution.
            BaseCommand.BypassElevationCheck = false;
        }
    }
}

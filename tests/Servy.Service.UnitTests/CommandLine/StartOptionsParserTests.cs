using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Service.CommandLine;
using System;
using System.Diagnostics;
using Xunit;

namespace Servy.Service.UnitTests.CommandLine
{
    public class StartOptionsParserTests
    {
        private readonly Mock<IServiceRepository> _mockRepository;
        private readonly Mock<IProcessHelper> _mockProcessHelper;

        public StartOptionsParserTests()
        {
            _mockRepository = new Mock<IServiceRepository>();
            _mockProcessHelper = new Mock<IProcessHelper>();

            // Setup default lenient path resolution to allow standard setup to pass cleanly
            _mockProcessHelper
                .Setup(p => p.ResolvePath(It.IsAny<string>()))
                .Returns<string>(input => input);
        }

        #region Guard Clause & Argument Exception Tests

        [Fact]
        public void Parse_NullArguments_ThrowsArgumentException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                StartOptionsParser.Parse(_mockRepository.Object, _mockProcessHelper.Object, null));

            Assert.Contains("No arguments provided", ex.Message);
        }

        [Fact]
        public void Parse_EmptyArgumentsArray_ThrowsArgumentException()
        {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                StartOptionsParser.Parse(_mockRepository.Object, _mockProcessHelper.Object, new string[0]));

            Assert.Contains("No arguments provided", ex.Message);
        }

        [Fact]
        public void Parse_ArgumentsLengthIsOne_ThrowsArgumentExceptionForEmptyServiceName()
        {
            // Arrange
            // fullArgs[0] is typically the executable name. Missing fullArgs[1] means service name evaluates to string.Empty
            string[] args = { "Servy.Service.exe" };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                StartOptionsParser.Parse(_mockRepository.Object, _mockProcessHelper.Object, args));

            Assert.Contains("Service name is empty!", ex.Message);
        }

        [Theory]
        [InlineData(" ")]
        [InlineData("\t")]
        public void Parse_WhitespaceServiceName_ThrowsArgumentException(string invalidName)
        {
            // Arrange
            string[] args = { "Servy.Service.exe", invalidName };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() =>
                StartOptionsParser.Parse(_mockRepository.Object, _mockProcessHelper.Object, args));

            Assert.Contains("Service name is empty!", ex.Message);
        }

        [Fact]
        public void Parse_ServiceNotFoundInDatabase_ThrowsInvalidOperationException()
        {
            // Arrange
            string serviceName = "MissingService";
            string[] args = { "Servy.Service.exe", serviceName };

            // Simply pass null directly. 
            // Moq's static typing will resolve this to the Returns(ServiceDto) overload.
            _mockRepository
                .Setup(r => r.GetByName(serviceName, It.IsAny<bool>()))
                .Returns((ServiceDto)null);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                StartOptionsParser.Parse(_mockRepository.Object, _mockProcessHelper.Object, args));

            Assert.Contains($"Service {serviceName} not found in the database!", ex.Message);
        }

        #endregion

        #region Happy Path & Fallback Ternary Mapping Tests

        [Fact]
        public void Parse_ValidDatabaseRecordWithValues_PopulatesStartOptionsCorrectly()
        {
            // Arrange
            string serviceName = "ProductionWorker";
            string[] args = { "Servy.Service.exe", serviceName };

            var serviceDto = new ServiceDto
            {
                ExecutablePath = @"C:\App\worker.exe",
                Parameters = @"--port 8080",
                StartupDirectory = @"C:\App",
                Priority = 4, // High
                EnableConsoleUI = true,
                StdoutPath = @"C:\Logs\stdout.log",
                StderrPath = @"C:\Logs\stderr.log",
                RotationSize = 50, // 50 MB
                UseLocalTimeForRotation = true,
                EnableHealthMonitoring = true,
                HeartbeatInterval = 15,
                MaxFailedChecks = 3,
                RecoveryAction = 1, // RestartService
                RecoveryOnCleanExit = false,
                MaxRestartAttempts = 5,
                EnvironmentVariables = "ENV=PROD;THEME=DARK",

                // Pre-Launch variants
                PreLaunchExecutablePath = @"C:\App\init.exe",
                PreLaunchStartupDirectory = @"C:\App\init",
                PreLaunchParameters = "--clean",
                PreLaunchEnvironmentVariables = "INIT=TRUE",
                PreLaunchStdoutPath = @"C:\Logs\init_out.log",
                PreLaunchStderrPath = @"C:\Logs\init_err.log",
                PreLaunchTimeoutSeconds = 45,
                PreLaunchRetryAttempts = 2,
                PreLaunchIgnoreFailure = true,

                // Failure actions
                FailureProgramPath = @"C:\App\alert.exe",
                FailureProgramStartupDirectory = @"C:\App\alert",
                FailureProgramParameters = "--notify admin",

                // Post-Launch, Pre-Stop, Post-Stop metadata
                PostLaunchExecutablePath = @"C:\App\post.exe",
                PostLaunchStartupDirectory = @"C:\App\post_dir",
                PostLaunchParameters = "--sync",
                DateRotationType = 0, // Daily
                EnableSizeRotation = true,
                EnableDateRotation = true,
                MaxRotations = 10,
                StartTimeout = 90,
                StopTimeout = 60,
                PreStopExecutablePath = @"C:\App\pre_stop.exe",
                PreStopStartupDirectory = @"C:\App\pre_stop_dir",
                PreStopParameters = "--drain",
                PreStopTimeoutSeconds = 20,
                PreStopLogAsError = true,
                PostStopExecutablePath = @"C:\App\post_stop.exe",
                PostStopStartupDirectory = @"C:\App\post_stop_dir",
                PostStopParameters = "--cleanup"
            };

            _mockRepository.Setup(r => r.GetByName(serviceName, It.IsAny<bool>())).Returns(serviceDto);

            // Act
            var result = StartOptionsParser.Parse(_mockRepository.Object, _mockProcessHelper.Object, args);

            // Assert main mappings
            Assert.Equal(serviceName, result.ServiceName);
            Assert.Equal(@"C:\App\worker.exe", result.ExecutablePath);
            Assert.Equal(@"--port 8080", result.ExecutableArgs);
            Assert.Equal(ProcessPriorityClass.High, result.Priority);
            Assert.True(result.EnableConsoleUI);
            Assert.Equal(50 * 1024 * 1024L, result.RotationSizeInBytes); // ToBytes verification
            Assert.True(result.UseLocalTimeForRotation);

            // Assert Recovery and Lifecycle mapping profiles
            Assert.True(result.EnableHealthMonitoring);
            Assert.Equal(RecoveryAction.RestartService, result.RecoveryAction);
            Assert.Equal(45, result.PreLaunchTimeoutInSeconds);
            Assert.True(result.PreLaunchIgnoreFailure);
            Assert.Equal(@"C:\App\pre_stop.exe", result.PreStopExecutablePath);
            Assert.True(result.PreStopLogAsError);
        }

        [Fact]
        public void Parse_DatabaseRecordContainsNulls_AppliesAppConfigDefaults()
        {
            // Arrange
            string serviceName = "MinimalService";
            string[] args = { "Servy.Service.exe", serviceName };

            // Instantiate a DTO with all fields assigned to null to force fallback paths
            var sparseDto = new ServiceDto { Priority = null };
            _mockRepository.Setup(r => r.GetByName(serviceName, It.IsAny<bool>())).Returns(sparseDto);

            // Act
            var result = StartOptionsParser.Parse(_mockRepository.Object, _mockProcessHelper.Object, args);

            // Assert fallbacks are activated correctly using AppConfig thresholds
            Assert.Equal(StartOptionsParser.MapPriority(AppConfig.DefaultProcessPriority), result.Priority);
            Assert.Equal(AppConfig.DefaultEnableConsoleUI, result.EnableConsoleUI);
            Assert.Equal(AppConfig.DefaultUseLocalTimeForRotation, result.UseLocalTimeForRotation);
            Assert.Equal(AppConfig.DefaultEnableHealthMonitoring, result.EnableHealthMonitoring);
            Assert.Equal(AppConfig.DefaultHeartbeatInterval, result.HeartbeatInterval);
            Assert.Equal(AppConfig.DefaultMaxFailedChecks, result.MaxFailedChecks);
            Assert.Equal(AppConfig.DefaultRecoveryOnCleanExit, result.RecoveryOnCleanExit);
            Assert.Equal(AppConfig.DefaultMaxRestartAttempts, result.MaxRestartAttempts);
            Assert.Equal(AppConfig.DefaultPreLaunchTimeoutSeconds, result.PreLaunchTimeoutInSeconds);
            Assert.Equal(AppConfig.DefaultPreLaunchRetryAttempts, result.PreLaunchRetryAttempts);
            Assert.Equal(AppConfig.DefaultPreLaunchIgnoreFailure, result.PreLaunchIgnoreFailure);
            Assert.Equal(AppConfig.DefaultMaxRotations, result.MaxRotations);
            Assert.Equal(AppConfig.DefaultEnableSizeRotation, result.EnableSizeRotation);
            Assert.Equal(AppConfig.DefaultEnableDateRotation, result.EnableDateRotation);
            Assert.Equal(AppConfig.DefaultStartTimeout, result.StartTimeoutInSeconds);
            Assert.Equal(AppConfig.DefaultStopTimeout, result.StopTimeoutInSeconds);
            Assert.Equal(AppConfig.DefaultPreStopTimeoutSeconds, result.PreStopTimeoutInSeconds);
            Assert.Equal(AppConfig.DefaultPreStopLogAsError, result.PreStopLogAsError);
        }

        [Fact]
        public void Parse_HealthMonitoringDisabled_OverridesRecoveryActionToNone()
        {
            // Arrange
            string serviceName = "NoMonitorService";
            string[] args = { "Servy.Service.exe", serviceName };

            var serviceDto = new ServiceDto
            {
                EnableHealthMonitoring = false,
                RecoveryAction = 1, // RestartService - Should be completely ignored because monitoring is off
            };
            _mockRepository.Setup(r => r.GetByName(serviceName, It.IsAny<bool>())).Returns(serviceDto);

            // Act
            var result = StartOptionsParser.Parse(_mockRepository.Object, _mockProcessHelper.Object, args);

            // Assert
            Assert.False(result.EnableHealthMonitoring);
            Assert.Equal(RecoveryAction.None, result.RecoveryAction); // Short-circuit conditional block validation
        }

        #endregion

        #region Exception Resiliency Filter Validation Blocks

        [Fact]
        public void Parse_MalformedEnvironmentVariables_ReturnsEmptyListInsteadOfThrowing()
        {
            // Arrange
            string serviceName = "CorruptedEnvService";
            string[] args = { "Servy.Service.exe", serviceName };

            var serviceDto = new ServiceDto
            {
                // Passing an invalid environment format (missing variable payload values or malformed structural separators) 
                // ensures that the static EnvironmentVariableParser throws a FormatException.
                EnvironmentVariables = "MALFORMED_VARIABLE_WITHOUT_EQUALS_SIGN_OR_VALUE_TOKEN_CONTEXT"
            };
            _mockRepository.Setup(r => r.GetByName(serviceName, It.IsAny<bool>())).Returns(serviceDto);

            // Act
            var result = StartOptionsParser.Parse(_mockRepository.Object, _mockProcessHelper.Object, args);

            // Assert
            // The catch (FormatException) block intercepts the parsing failure, outputs an error trace, 
            // and returns an empty list, preventing the wrapper orchestration layout from crashing.
            Assert.NotNull(result.EnvironmentVariables);
            Assert.Empty(result.EnvironmentVariables);
        }

        [Theory]
        [InlineData(typeof(ArgumentException))]
        [InlineData(typeof(InvalidOperationException))]
        public void Parse_PathResolutionThrows_FallsBackToRawConfiguredPath(Type exceptionType)
        {
            // Arrange
            string serviceName = "FaultyPathService";
            string[] args = { "Servy.Service.exe", serviceName };
            string brokenPathInput = @"%INVALID_ENV_VAR_TOKEN%\target.exe";

            var serviceDto = new ServiceDto
            {
                ExecutablePath = brokenPathInput
            };

            _mockRepository.Setup(r => r.GetByName(serviceName, It.IsAny<bool>())).Returns(serviceDto);

            // Force the injected path utility framework to throw targeted exceptions on matching executions
            _mockProcessHelper
                .Setup(p => p.ResolvePath(brokenPathInput))
                .Throws((Exception)Activator.CreateInstance(exceptionType));

            // Act
            var result = StartOptionsParser.Parse(_mockRepository.Object, _mockProcessHelper.Object, args);

            // Assert
            // The catch filters handle the problem, log an error diagnostic, and return the raw configuration text string token intact.
            Assert.Equal(brokenPathInput, result.ExecutablePath);
        }

        #endregion

        #region ProcessPriority Switch Strategy Matrix

        [Theory]
        [InlineData(ProcessPriority.Idle, ProcessPriorityClass.Idle)]
        [InlineData(ProcessPriority.BelowNormal, ProcessPriorityClass.BelowNormal)]
        [InlineData(ProcessPriority.Normal, ProcessPriorityClass.Normal)]
        [InlineData(ProcessPriority.AboveNormal, ProcessPriorityClass.AboveNormal)]
        [InlineData(ProcessPriority.High, ProcessPriorityClass.High)]
        [InlineData(ProcessPriority.RealTime, ProcessPriorityClass.RealTime)]
        public void MapPriority_ValidEnumStates_ReturnCorrectSystemClass(ProcessPriority input, ProcessPriorityClass expected)
        {
            // Act
            var result = StartOptionsParser.MapPriority(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void MapPriority_UndefinedValueCast_ReturnsNormalDefaultClass()
        {
            // Arrange
            // Force an undefined integer allocation state choice cast into the enum container structure
            ProcessPriority corruptedPriority = (ProcessPriority)8888;

            // Act
            var result = StartOptionsParser.MapPriority(corruptedPriority);

            // Assert
            // Standard execution gracefully hits the fallback default condition path and returns Normal
            Assert.Equal(ProcessPriorityClass.Normal, result);
        }

        #endregion
    }
}
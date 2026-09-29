using Moq;
using Servy.CLI.Commands;
using Servy.CLI.Options;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Services;
using System.ServiceProcess;
using CliStrings = Servy.CLI.Resources.Strings;

namespace Servy.CLI.UnitTests.Commands
{
    /// <summary>
    /// Tests for the <c>show</c> verb, which renders stored service configuration for a human reader.
    /// The class does not derive from <see cref="ServiceCommandTestsBase{TCommand, TOptions}"/> because
    /// that skeleton assumes a single <see cref="IServiceManager"/> constructor argument and a required
    /// service name, and this command takes a repository as well and treats the name as optional.
    /// </summary>
    [Collection(ElevationTestCollection.Name)]
    public class ShowServiceCommandTests : IDisposable
    {
        private const string ServiceName = "TestService";

        private readonly Mock<IServiceRepository> _repository;
        private readonly Mock<IServiceManager> _serviceManager;
        private readonly ShowServiceCommand _command;

        public ShowServiceCommandTests()
        {
            // The verb runs the elevation pre-flight, so take the test seam explicitly rather than
            // relying on an elevated host or on a sibling class having left the flag set.
            BaseCommand.BypassElevationCheck = true;

            _repository = new Mock<IServiceRepository>();
            _serviceManager = new Mock<IServiceManager>();
            _command = new ShowServiceCommand(_repository.Object, _serviceManager.Object);
        }

        public void Dispose()
        {
            BaseCommand.BypassElevationCheck = false;
        }

        #region Helpers

        /// <summary>
        /// Builds a DTO shaped like the record an install actually persists: the identity and path
        /// columns a caller supplies, plus every boolean and numeric column
        /// <see cref="Servy.Core.Services.ServiceManager.InstallServiceAsync"/> fills from its
        /// <c>AppConfig</c> default.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <returns>A DTO no column of which is NULL unless an install leaves it NULL.</returns>
        /// <remarks>
        /// The fixture used to leave every flag and timeout NULL, which no install produces, so tests
        /// built on it asserted omissions a user never sees: <c>ServiceManager.InstallServiceAsync</c>
        /// maps each of these from a non-nullable <c>InstallServiceOptions</c> member that carries an
        /// <c>AppConfig</c> default, so the Recovery, Pre-Launch and Pre-Stop categories are always
        /// populated for a service Servy created. The defaults are referenced rather than copied, so a
        /// change to one moves the fixture with it - and fails the sample test below, which is the
        /// signal that the documented output needs regenerating too.
        /// </remarks>
        private static ServiceDto MinimalDto(string name = ServiceName) => new ServiceDto
        {
            Name = name,
            DisplayName = "Test Service",
            Description = "A test service",
            ExecutablePath = @"C:\apps\test.exe",
            StartupDirectory = @"C:\apps",
            Parameters = "--flag",
            StartupType = (int)ServiceStartType.AutomaticDelayedStart,
            Priority = (int)ProcessPriority.Normal,

            // No username was supplied, so an install stores this as true and leaves UserAccount null.
            RunAsLocalSystem = true,

            EnableSizeRotation = AppConfig.DefaultEnableSizeRotation,
            RotationSize = AppConfig.DefaultRotationSizeMB,
            EnableDateRotation = AppConfig.DefaultEnableDateRotation,
            DateRotationType = (int)AppConfig.DefaultDateRotationType,
            MaxRotations = AppConfig.DefaultMaxRotations,
            UseLocalTimeForRotation = AppConfig.DefaultUseLocalTimeForRotation,

            StartTimeout = AppConfig.DefaultStartTimeout,
            StopTimeout = AppConfig.DefaultStopTimeout,

            EnableHealthMonitoring = AppConfig.DefaultEnableHealthMonitoring,
            HeartbeatInterval = AppConfig.DefaultHeartbeatInterval,
            MaxFailedChecks = AppConfig.DefaultMaxFailedChecks,
            RecoveryAction = (int)AppConfig.DefaultRecoveryAction,
            RecoveryOnCleanExit = AppConfig.DefaultRecoveryOnCleanExit,
            MaxRestartAttempts = AppConfig.DefaultMaxRestartAttempts,
            HeartbeatUrlTimeoutSeconds = AppConfig.DefaultHeartbeatUrlTimeoutSeconds,
            EnableHeartbeatUrlFlags = AppConfig.DefaultEnableHeartbeatUrlFlags,

            PreLaunchTimeoutSeconds = AppConfig.DefaultPreLaunchTimeoutSeconds,
            PreLaunchRetryAttempts = AppConfig.DefaultPreLaunchRetryAttempts,
            PreLaunchIgnoreFailure = AppConfig.DefaultPreLaunchIgnoreFailure,

            PreStopTimeoutSeconds = AppConfig.DefaultPreStopTimeoutSeconds,
            PreStopLogAsError = AppConfig.DefaultPreStopLogAsError,

            EnableConsoleUI = AppConfig.DefaultEnableConsoleUI,
            EnableDebugLogs = AppConfig.DefaultEnableDebugLogs
        };

        /// <summary>
        /// Builds a DTO with nothing but the columns the leading ungrouped block renders, so every
        /// label is short enough for the label column to sit on its <c>MinLabelColumnWidth</c> floor.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <returns>A DTO carrying only core-block columns.</returns>
        /// <remarks>
        /// Deliberately NOT the record an install produces - <see cref="MinimalDto"/> is that, and it
        /// fills the grouped categories, whose indented labels push the column past the floor. This
        /// fixture exists only so the floor itself stays observable to the alignment test; no other
        /// test should use it, because no user has a service shaped like this.
        /// </remarks>
        private static ServiceDto CoreOnlyDto(string name = ServiceName) => new ServiceDto
        {
            Name = name,
            DisplayName = "Test Service",
            Description = "A test service",
            ExecutablePath = @"C:\apps\test.exe",
            StartupDirectory = @"C:\apps",
            Parameters = "--flag",
            StartupType = (int)ServiceStartType.AutomaticDelayedStart,
            Priority = (int)ProcessPriority.Normal
        };

        /// <summary>Arranges the repository to return <paramref name="dto"/> for a by-name lookup.</summary>
        /// <param name="dto">The DTO to return, or <c>null</c> for a miss.</param>
        /// <param name="name">The service name the lookup is keyed on.</param>
        private void GivenService(ServiceDto? dto, string name = ServiceName)
        {
            _repository
                .Setup(r => r.GetByNameAsync(name, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);
        }

        /// <summary>Arranges the repository to return <paramref name="services"/> for the list query.</summary>
        /// <param name="services">The services the search returns.</param>
        private void GivenServices(params ServiceDto[] services)
        {
            _repository
                .Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(services);
        }

        /// <summary>Arranges the SCM to report <paramref name="status"/> for every service.</summary>
        /// <param name="status">The status to report.</param>
        private void GivenStatus(ServiceControllerStatus status)
        {
            _serviceManager
                .Setup(sm => sm.GetServiceStatus(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(status);
        }

        /// <summary>Returns the rendered value of a labelled row across the entire report.</summary>
        /// <param name="report">The rendered report.</param>
        /// <param name="label">The row label to find.</param>
        /// <returns>The trimmed value, or <c>null</c> when the row is absent.</returns>
        private static string? RowValue(string? report, string label) => SectionRowValue(report, null, label);

        /// <summary>Returns the rendered value of a labelled row scoped to a specific section title.</summary>
        /// <param name="report">The rendered report.</param>
        /// <param name="sectionTitle">The section heading to scope the lookup to, or <c>null</c> for global.</param>
        /// <param name="label">The row label to find.</param>
        /// <returns>The trimmed value, or <c>null</c> when the row is absent.</returns>
        private static string? SectionRowValue(string? report, string? sectionTitle, string label)
        {
            if (string.IsNullOrEmpty(report)) return null;

            var lines = report.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
            bool inSection = sectionTitle == null;

            foreach (var line in lines)
            {
                if (sectionTitle != null && line.Equals(sectionTitle, StringComparison.Ordinal))
                {
                    inSection = true;
                    continue;
                }

                if (sectionTitle != null && inSection && line.Length > 0 && !line.StartsWith("  ", StringComparison.Ordinal))
                {
                    // Hit another top-level section heading
                    break;
                }

                if (inSection)
                {
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith(label + " ", StringComparison.Ordinal) || trimmed.StartsWith(label + ":", StringComparison.Ordinal))
                    {
                        var idx = line.IndexOf(": ", StringComparison.Ordinal);
                        return idx < 0 ? string.Empty : line.Substring(idx + 2);
                    }
                }
            }

            return null;
        }

        #endregion

        #region Constructor

        [Fact]
        public void Constructor_NullRepository_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceRepository? nullRepository = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>("serviceRepository", () => new ShowServiceCommand(nullRepository!, _serviceManager.Object));
        }

        [Fact]
        public void Constructor_NullServiceManager_ThrowsArgumentNullException()
        {
            // Arrange
            IServiceManager? nullManager = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>("serviceManager", () => new ShowServiceCommand(_repository.Object, nullManager!));
        }

        #endregion

        #region Single-service mode

        [Fact]
        public async Task ExecuteAsync_ServiceNotInDatabase_ReturnsServiceNotFound()
        {
            // Arrange
            GivenService(null);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(Core.Resources.Strings.Msg_ServiceNotFound, result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_ServiceFound_StartsWithNameThenStatus()
        {
            // Arrange
            GivenService(MinimalDto());
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            var lines = (result.Message ?? string.Empty).Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
            Assert.True(result.IsSuccess);
            Assert.StartsWith(CliStrings.Msg_Show_Label_Name, lines[0]);
            Assert.StartsWith(CliStrings.Msg_Show_Label_Status, lines[1]);
            Assert.Equal(ServiceName, RowValue(result.Message, CliStrings.Msg_Show_Label_Name));
            Assert.Equal(nameof(ServiceControllerStatus.Running), RowValue(result.Message, CliStrings.Msg_Show_Label_Status));
        }

        [Fact]
        public async Task ExecuteAsync_ServiceFound_RendersPid()
        {
            // Arrange
            var dto = MinimalDto();
            dto.Pid = 4242;
            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal("4242", RowValue(result.Message, CliStrings.Msg_Show_Label_Pid));
        }

        [Fact]
        public async Task ExecuteAsync_ServiceFoundWithoutPid_RendersNotSetPlaceholder()
        {
            // Arrange
            var dto = MinimalDto();
            dto.Pid = null;
            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Stopped);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(CliStrings.Msg_Show_ValueNotSet, RowValue(result.Message, CliStrings.Msg_Show_Label_Pid));
        }

        [Fact]
        public async Task ExecuteAsync_ServiceFound_RendersEnumMemberNames()
        {
            // Arrange
            var dto = MinimalDto();
            dto.EnableHealthMonitoring = true;
            dto.RecoveryAction = (int)RecoveryAction.RestartService;
            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            // ServiceStartType is backed by uint, so this also pins the underlying-type conversion.
            Assert.Equal(nameof(ServiceStartType.AutomaticDelayedStart), RowValue(result.Message, CliStrings.Msg_Show_Label_StartupType));
            Assert.Equal(nameof(ProcessPriority.Normal), RowValue(result.Message, CliStrings.Msg_Show_Label_Priority));
            Assert.Equal(nameof(RecoveryAction.RestartService), SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Recovery, CliStrings.Msg_Show_Label_Recovery));
        }

        [Fact]
        public async Task ExecuteAsync_UndefinedEnumOrdinal_FallsBackToTheStoredNumber()
        {
            // Arrange
            var dto = MinimalDto();
            dto.Priority = 99;
            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal("99", RowValue(result.Message, CliStrings.Msg_Show_Label_Priority));
        }

        [Fact]
        public async Task ExecuteAsync_NegativeStartupType_FallsBackToTheStoredNumber()
        {
            // Arrange
            // ServiceStartType is uint-backed, so a negative stored ordinal cannot be converted and
            // takes FormatEnum's OverflowException arm rather than the IsDefined fallback.
            var dto = MinimalDto();
            dto.StartupType = -1;
            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("-1", RowValue(result.Message, CliStrings.Msg_Show_Label_StartupType));
        }

        [Fact]
        public async Task ExecuteAsync_PopulatedCategory_RendersItsHeadingAndRows()
        {
            // Arrange
            var dto = MinimalDto();
            dto.StdoutPath = @"C:\logs\out.log";
            dto.EnableSizeRotation = true;
            dto.RotationSize = 10;
            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains(CliStrings.Msg_Show_Group_Logs, result.Message);
            Assert.Equal(@"C:\logs\out.log", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Logs, CliStrings.Msg_Show_Label_Stdout));
            Assert.Equal(CliStrings.Msg_Show_Yes, SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Logs, CliStrings.Msg_Show_Label_SizeRotation));
            Assert.Equal(string.Format(CliStrings.Msg_Show_Megabytes, 10), SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Logs, CliStrings.Msg_Show_Label_RotationSize));
        }

        [Fact]
        public async Task ExecuteAsync_CategoryWithOnlyNullColumns_IsOmittedEntirely()
        {
            // Arrange
            GivenService(MinimalDto());
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            var lines = (result.Message ?? string.Empty).Split('\n').Select(l => l.TrimEnd('\r')).ToList();

            // A section is dropped when every one of its columns is NULL or explicitly disabled.
            Assert.DoesNotContain(CliStrings.Msg_Show_Group_Logs, lines);
            Assert.DoesNotContain(CliStrings.Msg_Show_Group_Recovery, lines);
            Assert.DoesNotContain(CliStrings.Msg_Show_Group_PreLaunch, lines);
            Assert.DoesNotContain(CliStrings.Msg_Show_Group_PostLaunch, lines);
            Assert.DoesNotContain(CliStrings.Msg_Show_Group_PreStop, lines);
            Assert.DoesNotContain(CliStrings.Msg_Show_Group_PostStop, lines);
            Assert.DoesNotContain(CliStrings.Msg_Show_Group_FailureProgram, lines);
            Assert.DoesNotContain(CliStrings.Msg_Show_Group_Environment, lines);
        }

        [Fact]
        public async Task ExecuteAsync_CategoryCarryingAStoredDefault_IsShownWhenFeatureIsActive()
        {
            // Arrange
            var dto = MinimalDto();
            dto.EnableHealthMonitoring = true;
            dto.PreLaunchExecutablePath = @"C:\apps\prelaunch.exe";
            dto.PreStopExecutablePath = @"C:\apps\prestop.exe";
            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains(CliStrings.Msg_Show_Group_Recovery, result.Message);
            Assert.Equal(CliStrings.Msg_Show_Yes, SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Recovery, CliStrings.Msg_Show_Label_HealthCheck));
            Assert.Equal(
                string.Format(CliStrings.Msg_Show_Seconds, AppConfig.DefaultHeartbeatInterval),
                SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Recovery, CliStrings.Msg_Show_Label_Heartbeat));

            Assert.Contains(CliStrings.Msg_Show_Group_PreLaunch, result.Message);
            Assert.Equal(CliStrings.Msg_Show_No, SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PreLaunch, CliStrings.Msg_Show_Label_IgnoreFailure));

            Assert.Contains(CliStrings.Msg_Show_Group_PreStop, result.Message);
            Assert.Equal(CliStrings.Msg_Show_No, SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PreStop, CliStrings.Msg_Show_Label_LogAsError));

            // And the Account section: no username means an install stores Local System true, so the
            // row is there rather than the section being dropped.
            Assert.Contains(CliStrings.Msg_Show_Group_Account, result.Message);
            Assert.Equal(CliStrings.Msg_Show_Yes, SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Account, CliStrings.Msg_Show_Label_RunAsLocalSystem));
        }

        [Fact]
        public async Task ExecuteAsync_LogsSection_ShowsDashForEmptyValuesWhenActive()
        {
            // Arrange
            var dto = MinimalDto();
            dto.StdoutPath = @"C:\logs\out.log";
            dto.StderrPath = null;
            dto.ActiveStdoutPath = null;
            dto.ActiveStderrPath = null;
            dto.EnableSizeRotation = null;
            dto.RotationSize = null;
            dto.EnableDateRotation = null;
            dto.DateRotationType = null;
            dto.MaxRotations = null;
            dto.UseLocalTimeForRotation = null;

            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains(CliStrings.Msg_Show_Group_Logs, result.Message);
            Assert.Equal(@"C:\logs\out.log", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Logs, CliStrings.Msg_Show_Label_Stdout));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Logs, CliStrings.Msg_Show_Label_Stderr));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Logs, CliStrings.Msg_Show_Label_ActiveStdout));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Logs, CliStrings.Msg_Show_Label_ActiveStderr));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Logs, CliStrings.Msg_Show_Label_SizeRotation));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Logs, CliStrings.Msg_Show_Label_RotationSize));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Logs, CliStrings.Msg_Show_Label_DateRotation));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Logs, CliStrings.Msg_Show_Label_RotationPeriod));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Logs, CliStrings.Msg_Show_Label_MaxFiles));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Logs, CliStrings.Msg_Show_Label_LocalTimeRotation));
        }

        [Fact]
        public async Task ExecuteAsync_RecoverySection_ShowsDashForEmptyValuesWhenActive()
        {
            // Arrange
            var dto = MinimalDto();
            dto.EnableHealthMonitoring = true;
            dto.HeartbeatInterval = null;
            dto.MaxFailedChecks = null;
            dto.RecoveryAction = null;
            dto.RecoveryOnCleanExit = null;
            dto.MaxRestartAttempts = null;
            dto.HeartbeatUrl = null;
            dto.HeartbeatUrlTimeoutSeconds = null;
            dto.EnableHeartbeatUrlFlags = null;

            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains(CliStrings.Msg_Show_Group_Recovery, result.Message);
            Assert.Equal(CliStrings.Msg_Show_Yes, SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Recovery, CliStrings.Msg_Show_Label_HealthCheck));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Recovery, CliStrings.Msg_Show_Label_Heartbeat));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Recovery, CliStrings.Msg_Show_Label_MaxFailedChecks));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Recovery, CliStrings.Msg_Show_Label_Recovery));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Recovery, CliStrings.Msg_Show_Label_OnCleanExit));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Recovery, CliStrings.Msg_Show_Label_MaxAttempts));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Recovery, CliStrings.Msg_Show_Label_HeartbeatUrl));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Recovery, CliStrings.Msg_Show_Label_UrlTimeout));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Recovery, CliStrings.Msg_Show_Label_UrlFlags));
        }

        [Fact]
        public async Task ExecuteAsync_PreLaunchSection_ShowsDashForEmptyValuesWhenActive()
        {
            // Arrange
            var dto = MinimalDto();
            dto.PreLaunchExecutablePath = @"C:\apps\prelaunch.exe";
            dto.PreLaunchStartupDirectory = null;
            dto.PreLaunchParameters = null;
            dto.PreLaunchEnvironmentVariables = null;
            dto.PreLaunchStdoutPath = null;
            dto.PreLaunchStderrPath = null;
            dto.PreLaunchTimeoutSeconds = null;
            dto.PreLaunchRetryAttempts = null;
            dto.PreLaunchIgnoreFailure = null;

            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains(CliStrings.Msg_Show_Group_PreLaunch, result.Message);
            Assert.Equal(@"C:\apps\prelaunch.exe", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PreLaunch, CliStrings.Msg_Show_Label_Executable));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PreLaunch, CliStrings.Msg_Show_Label_StartupDir));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PreLaunch, CliStrings.Msg_Show_Label_Parameters));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PreLaunch, CliStrings.Msg_Show_Label_EnvironmentVariables));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PreLaunch, CliStrings.Msg_Show_Label_Stdout));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PreLaunch, CliStrings.Msg_Show_Label_Stderr));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PreLaunch, CliStrings.Msg_Show_Label_Timeout));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PreLaunch, CliStrings.Msg_Show_Label_RetryAttempts));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PreLaunch, CliStrings.Msg_Show_Label_IgnoreFailure));
        }

        [Fact]
        public async Task ExecuteAsync_PostLaunchSection_ShowsDashForEmptyValuesWhenActive()
        {
            // Arrange
            var dto = MinimalDto();
            dto.PostLaunchExecutablePath = @"C:\apps\postlaunch.exe";
            dto.PostLaunchStartupDirectory = null;
            dto.PostLaunchParameters = null;

            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains(CliStrings.Msg_Show_Group_PostLaunch, result.Message);
            Assert.Equal(@"C:\apps\postlaunch.exe", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PostLaunch, CliStrings.Msg_Show_Label_Executable));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PostLaunch, CliStrings.Msg_Show_Label_StartupDir));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PostLaunch, CliStrings.Msg_Show_Label_Parameters));
        }

        [Fact]
        public async Task ExecuteAsync_PreStopSection_ShowsDashForEmptyValuesWhenActive()
        {
            // Arrange
            var dto = MinimalDto();
            dto.PreStopExecutablePath = @"C:\apps\prestop.exe";
            dto.PreStopStartupDirectory = null;
            dto.PreStopParameters = null;
            dto.PreStopTimeoutSeconds = null;
            dto.PreStopLogAsError = null;

            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains(CliStrings.Msg_Show_Group_PreStop, result.Message);
            Assert.Equal(@"C:\apps\prestop.exe", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PreStop, CliStrings.Msg_Show_Label_Executable));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PreStop, CliStrings.Msg_Show_Label_StartupDir));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PreStop, CliStrings.Msg_Show_Label_Parameters));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PreStop, CliStrings.Msg_Show_Label_Timeout));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PreStop, CliStrings.Msg_Show_Label_LogAsError));
        }

        [Fact]
        public async Task ExecuteAsync_PostStopSection_ShowsDashForEmptyValuesWhenActive()
        {
            // Arrange
            var dto = MinimalDto();
            dto.PostStopExecutablePath = @"C:\apps\poststop.exe";
            dto.PostStopStartupDirectory = null;
            dto.PostStopParameters = null;

            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains(CliStrings.Msg_Show_Group_PostStop, result.Message);
            Assert.Equal(@"C:\apps\poststop.exe", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PostStop, CliStrings.Msg_Show_Label_Executable));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PostStop, CliStrings.Msg_Show_Label_StartupDir));
            Assert.Equal("-", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PostStop, CliStrings.Msg_Show_Label_Parameters));
        }

        [Fact]
        public async Task ExecuteAsync_RecordAnInstallProduces_RendersTheDocumentedSample()
        {
            // Arrange
            // The record behind the two sample outputs on the Servy-CLI wiki page: a user-account
            // service with log rotation configured and nothing else, carrying the same stored defaults
            // ServiceManager.InstallServiceAsync writes. This test exists so the published sample is
            // generated output rather than a hand-drawn sketch - if the renderer, a label or a default
            // changes, this fails and the wiki page has to be regenerated with it.
            var dto = MinimalDto("telegraf");
            dto.Pid = 7312;
            dto.DisplayName = "Telegraf Agent";
            dto.Description = "Metrics collection agent";
            dto.ExecutablePath = @"C:\Program Files\telegraf\telegraf.exe";
            dto.StartupDirectory = @"C:\Program Files\telegraf";
            dto.Parameters = "--config telegraf.conf --config-directory telegraf.d";
            dto.RunAsLocalSystem = false;
            dto.UserAccount = @".\telegraf-svc";
            dto.Password = "SuperSecret123!";
            dto.StdoutPath = @"C:\Program Files\telegraf\log\out.log";
            dto.StderrPath = @"C:\Program Files\telegraf\log\err.log";
            dto.EnableSizeRotation = true;
            dto.EnableDateRotation = true;
            dto.MaxRotations = 7;
            dto.EnvironmentVariables = "API_TOKEN=9f3c1a; API_HOST=example.internal";

            GivenService(dto, "telegraf");
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = "telegraf" };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            var expected = string.Join(Environment.NewLine, new[]
            {
                @"Name                    : telegraf",
                @"Status                  : Running",
                @"Pid                     : 7312",
                @"Display Name            : Telegraf Agent",
                @"Description             : Metrics collection agent",
                @"Startup Type            : AutomaticDelayedStart",
                @"Priority                : Normal",
                @"Executable              : C:\Program Files\telegraf\telegraf.exe",
                @"Startup Dir             : C:\Program Files\telegraf",
                @"Parameters              : ********",
                string.Empty,
                @"Account",
                @"  Local System          : No",
                @"  User Account          : .\telegraf-svc",
                @"  Password              : ********",
                string.Empty,
                @"Logs",
                @"  Stdout                : C:\Program Files\telegraf\log\out.log",
                @"  Stderr                : C:\Program Files\telegraf\log\err.log",
                @"  Active Stdout         : -",
                @"  Active Stderr         : -",
                @"  Size Rotation         : Yes",
                @"  Rotation Size         : 10 MB",
                @"  Date Rotation         : Yes",
                @"  Rotation Period       : Daily",
                @"  Max Files             : 7",
                @"  Local Time            : No",
                string.Empty,
                @"Timeouts",
                @"  Start                 : 10s",
                @"  Stop                  : 5s",
                string.Empty,
                @"Environment",
                @"  Environment Variables : ********",
                string.Empty,
                @"Other",
                @"  Console UI            : No",
                @"  Debug Logs            : No"
            });

            Assert.Equal(expected, result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_ServiceFound_NeverRendersTheStoredPassword()
        {
            // Arrange
            var dto = MinimalDto();
            dto.UserAccount = @".\svcuser";
            dto.RunAsLocalSystem = false; // what an install stores when a username is supplied
            dto.Password = "SuperSecret123!";
            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            // The account is shown; the credential never is. This is the contract the service already
            // documents for debug logging - sensitive data is never shown by the CLI or the module.
            Assert.Equal(@".\svcuser", SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Account, CliStrings.Msg_Show_Label_UserAccount));
            Assert.Equal(CliStrings.Msg_Show_Masked, SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Account, CliStrings.Msg_Show_Label_Password));
            Assert.DoesNotContain("SuperSecret123!", result.Message);
        }

        /// <summary>Builds a DTO whose every encrypted-at-rest column carries a distinctive value.</summary>
        /// <returns>A DTO populated for the masking tests.</returns>
        private static ServiceDto DtoWithEveryEncryptedField()
        {
            var dto = MinimalDto();
            dto.Parameters = "--config telegraf.conf";
            dto.EnvironmentVariables = "API_HOST=example.internal";
            dto.PreLaunchEnvironmentVariables = "PRELAUNCH_MODE=check";
            dto.PreLaunchParameters = "--warmup";
            dto.PostLaunchParameters = "--notify";
            dto.PreStopParameters = "--drain";
            dto.PostStopParameters = "--cleanup";
            dto.FailureProgramParameters = "--alert";
            dto.PreLaunchExecutablePath = @"C:\apps\pre.exe";
            dto.PostLaunchExecutablePath = @"C:\apps\post.exe";
            dto.PreStopExecutablePath = @"C:\apps\prestop.exe";
            dto.PostStopExecutablePath = @"C:\apps\poststop.exe";
            dto.FailureProgramPath = @"C:\apps\fail.exe";
            dto.Password = "SuperSecret123!";
            return dto;
        }

        /// <summary>The distinctive values of every revealable encrypted-at-rest column.</summary>
        private static readonly string[] EncryptedFieldValues =
        {
            "--config telegraf.conf",
            "API_HOST=example.internal",
            "PRELAUNCH_MODE=check",
            "--warmup",
            "--notify",
            "--drain",
            "--cleanup",
            "--alert"
        };

        [Fact]
        public async Task ExecuteAsync_WithoutDecryptFlag_ReadsTheRecordWithoutDecrypting()
        {
            // Arrange
            GivenService(MinimalDto());
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            // The default masks every encrypted column, so the plaintext is never produced at all.
            _repository.Verify(r => r.GetByNameAsync(ServiceName, false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WithDecryptFlag_ReadsTheRecordDecrypted()
        {
            // Arrange
            GivenService(MinimalDto());
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName, Decrypt = true };

            // Act
            await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            _repository.Verify(r => r.GetByNameAsync(ServiceName, true, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ByDefault_MasksEveryEncryptedAtRestField()
        {
            // Arrange
            GivenService(DtoWithEveryEncryptedField());
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(CliStrings.Msg_Show_Masked, RowValue(result.Message, CliStrings.Msg_Show_Label_Parameters));
            foreach (var value in EncryptedFieldValues)
            {
                Assert.DoesNotContain(value, result.Message);
            }
        }

        [Fact]
        public async Task ExecuteAsync_WithDecryptFlag_RevealsEveryEncryptedAtRestField()
        {
            // Arrange
            GivenService(DtoWithEveryEncryptedField());
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName, Decrypt = true };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal("--config telegraf.conf", RowValue(result.Message, CliStrings.Msg_Show_Label_Parameters));
            foreach (var value in EncryptedFieldValues)
            {
                Assert.Contains(value, result.Message);
            }
        }

        [Fact]
        public async Task ExecuteAsync_WithDecryptFlag_StillMasksThePassword()
        {
            // Arrange
            var dto = DtoWithEveryEncryptedField();
            dto.UserAccount = @".\svcuser";
            dto.RunAsLocalSystem = false; // what an install stores when a username is supplied
            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName, Decrypt = true };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            // --decrypt reveals the other eight; the password is the one column it must not unmask.
            Assert.Equal(CliStrings.Msg_Show_Masked, SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Account, CliStrings.Msg_Show_Label_Password));
            Assert.DoesNotContain("SuperSecret123!", result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_UnsetEncryptedField_IsOmittedRatherThanMasked()
        {
            // Arrange
            // MinimalDto leaves the environment-variable columns unset; a mask there would wrongly
            // imply a value exists.
            GivenService(MinimalDto());
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.DoesNotContain(CliStrings.Msg_Show_Group_Environment, result.Message);
            Assert.Null(SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Account, CliStrings.Msg_Show_Label_Password));
        }

        [Fact]
        public async Task ExecuteAsync_DecryptWithoutName_IsRefused()
        {
            // Arrange
            var opts = new ShowServiceOptions { Decrypt = true };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(CliStrings.Msg_Show_DecryptRequiresName, result.Message);
            _repository.Verify(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_ListMode_DoesNotPayForDecryption()
        {
            // Arrange
            GivenServices(MinimalDto("alpha"));
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions();

            // Act
            await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            // None of the six listed columns is encrypted at rest, so the list must not pay to decrypt.
            _repository.Verify(r => r.SearchAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()), Times.Once);
            _repository.Verify(r => r.SearchAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_StatusUnreadableForInstalledService_ReportsUnknown()
        {
            // Arrange
            GivenService(MinimalDto());
            _serviceManager.Setup(sm => sm.GetServiceStatus(ServiceName, It.IsAny<CancellationToken>())).Returns((ServiceControllerStatus?)null);
            _serviceManager.Setup(sm => sm.IsServiceInstalled(ServiceName, It.IsAny<CancellationToken>())).Returns(true);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(nameof(ServiceStatus.Unknown), RowValue(result.Message, CliStrings.Msg_Show_Label_Status));
        }

        [Fact]
        public async Task ExecuteAsync_ServiceInDatabaseButNotInScm_ReportsNotInstalled()
        {
            // Arrange
            GivenService(MinimalDto());
            _serviceManager.Setup(sm => sm.GetServiceStatus(ServiceName, It.IsAny<CancellationToken>())).Returns((ServiceControllerStatus?)null);
            _serviceManager.Setup(sm => sm.IsServiceInstalled(ServiceName, It.IsAny<CancellationToken>())).Returns(false);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(nameof(ServiceStatus.NotInstalled), RowValue(result.Message, CliStrings.Msg_Show_Label_Status));
        }

        [Fact]
        public async Task ExecuteAsync_CoreBlockOnly_AlignsEveryValueInOneColumn()
        {
            // Arrange
            // CoreOnlyDto, not MinimalDto: the 16-column floor is only observable while every
            // label is short, and a record an install produces fills the grouped categories, whose
            // indented labels widen the column. The realistic layout is pinned by
            // ExecuteAsync_RecordAnInstallProduces_RendersTheDocumentedSample instead.
            GivenService(CoreOnlyDto());
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            var separators = (result.Message ?? string.Empty)
                .Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => l.IndexOf(": ", StringComparison.Ordinal) >= 0)
                .Select(l => l.IndexOf(": ", StringComparison.Ordinal))
                .Distinct()
                .ToList();
            Assert.Single(separators);
            Assert.Equal(16, separators[0]);
        }

        [Fact]
        public async Task ExecuteAsync_EveryBoolean_UsesOneVocabulary()
        {
            // Arrange
            // Two "feature" flags and two predicates, which an earlier version rendered with two
            // different word pairs on the same screen.
            var dto = MinimalDto();
            dto.StdoutPath = @"C:\logs\stdout.log";
            dto.EnableSizeRotation = true;
            dto.EnableConsoleUI = false;
            dto.UseLocalTimeForRotation = true;
            dto.PreStopLogAsError = false;
            dto.PreStopExecutablePath = @"C:\apps\prestop.exe";
            GivenService(dto);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(CliStrings.Msg_Show_Yes, SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Logs, CliStrings.Msg_Show_Label_SizeRotation));
            Assert.Equal(CliStrings.Msg_Show_Yes, SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Logs, CliStrings.Msg_Show_Label_LocalTimeRotation));
            Assert.Equal(CliStrings.Msg_Show_No, SectionRowValue(result.Message, CliStrings.Msg_Show_Group_Other, CliStrings.Msg_Show_Label_ConsoleUI));
            Assert.Equal(CliStrings.Msg_Show_No, SectionRowValue(result.Message, CliStrings.Msg_Show_Group_PreStop, CliStrings.Msg_Show_Label_LogAsError));
            Assert.DoesNotContain("Enabled", result.Message);
            Assert.DoesNotContain("Disabled", result.Message);
        }

        #endregion

        #region List mode

        [Fact]
        public async Task ExecuteAsync_NoName_ListsEveryServiceWithItsColumns()
        {
            // Arrange
            var first = MinimalDto("alpha");
            first.Pid = 11;
            var second = MinimalDto("beta");
            second.Pid = 22;
            GivenServices(first, second);
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions();

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Contains(CliStrings.Msg_Show_Column_Name, result.Message);
            Assert.Contains(CliStrings.Msg_Show_Column_DisplayName, result.Message);
            Assert.Contains(CliStrings.Msg_Show_Column_Description, result.Message);
            Assert.Contains(CliStrings.Msg_Show_Column_StartupType, result.Message);
            Assert.Contains(CliStrings.Msg_Show_Column_Status, result.Message);
            Assert.Contains(CliStrings.Msg_Show_Column_Pid, result.Message);
            Assert.Contains("alpha", result.Message);
            Assert.Contains("beta", result.Message);
            Assert.Contains("11", result.Message);
            Assert.Contains("22", result.Message);
            Assert.Contains(string.Format(CliStrings.Msg_Show_ServiceCount, 2), result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_NoName_OrdersServicesByNameIgnoringCase()
        {
            // Arrange
            GivenServices(MinimalDto("zulu"), MinimalDto("Alpha"), MinimalDto("mike"));
            GivenStatus(ServiceControllerStatus.Stopped);
            var opts = new ShowServiceOptions();

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            var body = result.Message ?? string.Empty;
            Assert.True(body.IndexOf("Alpha", StringComparison.Ordinal) < body.IndexOf("mike", StringComparison.Ordinal));
            Assert.True(body.IndexOf("mike", StringComparison.Ordinal) < body.IndexOf("zulu", StringComparison.Ordinal));
        }

        [Fact]
        public async Task ExecuteAsync_NoServicesStored_ReturnsTheEmptyListMessage()
        {
            // Arrange
            GivenServices();
            var opts = new ShowServiceOptions();

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(CliStrings.Msg_Show_NoServices, result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_SearchKeyword_IsPassedToTheRepository()
        {
            // Arrange
            GivenServices(MinimalDto("alpha"));
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions { SearchKeyword = "alph" };

            // Act
            await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            _repository.Verify(r => r.SearchAsync("alph", false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_NoSearchKeyword_QueriesWithTheEmptyKeyword()
        {
            // Arrange
            GivenServices(MinimalDto("alpha"));
            GivenStatus(ServiceControllerStatus.Running);
            var opts = new ShowServiceOptions();

            // Act
            await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            // An empty keyword is the repository's "everything" query, so list mode needs no second call.
            _repository.Verify(r => r.SearchAsync(string.Empty, false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_NameCombinedWithSearch_IsRefused()
        {
            // Arrange
            var opts = new ShowServiceOptions { ServiceName = ServiceName, SearchKeyword = "alph" };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(CliStrings.Msg_Show_NameAndSearchNotAllowed, result.Message);
            _repository.Verify(r => r.GetByNameAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            _repository.Verify(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region Error handling

        [Fact]
        public async Task ExecuteAsync_UnauthorizedAccessException_ReturnsAdminPrivilegesRequired()
        {
            // Arrange
            _repository
                .Setup(r => r.GetByNameAsync(ServiceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException());
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(string.Format(CliStrings.Msg_AdminPrivilegesRequired, "show"), result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_OperationCanceled_ReturnsCancelledResult()
        {
            // Arrange
            _repository
                .Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());
            var opts = new ShowServiceOptions();

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(string.Format(CliStrings.Msg_CommandCancelled, "show"), result.Message);
        }

        [Fact]
        public async Task ExecuteAsync_GenericException_ReportsTheActionAndTheSuggestion()
        {
            // Arrange
            _repository
                .Setup(r => r.GetByNameAsync(ServiceName, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("database unreadable"));
            var opts = new ShowServiceOptions { ServiceName = ServiceName };

            // Act
            var result = await _command.ExecuteAsync(opts, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains(string.Format(CliStrings.Msg_ShowServiceAction, ServiceName), result.Message);
            Assert.Contains(CliStrings.Msg_ShowServiceSuggestion, result.Message);
        }

        #endregion

        #region Verb metadata

        [Fact]
        public void ShowVerb_DeclaresAnOptionalNameAndAnOptionalSearch()
        {
            // Arrange
            var verb = typeof(ShowServiceOptions)
                .GetCustomAttributes(typeof(CommandLine.VerbAttribute), false)
                .FirstOrDefault() as CommandLine.VerbAttribute;

            // Act
            var options = typeof(ShowServiceOptions)
                .GetProperties()
                .Select(p => p.GetCustomAttributes(typeof(CommandLine.OptionAttribute), false).FirstOrDefault())
                .OfType<CommandLine.OptionAttribute>()
                .ToList();

            // Assert
            Assert.NotNull(verb);
            Assert.Equal("show", verb!.Name);
            Assert.Contains(options, o => o.LongName == "name" && o.ShortName == "n" && !o.Required);
            Assert.Contains(options, o => o.LongName == "search" && o.ShortName == "s" && !o.Required);
            Assert.Contains(options, o => o.LongName == "decrypt" && o.ShortName == "d" && !o.Required);
        }

        #endregion
    }
}

using Moq;
using Servy.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Models;
using Servy.Resources;
using Servy.Services;
using Servy.UI.Services;
using Servy.ViewModels;
using System.ComponentModel;
using System.Windows.Input;

namespace Servy.UnitTests.ViewModels
{
    public class MainViewModelTests : IDisposable
    {
        private readonly Mock<IFileDialogService> _dialogServiceMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;
        private readonly Mock<IMessageBoxService> _messageBoxService;
        private readonly Mock<IServiceRepository> _serviceRepository;
        private readonly Mock<IHelpService> _helpService;
        private readonly Mock<IAppConfiguration> _appConfigMock;
        private readonly MainViewModel _viewModel;

        public MainViewModelTests()
        {
            _dialogServiceMock = new Mock<IFileDialogService>();
            _serviceCommandsMock = new Mock<IServiceCommands>();
            _messageBoxService = new Mock<IMessageBoxService>();
            _serviceRepository = new Mock<IServiceRepository>();
            _helpService = new Mock<IHelpService>();

            _appConfigMock = new Mock<IAppConfiguration>();
            _appConfigMock.Setup(c => c.IsManagerAppAvailable).Returns(true);

            _viewModel = new MainViewModel(
                _dialogServiceMock.Object,
                _serviceCommandsMock.Object,
                _messageBoxService.Object,
                _serviceRepository.Object,
                _helpService.Object,
                _appConfigMock.Object
            );
        }

        public void Dispose()
        {
            _viewModel.Dispose();
        }

        #region Core Property Tests

        [Fact]
        public void PropertyChanged_Raised_When_ServiceName_Changed()
        {
            // Arrange
            var raised = false;
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.ServiceName))
                    raised = true;
            };

            // Act
            _viewModel.ServiceName = "NewService";

            // Assert
            Assert.True(raised);
        }

        [Fact]
        public void AppConfig_PropertyChanged_Updates_IsManagerAppAvailable_Dynamically()
        {
            // Arrange
            _appConfigMock.Setup(c => c.IsManagerAppAvailable).Returns(false);

            // Act
            _appConfigMock.Raise(c => c.PropertyChanged += null, new PropertyChangedEventArgs(nameof(IAppConfiguration.IsManagerAppAvailable)));

            // Assert
            Assert.False(_viewModel.IsManagerAppAvailable);
        }

        #endregion

        #region Service Action Command Tests

        [Fact]
        public async Task InstallCommand_Calls_InstallService_With_Configuration()
        {
            // Arrange
            _viewModel.ServiceName = "TestService";
            _viewModel.ServiceDisplayName = "TestServiceDisplayName";
            _viewModel.ServiceDescription = "Desc";
            _viewModel.ProcessPath = "C:\\test.exe";
            _viewModel.StartupDirectory = "C:\\";
            _viewModel.ProcessParameters = "--flag";
            _viewModel.StdoutPath = "out.log";
            _viewModel.StderrPath = "err.log";
            _viewModel.EnableSizeRotation = true;
            _viewModel.RotationSize = "12345";
            _viewModel.UseLocalTimeForRotation = true;
            _viewModel.EnableHealthMonitoring = true;
            _viewModel.HeartbeatInterval = "60";
            _viewModel.MaxFailedChecks = "5";
            _viewModel.MaxRestartAttempts = "3";
            _viewModel.SelectedStartupType = ServiceStartType.Manual;
            _viewModel.SelectedProcessPriority = ProcessPriority.High;
            _viewModel.SelectedRecoveryAction = RecoveryAction.RestartService;
            _viewModel.EnvironmentVariables = "var1=val1;var2=val2";
            _viewModel.ServiceDependencies = "MongoDB";
            _viewModel.RunAsLocalSystem = false;
            _viewModel.UserAccount = @".\username";
            _viewModel.Password = "password";
            _viewModel.ConfirmPassword = "password";

            _viewModel.PreLaunchExecutablePath = @"C:\pre-launch.exe";
            _viewModel.PreLaunchStartupDirectory = @"C:\";
            _viewModel.PreLaunchParameters = "--param1 val1";
            _viewModel.PreLaunchEnvironmentVariables = "var1=val1; var2=val2;";
            _viewModel.PreLaunchStdoutPath = @"C:\pre-launch-stdout.log";
            _viewModel.PreLaunchStderrPath = @"C:\pre-launch-stderr.log";
            _viewModel.PreLaunchTimeoutSeconds = "40";
            _viewModel.PreLaunchRetryAttempts = "3";
            _viewModel.PreLaunchIgnoreFailure = true;

            _viewModel.FailureProgramPath = @"C:\failureProgram.exe";
            _viewModel.FailureProgramStartupDirectory = @"C:\failureProgramDir";
            _viewModel.FailureProgramParameters = "--failureProgramParam1 val1";

            _viewModel.PostLaunchExecutablePath = @"C:\post-launch.exe";
            _viewModel.PostLaunchStartupDirectory = @"C:\";
            _viewModel.PostLaunchParameters = "--param1 val1";
            _viewModel.MaxRotations = "5";
            _viewModel.EnableDateRotation = true;
            _viewModel.SelectedDateRotationType = DateRotationType.Weekly;

            _viewModel.StartTimeout = "11";
            _viewModel.StopTimeout = "6";

            _viewModel.PreStopExecutablePath = @"C:\pre-stop\pre-stop.exe";
            _viewModel.PreStopStartupDirectory = @"C:\pre-stop";
            _viewModel.PreStopParameters = "pre-stop-args";
            _viewModel.PreStopTimeoutSeconds = "15";
            _viewModel.PreStopLogAsError = true;

            _viewModel.PostStopExecutablePath = @"C:\post-stop\post-stop.exe";
            _viewModel.PostStopStartupDirectory = @"C:\post-stop";
            _viewModel.PostStopParameters = "post-stop-args";

            // Act
            await _viewModel.InstallCommand.ExecuteAsync(null);

            // Assert
            _serviceCommandsMock.Verify(s => s.InstallService(
                It.Is<ServiceConfiguration>(c =>
                    c.Name == _viewModel.ServiceName &&
                    c.DisplayName == _viewModel.ServiceDisplayName &&
                    c.Description == _viewModel.ServiceDescription &&
                    c.ExecutablePath == _viewModel.ProcessPath &&
                    c.StartupDirectory == _viewModel.StartupDirectory &&
                    c.Parameters == _viewModel.ProcessParameters &&
                    c.StartupType == _viewModel.SelectedStartupType &&
                    c.Priority == _viewModel.SelectedProcessPriority &&
                    c.StdoutPath == _viewModel.StdoutPath &&
                    c.StderrPath == _viewModel.StderrPath &&
                    c.EnableSizeRotation == _viewModel.EnableSizeRotation &&
                    c.RotationSize == _viewModel.RotationSize &&
                    c.EnableDateRotation == _viewModel.EnableDateRotation &&
                    c.DateRotationType == _viewModel.SelectedDateRotationType &&
                    c.MaxRotations == _viewModel.MaxRotations &&
                    c.UseLocalTimeForRotation == _viewModel.UseLocalTimeForRotation &&
                    c.EnableHealthMonitoring == _viewModel.EnableHealthMonitoring &&
                    c.HeartbeatInterval == _viewModel.HeartbeatInterval &&
                    c.MaxFailedChecks == _viewModel.MaxFailedChecks &&
                    c.RecoveryAction == _viewModel.SelectedRecoveryAction &&
                    c.MaxRestartAttempts == _viewModel.MaxRestartAttempts &&
                    c.FailureProgramPath == _viewModel.FailureProgramPath &&
                    c.FailureProgramStartupDirectory == _viewModel.FailureProgramStartupDirectory &&
                    c.FailureProgramParameters == _viewModel.FailureProgramParameters &&
                    c.EnvironmentVariables == _viewModel.EnvironmentVariables &&
                    c.ServiceDependencies == _viewModel.ServiceDependencies &&
                    c.RunAsLocalSystem == _viewModel.RunAsLocalSystem &&
                    c.UserAccount == _viewModel.UserAccount &&
                    c.Password == _viewModel.Password &&
                    c.ConfirmPassword == _viewModel.ConfirmPassword &&
                    c.PreLaunchExecutablePath == _viewModel.PreLaunchExecutablePath &&
                    c.PreLaunchStartupDirectory == _viewModel.PreLaunchStartupDirectory &&
                    c.PreLaunchParameters == _viewModel.PreLaunchParameters &&
                    c.PreLaunchEnvironmentVariables == _viewModel.PreLaunchEnvironmentVariables &&
                    c.PreLaunchStdoutPath == _viewModel.PreLaunchStdoutPath &&
                    c.PreLaunchStderrPath == _viewModel.PreLaunchStderrPath &&
                    c.PreLaunchTimeoutSeconds == _viewModel.PreLaunchTimeoutSeconds &&
                    c.PreLaunchRetryAttempts == _viewModel.PreLaunchRetryAttempts &&
                    c.PreLaunchIgnoreFailure == _viewModel.PreLaunchIgnoreFailure &&
                    c.PostLaunchExecutablePath == _viewModel.PostLaunchExecutablePath &&
                    c.PostLaunchStartupDirectory == _viewModel.PostLaunchStartupDirectory &&
                    c.PostLaunchParameters == _viewModel.PostLaunchParameters &&
                    c.StartTimeout == _viewModel.StartTimeout &&
                    c.StopTimeout == _viewModel.StopTimeout &&
                    c.PreStopExecutablePath == _viewModel.PreStopExecutablePath &&
                    c.PreStopStartupDirectory == _viewModel.PreStopStartupDirectory &&
                    c.PreStopParameters == _viewModel.PreStopParameters &&
                    c.PreStopTimeoutSeconds == _viewModel.PreStopTimeoutSeconds &&
                    c.PreStopLogAsError == _viewModel.PreStopLogAsError &&
                    c.PostStopExecutablePath == _viewModel.PostStopExecutablePath &&
                    c.PostStopStartupDirectory == _viewModel.PostStopStartupDirectory &&
                    c.PostStopParameters == _viewModel.PostStopParameters
                ), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StartCommand_Calls_StartService()
        {
            _viewModel.ServiceName = "MyService";
            await _viewModel.StartCommand.ExecuteAsync(null);
            _serviceCommandsMock.Verify(s => s.StartService("MyService", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StopCommand_Calls_StopService()
        {
            _viewModel.ServiceName = "MyService";
            await _viewModel.StopCommand.ExecuteAsync(null);
            _serviceCommandsMock.Verify(s => s.StopService("MyService", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RestartCommand_Calls_RestartService()
        {
            _viewModel.ServiceName = "MyService";
            await _viewModel.RestartCommand.ExecuteAsync(null);
            _serviceCommandsMock.Verify(s => s.RestartService("MyService", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UninstallCommand_Calls_UninstallService()
        {
            _viewModel.ServiceName = "MyService";
            await _viewModel.UninstallCommand.ExecuteAsync(null);
            _serviceCommandsMock.Verify(s => s.UninstallService("MyService", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ManagerCommand_Calls_OpenManager()
        {
            await _viewModel.ManagerCommand.ExecuteAsync(null);
            _serviceCommandsMock.Verify(s => s.OpenManager(), Times.Once);
        }

        #endregion

        #region Browse Method Tests

        [Theory]
        [InlineData(nameof(MainViewModel.BrowseProcessPathCommand), nameof(MainViewModel.ProcessPath), "C:\\App\\proc.exe", false)]
        [InlineData(nameof(MainViewModel.BrowseStartupDirectoryCommand), nameof(MainViewModel.StartupDirectory), "C:\\AppDir", true)]
        [InlineData(nameof(MainViewModel.BrowseFailureProgramPathCommand), nameof(MainViewModel.FailureProgramPath), "C:\\App\\fail.exe", false)]
        [InlineData(nameof(MainViewModel.BrowseFailureProgramStartupDirectoryCommand), nameof(MainViewModel.FailureProgramStartupDirectory), "C:\\FailDir", true)]
        [InlineData(nameof(MainViewModel.BrowsePreLaunchProcessPathCommand), nameof(MainViewModel.PreLaunchExecutablePath), "C:\\App\\pre.exe", false)]
        [InlineData(nameof(MainViewModel.BrowsePreLaunchStartupDirectoryCommand), nameof(MainViewModel.PreLaunchStartupDirectory), "C:\\PreDir", true)]
        [InlineData(nameof(MainViewModel.BrowsePostLaunchProcessPathCommand), nameof(MainViewModel.PostLaunchExecutablePath), "C:\\App\\post.exe", false)]
        [InlineData(nameof(MainViewModel.BrowsePostLaunchStartupDirectoryCommand), nameof(MainViewModel.PostLaunchStartupDirectory), "C:\\PostDir", true)]
        [InlineData(nameof(MainViewModel.BrowsePreStopProcessPathCommand), nameof(MainViewModel.PreStopExecutablePath), "C:\\App\\prestop.exe", false)]
        [InlineData(nameof(MainViewModel.BrowsePreStopStartupDirectoryCommand), nameof(MainViewModel.PreStopStartupDirectory), "C:\\PreStopDir", true)]
        [InlineData(nameof(MainViewModel.BrowsePostStopProcessPathCommand), nameof(MainViewModel.PostStopExecutablePath), "C:\\App\\poststop.exe", false)]
        [InlineData(nameof(MainViewModel.BrowsePostStopStartupDirectoryCommand), nameof(MainViewModel.PostStopStartupDirectory), "C:\\PostStopDir", true)]
        public void BrowseExecutableAndFolderCommands_Set_CorrectPaths_When_Selected(string commandName, string propertyName, string samplePath, bool isFolder)
        {
            // Arrange
            if (isFolder)
                _dialogServiceMock.Setup(d => d.OpenFolder()).Returns(samplePath);
            else
                _dialogServiceMock.Setup(d => d.OpenExecutable()).Returns(samplePath);

            var command = (ICommand)typeof(MainViewModel).GetProperty(commandName)!.GetValue(_viewModel)!;
            var property = typeof(MainViewModel).GetProperty(propertyName)!;

            // Act
            command.Execute(null);

            // Assert
            Assert.Equal(samplePath, property.GetValue(_viewModel));
        }

        [Theory]
        [InlineData(nameof(MainViewModel.BrowseStdoutPathCommand), nameof(MainViewModel.StdoutPath))]
        [InlineData(nameof(MainViewModel.BrowseStderrPathCommand), nameof(MainViewModel.StderrPath))]
        [InlineData(nameof(MainViewModel.BrowsePreLaunchStdoutPathCommand), nameof(MainViewModel.PreLaunchStdoutPath))]
        [InlineData(nameof(MainViewModel.BrowsePreLaunchStderrPathCommand), nameof(MainViewModel.PreLaunchStderrPath))]
        public void BrowseSaveFileCommands_Set_CorrectPaths_When_Selected(string commandName, string propertyName)
        {
            // Arrange
            var samplePath = $"C:\\Logs\\{propertyName}.log";
            _dialogServiceMock.Setup(d => d.SaveFile(It.IsAny<string>())).Returns(samplePath);

            var command = (ICommand)typeof(MainViewModel).GetProperty(commandName)!.GetValue(_viewModel)!;
            var property = typeof(MainViewModel).GetProperty(propertyName)!;

            // Act
            command.Execute(null);

            // Assert
            Assert.Equal(samplePath, property.GetValue(_viewModel));
        }

        [Fact]
        public void BrowseAndAssign_DoesNotOverwrite_ExistingPath_When_UserCancelsDialog()
        {
            // Arrange
            _viewModel.ProcessPath = "C:\\Existing\\App.exe";
            _dialogServiceMock.Setup(d => d.OpenExecutable()).Returns((string?)null);

            // Act
            _viewModel.BrowseProcessPathCommand.Execute(null);

            // Assert
            Assert.Equal("C:\\Existing\\App.exe", _viewModel.ProcessPath);
        }

        #endregion

        #region Form State Management & Clear Form Tests

        [Fact]
        public async Task ClearCommand_Resets_All_Fields_WhenUserConfirms()
        {
            // Arrange
            _viewModel.ServiceName = "TestService";
            _viewModel.ProcessPath = "test.exe";
            _viewModel.EnableSizeRotation = true;
            _viewModel.RotationSize = "555";

            _messageBoxService.Setup(ds => ds.ShowConfirmAsync(Strings.Confirm_ClearAll, UiAppConfig.Caption)).ReturnsAsync(true);

            // Act
            await _viewModel.ClearFormCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal(string.Empty, _viewModel.ServiceName);
            Assert.Equal(string.Empty, _viewModel.ProcessPath);
            Assert.False(_viewModel.EnableSizeRotation);
            Assert.Equal(Core.Config.AppConfig.DefaultRotationSizeMB.ToString(), _viewModel.RotationSize);
        }

        [Fact]
        public async Task ClearCommand_DoesNotReset_WhenUserCancels()
        {
            // Arrange
            _viewModel.ServiceName = "TestService";
            _viewModel.ProcessPath = "test.exe";
            _viewModel.EnableSizeRotation = true;
            _viewModel.RotationSize = "555";

            _messageBoxService.Setup(ds => ds.ShowConfirmAsync(Strings.Confirm_ClearAll, UiAppConfig.Caption)).ReturnsAsync(false);

            // Act
            await _viewModel.ClearFormCommand.ExecuteAsync(null);

            // Assert values remain unchanged
            Assert.Equal("TestService", _viewModel.ServiceName);
            Assert.Equal("test.exe", _viewModel.ProcessPath);
            Assert.True(_viewModel.EnableSizeRotation);
            Assert.Equal("555", _viewModel.RotationSize);
        }

        #endregion

        #region Import/Export Configuration Tests

        [Fact]
        public async Task ExportXmlCommand_Calls_ExportXmlConfig_With_ConfirmPassword()
        {
            _viewModel.ConfirmPassword = "SecretPassword123";
            await _viewModel.ExportXmlCommand.ExecuteAsync(null);
            _serviceCommandsMock.Verify(m => m.ExportXmlConfig("SecretPassword123"), Times.Once);
        }

        [Fact]
        public async Task ExportJsonCommand_Calls_ExportJsonConfig_With_ConfirmPassword()
        {
            _viewModel.ConfirmPassword = "SecretPassword123";
            await _viewModel.ExportJsonCommand.ExecuteAsync(null);
            _serviceCommandsMock.Verify(m => m.ExportJsonConfig("SecretPassword123"), Times.Once);
        }

        [Fact]
        public async Task ImportXmlCommand_Calls_ImportXmlConfig()
        {
            await _viewModel.ImportXmlCommand.ExecuteAsync(null);
            _serviceCommandsMock.Verify(m => m.ImportXmlConfig(), Times.Once);
        }

        [Fact]
        public async Task ImportJsonCommand_Calls_ImportJsonConfig()
        {
            await _viewModel.ImportJsonCommand.ExecuteAsync(null);
            _serviceCommandsMock.Verify(m => m.ImportJsonConfig(), Times.Once);
        }

        #endregion

        #region Help / Documentation / Updates / About Dialog Tests

        [Fact]
        public async Task OpenDocumentation_Calls_HelpService_With_Caption()
        {
            await _viewModel.OpenDocumentationCommand.ExecuteAsync(null);
            _helpService.Verify(h => h.OpenDocumentationAsync(UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task CheckUpdatesAsync_Calls_HelpService_With_Caption()
        {
            await _viewModel.CheckUpdatesCommand.ExecuteAsync(null);
            _helpService.Verify(h => h.CheckUpdatesAsync(UiAppConfig.Caption), Times.Once);
        }

        [Fact]
        public async Task OpenAboutDialog_Calls_HelpService_With_FormattedText_And_Caption()
        {
            // Act
            await _viewModel.OpenAboutDialogCommand.ExecuteAsync(null);

            // Assert
            _helpService.Verify(h => h.OpenAboutDialogAsync(
                It.Is<string>(text =>
                    text.Contains(Core.Config.AppConfig.Version) &&
                    text.Contains(DateTime.Now.Year.ToString())
                ),
                UiAppConfig.Caption), Times.Once);
        }

        #endregion

        #region Service Configuration Repository & Lifecycles Tests

        [Fact]
        public async Task LoadServiceConfiguration_ValidName_Binds_TargetConfigurationDtoToModel()
        {
            // Arrange
            var sampleDto = new ServiceDto
            {
                Name = "PolledService",
                DisplayName = "Polled Service Display",
                ExecutablePath = "C:\\Polled\\Service.exe",
                StartupType = (int)ServiceStartType.AutomaticDelayedStart,
                Priority = (int)ProcessPriority.BelowNormal
            };

            _serviceRepository.Setup(r => r.GetByNameAsync("PolledService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(sampleDto);

            // Act
            await _viewModel.LoadServiceConfiguration("PolledService");

            // Assert
            Assert.Equal("PolledService", _viewModel.ServiceName);
            Assert.Equal("Polled Service Display", _viewModel.ServiceDisplayName);
            Assert.Equal("C:\\Polled\\Service.exe", _viewModel.ProcessPath);
            Assert.Equal(ServiceStartType.AutomaticDelayedStart, _viewModel.SelectedStartupType);
            Assert.Equal(ProcessPriority.BelowNormal, _viewModel.SelectedProcessPriority);
        }

        [Fact]
        public async Task LoadServiceConfiguration_RepositoryNull_ExitsEarlyWithoutMutation()
        {
            // Arrange
            _viewModel.ServiceName = "KeepThisName";
            _serviceRepository.Setup(r => r.GetByNameAsync("MissingService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync((ServiceDto?)null);

            // Act
            await _viewModel.LoadServiceConfiguration("MissingService");

            // Assert
            Assert.Equal("KeepThisName", _viewModel.ServiceName);
            _messageBoxService.Verify(m => m.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task LoadServiceConfiguration_ExceptionBranch_LogsAndDisplaysErrorMessageBox()
        {
            // Arrange
            _serviceRepository.Setup(r => r.GetByNameAsync("ErrorService", It.IsAny<bool>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("DB Corrupt"));

            // Act
            await _viewModel.LoadServiceConfiguration("ErrorService");

            // Assert
            _messageBoxService.Verify(m => m.ShowErrorAsync(Strings.Msg_UnexpectedError, UiAppConfig.Caption), Times.Once);
        }

        #endregion

        #region DTO Binding and Conversion Mapping Tests

        [Fact]
        public void BindServiceDtoToModel_Populates_All_Properties_Correctly()
        {
            // Arrange
            var dto = new ServiceDto
            {
                Name = "DtoService",
                DisplayName = "Dto Display",
                Description = "Dto Desc",
                ExecutablePath = "C:\\proc.exe",
                StartupDirectory = "C:\\Start",
                Parameters = "--args",
                StartupType = (int)ServiceStartType.Disabled,
                Priority = (int)ProcessPriority.RealTime,
                EnableConsoleUI = true,
                StdoutPath = "out.log",
                StderrPath = "err.log",
                EnableSizeRotation = true,
                RotationSize = 50,
                EnableDateRotation = true,
                DateRotationType = (int)DateRotationType.Monthly,
                MaxRotations = 10,
                UseLocalTimeForRotation = true,
                EnableHealthMonitoring = true,
                HeartbeatInterval = 30,
                MaxFailedChecks = 3,
                RecoveryAction = (int)RecoveryAction.RestartProcess,
                RecoveryOnCleanExit = true,
                MaxRestartAttempts = 5,
                FailureProgramPath = "fail.exe",
                FailureProgramStartupDirectory = "fail_dir",
                FailureProgramParameters = "--fail-args",
                EnvironmentVariables = "A=1\nB=2",
                ServiceDependencies = "ServiceA\nServiceB",
                RunAsLocalSystem = false,
                UserAccount = "Admin",
                Password = "ProtectedPassword",
                PreLaunchExecutablePath = "pre.exe",
                PreLaunchStartupDirectory = "pre_dir",
                PreLaunchParameters = "--pre-args",
                PreLaunchEnvironmentVariables = "X=9",
                PreLaunchStdoutPath = "pre_out.log",
                PreLaunchStderrPath = "pre_err.log",
                PreLaunchTimeoutSeconds = 15,
                PreLaunchRetryAttempts = 2,
                PreLaunchIgnoreFailure = true,
                PostLaunchExecutablePath = "post.exe",
                PostLaunchStartupDirectory = "post_dir",
                PostLaunchParameters = "--post-args",
                EnableDebugLogs = true,
                StartTimeout = 45,
                StopTimeout = 25,
                PreStopExecutablePath = "pre_stop.exe",
                PreStopStartupDirectory = "pre_stop_dir",
                PreStopParameters = "--pre-stop-args",
                PreStopTimeoutSeconds = 20,
                PreStopLogAsError = true,
                PostStopExecutablePath = "post_stop.exe",
                PostStopStartupDirectory = "post_stop_dir",
                PostStopParameters = "--post-stop-args"
            };

            // Act
            _viewModel.BindServiceDtoToModel(dto);

            // Assert
            Assert.Equal(dto.Name, _viewModel.ServiceName);
            Assert.Equal(dto.DisplayName, _viewModel.ServiceDisplayName);
            Assert.Equal(dto.Description, _viewModel.ServiceDescription);
            Assert.Equal(dto.ExecutablePath, _viewModel.ProcessPath);
            Assert.Equal(dto.StartupDirectory, _viewModel.StartupDirectory);
            Assert.Equal(dto.Parameters, _viewModel.ProcessParameters);
            Assert.Equal(ServiceStartType.Disabled, _viewModel.SelectedStartupType);
            Assert.Equal(ProcessPriority.RealTime, _viewModel.SelectedProcessPriority);
            Assert.True(_viewModel.EnableConsoleUI);
            Assert.Equal(dto.StdoutPath, _viewModel.StdoutPath);
            Assert.Equal(dto.StderrPath, _viewModel.StderrPath);
            Assert.True(_viewModel.EnableSizeRotation);
            Assert.Equal("50", _viewModel.RotationSize);
            Assert.True(_viewModel.EnableDateRotation);
            Assert.Equal(DateRotationType.Monthly, _viewModel.SelectedDateRotationType);
            Assert.Equal("10", _viewModel.MaxRotations);
            Assert.True(_viewModel.UseLocalTimeForRotation);
            Assert.True(_viewModel.EnableHealthMonitoring);
            Assert.Equal("30", _viewModel.HeartbeatInterval);
            Assert.Equal("3", _viewModel.MaxFailedChecks);
            Assert.Equal(RecoveryAction.RestartProcess, _viewModel.SelectedRecoveryAction);
            Assert.True(_viewModel.RecoveryOnCleanExit);
            Assert.Equal("5", _viewModel.MaxRestartAttempts);
            Assert.Equal(dto.FailureProgramPath, _viewModel.FailureProgramPath);
            Assert.Equal(dto.FailureProgramStartupDirectory, _viewModel.FailureProgramStartupDirectory);
            Assert.Equal(dto.FailureProgramParameters, _viewModel.FailureProgramParameters);
            Assert.Equal(dto.UserAccount, _viewModel.UserAccount);
            Assert.Equal(dto.Password, _viewModel.Password);
            Assert.Equal(string.Empty, _viewModel.ConfirmPassword); // Purposely wiped for safety checks
            Assert.Equal(dto.PreLaunchExecutablePath, _viewModel.PreLaunchExecutablePath);
            Assert.Equal(dto.PreLaunchStartupDirectory, _viewModel.PreLaunchStartupDirectory);
            Assert.Equal(dto.PreLaunchParameters, _viewModel.PreLaunchParameters);
            Assert.Equal(dto.PreLaunchStdoutPath, _viewModel.PreLaunchStdoutPath);
            Assert.Equal(dto.PreLaunchStderrPath, _viewModel.PreLaunchStderrPath);
            Assert.Equal("15", _viewModel.PreLaunchTimeoutSeconds);
            Assert.Equal("2", _viewModel.PreLaunchRetryAttempts);
            Assert.True(_viewModel.PreLaunchIgnoreFailure);
            Assert.Equal(dto.PostLaunchExecutablePath, _viewModel.PostLaunchExecutablePath);
            Assert.Equal(dto.PostLaunchStartupDirectory, _viewModel.PostLaunchStartupDirectory);
            Assert.Equal(dto.PostLaunchParameters, _viewModel.PostLaunchParameters);
            Assert.True(_viewModel.EnableDebugLogs);
            Assert.Equal("45", _viewModel.StartTimeout);
            Assert.Equal("25", _viewModel.StopTimeout);
            Assert.Equal(dto.PreStopExecutablePath, _viewModel.PreStopExecutablePath);
            Assert.Equal(dto.PreStopStartupDirectory, _viewModel.PreStopStartupDirectory);
            Assert.Equal(dto.PreStopParameters, _viewModel.PreStopParameters);
            Assert.Equal("20", _viewModel.PreStopTimeoutSeconds);
            Assert.True(_viewModel.PreStopLogAsError);
            Assert.Equal(dto.PostStopExecutablePath, _viewModel.PostStopExecutablePath);
            Assert.Equal(dto.PostStopStartupDirectory, _viewModel.PostStopStartupDirectory);
            Assert.Equal(dto.PostStopParameters, _viewModel.PostStopParameters);
        }

        [Fact]
        public void ModelToServiceDto_Converts_CurrentState_To_Dto_Correctly()
        {
            // Arrange
            _viewModel.ServiceName = "ModelService";
            _viewModel.ServiceDisplayName = "Model Display";
            _viewModel.ServiceDescription = "Model Desc";
            _viewModel.ProcessPath = "C:\\proc.exe";
            _viewModel.StartupDirectory = "C:\\Dir";
            _viewModel.ProcessParameters = "--run";
            _viewModel.SelectedStartupType = ServiceStartType.Automatic;
            _viewModel.SelectedProcessPriority = ProcessPriority.Normal;
            _viewModel.EnableConsoleUI = false;
            _viewModel.StdoutPath = "stdout.txt";
            _viewModel.StderrPath = "stderr.txt";
            _viewModel.EnableSizeRotation = false;
            _viewModel.RotationSize = "25";
            _viewModel.EnableDateRotation = false;
            _viewModel.SelectedDateRotationType = DateRotationType.Daily;
            _viewModel.MaxRotations = "5";
            _viewModel.UseLocalTimeForRotation = false;
            _viewModel.EnableHealthMonitoring = false;
            _viewModel.HeartbeatInterval = "10";
            _viewModel.MaxFailedChecks = "2";
            _viewModel.SelectedRecoveryAction = RecoveryAction.None;
            _viewModel.RecoveryOnCleanExit = false;
            _viewModel.MaxRestartAttempts = "0";
            _viewModel.FailureProgramPath = "kill.exe";
            _viewModel.FailureProgramStartupDirectory = "kill_dir";
            _viewModel.FailureProgramParameters = "--now";
            _viewModel.EnvironmentVariables = "Env=True";
            _viewModel.ServiceDependencies = "Deps";
            _viewModel.RunAsLocalSystem = true;
            _viewModel.UserAccount = "LocalSystem";
            _viewModel.Password = "Pass";
            _viewModel.PreLaunchExecutablePath = "p.exe";
            _viewModel.PreLaunchStartupDirectory = "p_dir";
            _viewModel.PreLaunchParameters = "-p";
            _viewModel.PreLaunchEnvironmentVariables = "V=1";
            _viewModel.PreLaunchStdoutPath = "p.log";
            _viewModel.PreLaunchStderrPath = "p_err.log";
            _viewModel.PreLaunchTimeoutSeconds = "5";
            _viewModel.PreLaunchRetryAttempts = "1";
            _viewModel.PreLaunchIgnoreFailure = false;
            _viewModel.PostLaunchExecutablePath = "po.exe";
            _viewModel.PostLaunchStartupDirectory = "po_dir";
            _viewModel.PostLaunchParameters = "-po";
            _viewModel.EnableDebugLogs = false;
            _viewModel.StartTimeout = "30";
            _viewModel.StopTimeout = "20";
            _viewModel.PreStopExecutablePath = "ps.exe";
            _viewModel.PreStopStartupDirectory = "ps_dir";
            _viewModel.PreStopParameters = "-ps";
            _viewModel.PreStopTimeoutSeconds = "10";
            _viewModel.PreStopLogAsError = false;
            _viewModel.PostStopExecutablePath = "pst.exe";
            _viewModel.PostStopStartupDirectory = "pst_dir";
            _viewModel.PostStopParameters = "-pst";

            // Act
            var dto = _viewModel.ModelToServiceDto();

            // Assert
            Assert.Equal(_viewModel.ServiceName, dto.Name);
            Assert.Equal(_viewModel.ServiceDisplayName, dto.DisplayName);
            Assert.Equal(_viewModel.ServiceDescription, dto.Description);
            Assert.Equal(_viewModel.ProcessPath, dto.ExecutablePath);
            Assert.Equal(_viewModel.StartupDirectory, dto.StartupDirectory);
            Assert.Equal(_viewModel.ProcessParameters, dto.Parameters);
            Assert.Equal((int)ServiceStartType.Automatic, dto.StartupType);
            Assert.Equal((int)ProcessPriority.Normal, dto.Priority);
            Assert.False(dto.EnableConsoleUI);
            Assert.Equal(_viewModel.StdoutPath, dto.StdoutPath);
            Assert.Equal(_viewModel.StderrPath, dto.StderrPath);
            Assert.False(dto.EnableSizeRotation);
            Assert.Equal(25, dto.RotationSize);
            Assert.False(dto.EnableDateRotation);
            Assert.Equal((int)DateRotationType.Daily, dto.DateRotationType);
            Assert.Equal(5, dto.MaxRotations);
            Assert.False(dto.UseLocalTimeForRotation);
            Assert.False(dto.EnableHealthMonitoring);
            Assert.Equal(10, dto.HeartbeatInterval);
            Assert.Equal(2, dto.MaxFailedChecks);
            Assert.Equal((int)RecoveryAction.None, dto.RecoveryAction);
            Assert.False(dto.RecoveryOnCleanExit);
            Assert.Equal(0, dto.MaxRestartAttempts);
            Assert.Equal(_viewModel.FailureProgramPath, dto.FailureProgramPath);
            Assert.Equal(_viewModel.FailureProgramStartupDirectory, dto.FailureProgramStartupDirectory);
            Assert.Equal(_viewModel.FailureProgramParameters, dto.FailureProgramParameters);
            Assert.True(dto.RunAsLocalSystem);
            Assert.Equal(_viewModel.UserAccount, dto.UserAccount);
            Assert.Equal(_viewModel.Password, dto.Password);
            Assert.Equal(5, dto.PreLaunchTimeoutSeconds);
            Assert.Equal(1, dto.PreLaunchRetryAttempts);
            Assert.False(dto.PreLaunchIgnoreFailure);
            Assert.False(dto.EnableDebugLogs);
            Assert.Equal(30, dto.StartTimeout);
            Assert.Equal(20, dto.StopTimeout);
            Assert.Equal(10, dto.PreStopTimeoutSeconds);
            Assert.False(dto.PreStopLogAsError);
        }

        #endregion

        #region Cleanup / Unhook Event Handler Demolition Tests

        [Fact]
        public void Dispose_Unhooks_AppConfig_PropertyChanged_EventHandler_To_Prevent_Leaks()
        {
            // Arrange
            _appConfigMock.Setup(c => c.IsManagerAppAvailable).Returns(false);

            // Act - Dispose the ViewModel to unhook event bindings
            _viewModel.Dispose();

            // Raise the PropertyChanged event post-dispose
            _appConfigMock.Raise(c => c.PropertyChanged += null, new PropertyChangedEventArgs(nameof(IAppConfiguration.IsManagerAppAvailable)));

            // Assert - The property shouldn't be updated anymore since the handler is safely detached
            Assert.True(_viewModel.IsManagerAppAvailable);
        }

        #endregion
    }
}
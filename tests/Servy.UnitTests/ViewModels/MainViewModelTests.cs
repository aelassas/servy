using Moq;
using Servy.Config;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Models;
using Servy.Services;
using Servy.UI.Services;
using Servy.ViewModels;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using AppConfig = Servy.Core.Config.AppConfig;

namespace Servy.UnitTests.ViewModels
{
    public class MainViewModelTests
    {
        private readonly Mock<IFileDialogService> _dialogServiceMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;
        private readonly Mock<IMessageBoxService> _messageBoxService;
        private readonly Mock<IServiceRepository> _serviceRepository;
        private readonly Mock<IHelpService> _helpService;
        private readonly Mock<IAppConfiguration> _appConfigMock; // Added new dependency
        private readonly MainViewModel _viewModel;

        public MainViewModelTests()
        {
            // Application.Current hack removed! The ViewModel is now fully decoupled.

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
                _appConfigMock.Object // Injected configuration
            );
        }

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
                    // 1. Core Metadata
                    c.Name == _viewModel.ServiceName &&
                    c.DisplayName == _viewModel.ServiceDisplayName &&
                    c.Description == _viewModel.ServiceDescription &&
                    c.ExecutablePath == _viewModel.ProcessPath &&
                    c.StartupDirectory == _viewModel.StartupDirectory &&
                    c.Parameters == _viewModel.ProcessParameters &&
                    c.StartupType == _viewModel.SelectedStartupType &&
                    c.Priority == _viewModel.SelectedProcessPriority &&

                    // 2. Logging & Rotation
                    c.StdoutPath == _viewModel.StdoutPath &&
                    c.StderrPath == _viewModel.StderrPath &&
                    c.EnableSizeRotation == _viewModel.EnableSizeRotation &&
                    c.RotationSize == _viewModel.RotationSize &&
                    c.EnableDateRotation == _viewModel.EnableDateRotation &&
                    c.DateRotationType == _viewModel.SelectedDateRotationType &&
                    c.MaxRotations == _viewModel.MaxRotations &&
                    c.UseLocalTimeForRotation == _viewModel.UseLocalTimeForRotation &&

                    // 3. Health & Recovery
                    c.EnableHealthMonitoring == _viewModel.EnableHealthMonitoring &&
                    c.HeartbeatInterval == _viewModel.HeartbeatInterval &&
                    c.MaxFailedChecks == _viewModel.MaxFailedChecks &&
                    c.RecoveryAction == _viewModel.SelectedRecoveryAction &&
                    c.MaxRestartAttempts == _viewModel.MaxRestartAttempts &&
                    c.FailureProgramPath == _viewModel.FailureProgramPath &&
                    c.FailureProgramStartupDirectory == _viewModel.FailureProgramStartupDirectory &&
                    c.FailureProgramParameters == _viewModel.FailureProgramParameters &&

                    // 4. Identity & Security
                    c.EnvironmentVariables == _viewModel.EnvironmentVariables &&
                    c.ServiceDependencies == _viewModel.ServiceDependencies &&
                    c.RunAsLocalSystem == _viewModel.RunAsLocalSystem &&
                    c.UserAccount == _viewModel.UserAccount &&
                    c.Password == _viewModel.Password &&
                    c.ConfirmPassword == _viewModel.ConfirmPassword &&

                    // 5. Pre-Launch Hooks
                    c.PreLaunchExecutablePath == _viewModel.PreLaunchExecutablePath &&
                    c.PreLaunchStartupDirectory == _viewModel.PreLaunchStartupDirectory &&
                    c.PreLaunchParameters == _viewModel.PreLaunchParameters &&
                    c.PreLaunchEnvironmentVariables == _viewModel.PreLaunchEnvironmentVariables &&
                    c.PreLaunchStdoutPath == _viewModel.PreLaunchStdoutPath &&
                    c.PreLaunchStderrPath == _viewModel.PreLaunchStderrPath &&
                    c.PreLaunchTimeoutSeconds == _viewModel.PreLaunchTimeoutSeconds &&
                    c.PreLaunchRetryAttempts == _viewModel.PreLaunchRetryAttempts &&
                    c.PreLaunchIgnoreFailure == _viewModel.PreLaunchIgnoreFailure &&

                    // 6. Post-Launch & Timing
                    c.PostLaunchExecutablePath == _viewModel.PostLaunchExecutablePath &&
                    c.PostLaunchStartupDirectory == _viewModel.PostLaunchStartupDirectory &&
                    c.PostLaunchParameters == _viewModel.PostLaunchParameters &&
                    c.StartTimeout == _viewModel.StartTimeout &&
                    c.StopTimeout == _viewModel.StopTimeout &&

                    // 7. Pre-Stop Hooks
                    c.PreStopExecutablePath == _viewModel.PreStopExecutablePath &&
                    c.PreStopStartupDirectory == _viewModel.PreStopStartupDirectory &&
                    c.PreStopParameters == _viewModel.PreStopParameters &&
                    c.PreStopTimeoutSeconds == _viewModel.PreStopTimeoutSeconds &&
                    c.PreStopLogAsError == _viewModel.PreStopLogAsError &&

                    // 8. Post-Stop Hooks
                    c.PostStopExecutablePath == _viewModel.PostStopExecutablePath &&
                    c.PostStopStartupDirectory == _viewModel.PostStopStartupDirectory &&
                    c.PostStopParameters == _viewModel.PostStopParameters
                ), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ClearCommand_Resets_All_Fields_WhenUserConfirms()
        {
            // Arrange
            _viewModel.ServiceName = "TestService";
            _viewModel.ProcessPath = "test.exe";
            _viewModel.EnableSizeRotation = true;
            _viewModel.RotationSize = "555";

            // Mock dialog service to always confirm
            _messageBoxService.Setup(ds => ds.ShowConfirmAsync(
              It.IsAny<string>(), It.IsAny<string>()))
              .ReturnsAsync(true);

            // Act
            await _viewModel.ClearFormCommand.ExecuteAsync(null);

            // Assert
            Assert.Equal(string.Empty, _viewModel.ServiceName);
            Assert.Equal(string.Empty, _viewModel.ProcessPath);
            Assert.False(_viewModel.EnableSizeRotation);
            Assert.Equal(AppConfig.DefaultRotationSize.ToString(), _viewModel.RotationSize);
        }

        [Fact]
        public async Task ClearCommand_DoesNotReset_WhenUserCancels()
        {
            // Arrange
            _viewModel.ServiceName = "TestService";
            _viewModel.ProcessPath = "test.exe";
            _viewModel.EnableSizeRotation = true;
            _viewModel.RotationSize = "555";

            // Mock dialog service to cancel
            _messageBoxService.Setup(ds => ds.ShowConfirmAsync(
                It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            await _viewModel.ClearFormCommand.ExecuteAsync(null);

            // Assert values remain unchanged
            Assert.Equal("TestService", _viewModel.ServiceName);
            Assert.Equal("test.exe", _viewModel.ProcessPath);
            Assert.True(_viewModel.EnableSizeRotation);
            Assert.Equal("555", _viewModel.RotationSize);
        }

        [Fact]
        public void BrowseProcessPathCommand_Sets_ProcessPath_When_File_Selected()
        {
            // Arrange
            _dialogServiceMock.Setup(d => d.OpenExecutable()).Returns("C:\\app.exe");

            // Act
            _viewModel.BrowseProcessPathCommand.Execute(null);

            // Assert
            Assert.Equal("C:\\app.exe", _viewModel.ProcessPath);
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
        public async Task ExportXmlCommand_ValidModel_ShowsSuccessMessage()
        {
            // Arrange
            var path = "export.xml";
            _dialogServiceMock.Setup(d => d.SaveXml(It.IsAny<string>())).Returns(path);

            _serviceCommandsMock.Setup(d => d.ExportXmlConfig(null)).Returns(Task.CompletedTask);

            // Act
            await _viewModel.ExportXmlCommand.ExecuteAsync(null);

            // Assert
            _serviceCommandsMock.Verify(m => m.ExportXmlConfig(null), Times.Once);
        }

        [Fact]
        public async Task ExportJsonCommand_ValidModel_ShowsSuccessMessage()
        {
            // Arrange
            var path = "export.json";
            _dialogServiceMock.Setup(d => d.SaveJson(It.IsAny<string>())).Returns(path);

            _serviceCommandsMock.Setup(d => d.ExportJsonConfig(null)).Returns(Task.CompletedTask);

            // Act
            await _viewModel.ExportJsonCommand.ExecuteAsync(null);

            // Assert
            _serviceCommandsMock.Verify(m => m.ExportJsonConfig(null), Times.Once);
        }

        [Fact]
        public async Task ImportXmlCommand_ValidFile_UpdatesModel()
        {
            // Arrange
            var path = "test.xml";

            _dialogServiceMock.Setup(d => d.OpenXml()).Returns(path);

            _serviceCommandsMock.Setup(d => d.ImportXmlConfig()).Returns(Task.CompletedTask);

            // Act
            await _viewModel.ImportXmlCommand.ExecuteAsync(null);

            // Assert
            _serviceCommandsMock.Verify(m => m.ImportXmlConfig(), Times.Once);
        }
    }
}
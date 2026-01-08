using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Enums;
using Servy.Services;
using Servy.UI.Services;
using Servy.ViewModels;
using System.Threading.Tasks;
using System.Windows;
using Xunit;
using IHelpService = Servy.UI.Services.IHelpService;

namespace Servy.UnitTests.ViewModels
{
    public class MainViewModelTests
    {
        private readonly Mock<IFileDialogService> _dialogServiceMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;
        private readonly Mock<IMessageBoxService> _messageBoxService;
        private readonly Mock<IServiceRepository> _serviceRepository;
        private readonly Mock<IHelpService> _helpService;
        private readonly MainViewModel _viewModel;

        public MainViewModelTests()
        {
            if (Application.Current == null)
            {
                new App();
            }

            _dialogServiceMock = new Mock<IFileDialogService>();
            _serviceCommandsMock = new Mock<IServiceCommands>();
            _messageBoxService = new Mock<IMessageBoxService>();
            _serviceRepository = new Mock<IServiceRepository>();
            _helpService = new Mock<IHelpService>();
            _viewModel = new MainViewModel(_dialogServiceMock.Object,
                _serviceCommandsMock.Object,
                _messageBoxService.Object,
                _serviceRepository.Object,
                _helpService.Object
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
        public void InstallCommand_Calls_InstallService_With_Configuration()
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

            // Act
            _viewModel.InstallCommand.Execute(null);

            // Assert
            _serviceCommandsMock.Verify(s => s.InstallService(
                "TestService",
                "Desc",
                "C:\\test.exe",
                "C:\\",
                "--flag",
                ServiceStartType.Manual,
                ProcessPriority.High,
                "out.log",
                "err.log",
                true,
                "12345",
                true,
                "60",
                "5",
                RecoveryAction.RestartService,
                "3",
                "var1=val1;var2=val2",
                "MongoDB",
                false,
                 @".\username",
                 "password",
                 "password",

                 @"C:\pre-launch.exe",
                 @"C:\",
                 "--param1 val1",
                 "var1=val1; var2=val2;",
                 @"C:\pre-launch-stdout.log",
                 @"C:\pre-launch-stderr.log",
                 "40",
                 "3",
                 true,

                 @"C:\failureProgram.exe",
                 @"C:\failureProgramDir",
                 "--failureProgramParam1 val1",

                 @"C:\post-launch.exe",
                 @"C:\",
                 "--param1 val1",
                 false,
                 "TestServiceDisplayName",
                 "5",
                 true,
                 DateRotationType.Weekly,
                 "11",
                 "6"
            ), Times.Once);
        }

        [Fact]
        public void ClearCommand_Resets_All_Fields_WhenUserConfirms()
        {
            // Arrange
            _viewModel.ServiceName = "TestService";
            _viewModel.ProcessPath = "test.exe";
            _viewModel.EnableSizeRotation = true;
            _viewModel.RotationSize = "555";

            // Mock dialog service to always confirm
            _messageBoxService.Setup(ds => ds.ShowConfirmAsync(
              It.IsAny<string>(), It.IsAny<string>()))
              .Returns(Task.FromResult(true));

            // Act
            _viewModel.ClearFormCommand.Execute(null);

            // Assert
            Assert.Equal(string.Empty, _viewModel.ServiceName);
            Assert.Equal(string.Empty, _viewModel.ProcessPath);
            Assert.False(_viewModel.EnableSizeRotation);
            Assert.Equal(AppConfig.DefaultRotationSize.ToString(), _viewModel.RotationSize);
        }

        [Fact]
        public void ClearCommand_DoesNotReset_WhenUserCancels()
        {
            // Arrange
            _viewModel.ServiceName = "TestService";
            _viewModel.ProcessPath = "test.exe";
            _viewModel.EnableSizeRotation = true;
            _viewModel.RotationSize = "555";

            // Mock dialog service to cancel
            _messageBoxService.Setup(ds => ds.ShowConfirmAsync(
                It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(false));

            // Act
            _viewModel.ClearFormCommand.Execute(null);

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
        public void StartCommand_Calls_StartService()
        {
            _viewModel.ServiceName = "MyService";

            _viewModel.StartCommand.Execute(null);

            _serviceCommandsMock.Verify(s => s.StartService("MyService"), Times.Once);
        }

        [Fact]
        public void StopCommand_Calls_StopService()
        {
            _viewModel.ServiceName = "MyService";

            _viewModel.StopCommand.Execute(null);

            _serviceCommandsMock.Verify(s => s.StopService("MyService"), Times.Once);
        }

        [Fact]
        public void RestartCommand_Calls_RestartService()
        {
            _viewModel.ServiceName = "MyService";

            _viewModel.RestartCommand.Execute(null);

            _serviceCommandsMock.Verify(s => s.RestartService("MyService"), Times.Once);
        }

        [Fact]
        public void UninstallCommand_Calls_UninstallService()
        {
            _viewModel.ServiceName = "MyService";

            _viewModel.UninstallCommand.Execute(null);

            _serviceCommandsMock.Verify(s => s.UninstallService("MyService"), Times.Once);
        }

        [Fact]
        public async Task ExportXmlCommand_ValidModel_ShowsSuccessMessage()
        {
            // Arrange
            var path = "export.xml";
            _dialogServiceMock.Setup(d => d.SaveXml(It.IsAny<string>())).Returns(path);

            _serviceCommandsMock.Setup(d => d.ExportXmlConfig(null));

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

            _serviceCommandsMock.Setup(d => d.ExportJsonConfig(null));

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

            _serviceCommandsMock.Setup(d => d.ImportXmlConfig());

            // Act
            await _viewModel.ImportXmlCommand.ExecuteAsync(null);

            // Assert
            _serviceCommandsMock.Verify(m => m.ImportXmlConfig(), Times.Once);
        }

    }
}

using Moq;
using Servy.Core.Enums;
using Servy.Services;
using Servy.ViewModels;
using Xunit;

namespace Servy.UnitTests.ViewModels
{
    public class MainViewModelTests
    {
        private readonly Mock<IFileDialogService> _dialogServiceMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;
        private readonly MainViewModel _viewModel;

        public MainViewModelTests()
        {
            _dialogServiceMock = new Mock<IFileDialogService>();
            _serviceCommandsMock = new Mock<IServiceCommands>();
            _viewModel = new MainViewModel(_dialogServiceMock.Object, _serviceCommandsMock.Object);
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
            _viewModel.ServiceDescription = "Desc";
            _viewModel.ProcessPath = "C:\\test.exe";
            _viewModel.StartupDirectory = "C:\\";
            _viewModel.ProcessParameters = "--flag";
            _viewModel.StdoutPath = "out.log";
            _viewModel.StderrPath = "err.log";
            _viewModel.EnableRotation = true;
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
                 true

            ), Times.Once);
        }

        [Fact]
        public void ClearCommand_Resets_All_Fields()
        {
            // Arrange
            _viewModel.ServiceName = "TestService";
            _viewModel.ProcessPath = "test.exe";
            _viewModel.EnableRotation = true;
            _viewModel.RotationSize = "555";

            // Act
            _viewModel.ClearCommand.Execute(null);

            // Assert
            Assert.Equal(string.Empty, _viewModel.ServiceName);
            Assert.Equal(string.Empty, _viewModel.ProcessPath);
            Assert.False(_viewModel.EnableRotation);
            Assert.Equal((10 * 1024 * 1024).ToString(), _viewModel.RotationSize);
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
    }
}

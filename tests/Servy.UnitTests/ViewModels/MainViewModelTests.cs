using Moq;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Helpers;
using Servy.Services;
using Servy.ViewModels;

namespace Servy.UnitTests.ViewModels
{
    public class MainViewModelTests
    {
        private readonly Mock<IFileDialogService> _dialogServiceMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;
        private readonly Mock<IMessageBoxService> _messageBoxService;
        private readonly Mock<IServiceConfigurationValidator> _serviceConfigurationValidator;
        private readonly MainViewModel _viewModel;

        public MainViewModelTests()
        {
            _dialogServiceMock = new Mock<IFileDialogService>();
            _serviceCommandsMock = new Mock<IServiceCommands>();
            _messageBoxService = new Mock<IMessageBoxService>();
            _serviceConfigurationValidator = new Mock<IServiceConfigurationValidator>();
            _viewModel = new MainViewModel(_dialogServiceMock.Object, _serviceCommandsMock.Object, _messageBoxService.Object, _serviceConfigurationValidator.Object);
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
        public void ClearCommand_Resets_All_Fields_WhenUserConfirms()
        {
            // Arrange
            _viewModel.ServiceName = "TestService";
            _viewModel.ProcessPath = "test.exe";
            _viewModel.EnableRotation = true;
            _viewModel.RotationSize = "555";

            // Mock dialog service to always confirm
            _messageBoxService.Setup(ds => ds.ShowConfirm(
              It.IsAny<string>(), It.IsAny<string>()))
              .Returns(true);

            // Act
            _viewModel.ClearCommand.Execute(null);

            // Assert
            Assert.Equal(string.Empty, _viewModel.ServiceName);
            Assert.Equal(string.Empty, _viewModel.ProcessPath);
            Assert.False(_viewModel.EnableRotation);
            Assert.Equal((10 * 1024 * 1024).ToString(), _viewModel.RotationSize);
        }

        [Fact]
        public void ClearCommand_DoesNotReset_WhenUserCancels()
        {
            // Arrange
            _viewModel.ServiceName = "TestService";
            _viewModel.ProcessPath = "test.exe";
            _viewModel.EnableRotation = true;
            _viewModel.RotationSize = "555";

            // Mock dialog service to cancel
            _messageBoxService.Setup(ds => ds.ShowConfirm(
                It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);

            // Act
            _viewModel.ClearCommand.Execute(null);

            // Assert values remain unchanged
            Assert.Equal("TestService", _viewModel.ServiceName);
            Assert.Equal("test.exe", _viewModel.ProcessPath);
            Assert.True(_viewModel.EnableRotation);
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
        public void ImportXmlCommand_ValidFile_UpdatesModel()
        {
            // Arrange
            var xmlContent = @"<ServiceDto><Name>TestService</Name><ExecutablePath>C:\MyApp.exe</ExecutablePath></ServiceDto>";
            var path = "test.xml";

            _serviceConfigurationValidator.Setup(d => d.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>())).Returns(true);

            _dialogServiceMock.Setup(d => d.OpenXml()).Returns(path);
            File.WriteAllText(path, xmlContent); // alternatively, mock File.ReadAllText

            // Act
            _viewModel.ImportXmlCommand.Execute(null);

            // Assert
            Assert.Equal("TestService", _viewModel.ServiceName);
            // Cleanup
            File.Delete(path);
        }

        [Fact]
        public void ImportJsonCommand_ValidFile_UpdatesModel()
        {
            // Arrange
            var jsonContent = "{\"Name\":\"TestService\", \"ExecutablePath\":\"C:\\\\MyApp.exe\"}";
            var path = "test.json";

            _serviceConfigurationValidator.Setup(d => d.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>())).Returns(true);

            _dialogServiceMock.Setup(d => d.OpenJson()).Returns(path);
            File.WriteAllText(path, jsonContent);

            // Act
            _viewModel.ImportJsonCommand.Execute(null);

            // Assert
            Assert.Equal("TestService", _viewModel.ServiceName);
            File.Delete(path);
        }

        [Fact]
        public void ExportXmlCommand_ValidModel_ShowsSuccessMessage()
        {
            // Arrange
            var path = "export.xml";
            _dialogServiceMock.Setup(d => d.SaveXml(It.IsAny<string>())).Returns(path);

            _viewModel.ServiceName = "TestService";

            _serviceConfigurationValidator.Setup(d => d.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>())).Returns(true);

            // Act
            _viewModel.ExportXmlCommand.Execute(null);

            // Assert
            _messageBoxService.Verify(m => m.ShowInfo(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            Assert.True(File.Exists(path));
            File.Delete(path);
        }

        [Fact]
        public void ExportJsonCommand_ValidModel_ShowsSuccessMessage()
        {
            // Arrange
            var path = "export.json";
            _dialogServiceMock.Setup(d => d.SaveJson(It.IsAny<string>())).Returns(path);

            _viewModel.ServiceName = "TestService";

            _serviceConfigurationValidator.Setup(d => d.Validate(It.IsAny<ServiceDto>(), It.IsAny<string>())).Returns(true);

            // Act
            _viewModel.ExportJsonCommand.Execute(null);

            // Assert
            _messageBoxService.Verify(m => m.ShowInfo(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            Assert.True(File.Exists(path));
            File.Delete(path);
        }

        [Fact]
        public void ImportXmlCommand_UserCancels_ShowsNothing()
        {
            // Arrange
            _dialogServiceMock.Setup(d => d.OpenXml()).Returns(string.Empty);

            // Act
            _viewModel.ImportXmlCommand.Execute(null);

            // Assert
            _messageBoxService.Verify(m => m.ShowError(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ExportJsonCommand_UserCancels_ShowsNothing()
        {
            // Arrange
            _dialogServiceMock.Setup(d => d.SaveJson(It.IsAny<string>())).Returns(string.Empty);

            // Act
            _viewModel.ExportJsonCommand.Execute(null);

            // Assert
            _messageBoxService.Verify(m => m.ShowInfo(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}

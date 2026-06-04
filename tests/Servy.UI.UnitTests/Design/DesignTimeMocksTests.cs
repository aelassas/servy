using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Services;
using Servy.UI.Design;
using System.ServiceProcess;
using System.Windows.Threading;

namespace Servy.UI.UnitTests.Design
{
    public class DesignTimeMocksTests
    {
        private readonly DesignTimeUiDispatcher _dispatcher;

        public DesignTimeMocksTests()
        {
            _dispatcher = new DesignTimeUiDispatcher();
        }

        #region ProcessHelper Tests

        [Fact]
        public void DesignTimeProcessHelper_CanBeInstantiated()
        {
            // Act
            var helper = new DesignTimeProcessHelper();

            // Assert: Verify the object is created successfully
            Assert.NotNull(helper);

            // Assert: Verify it inherits from the base ProcessHelper
            Assert.IsAssignableFrom<ProcessHelper>(helper);
        }

        #endregion

        #region Repository Tests

        [Fact]
        public async Task DesignTimeServiceRepository_Methods_ReturnDefaultValues()
        {
            var repo = new DesignTimeServiceRepository();
            var ct = TestContext.Current.CancellationToken;

            // Sync branches
            Assert.Null(repo.GetByName("test"));
            Assert.Null(repo.GetByName("test", decrypt: false));

            // Async branches (Task.FromResult coverage)
            Assert.Null(await repo.GetByIdAsync(1, cancellationToken: ct));
            Assert.Null(await repo.GetByNameAsync("test", cancellationToken: ct));
            Assert.Null(await repo.GetServicePidAsync("test", cancellationToken: ct));
            Assert.Null(await repo.GetServiceConsoleStateAsync("test", cancellationToken: ct));

            Assert.Empty(await repo.GetAllAsync(cancellationToken: ct));
            Assert.Empty(await repo.SearchAsync("key", cancellationToken: ct));

            Assert.Equal(string.Empty, await repo.ExportXmlAsync("test", cancellationToken: ct));
            Assert.Equal(string.Empty, await repo.ExportJsonAsync("test", cancellationToken: ct));
            Assert.True(await repo.ImportXmlAsync("<xml/>", cancellationToken: ct));
            Assert.True(await repo.ImportJsonAsync("{}", cancellationToken: ct));

            // Void/Int branches
            repo.Upsert(new ServiceDto());
            repo.Delete("test");
            Assert.Equal(0, repo.Update(new ServiceDto(), true, true));
            Assert.Equal(0, await repo.DeleteAsync(1, cancellationToken: ct));
        }

        #endregion

        #region Service Manager Tests

        [Fact]
        public async Task DesignTimeServiceManager_Methods_ReturnSafeDefaults()
        {
            var manager = new DesignTimeServiceManager();
            var ct = TestContext.Current.CancellationToken;

            // Result branches
            var result = await manager.InstallServiceAsync(new InstallServiceOptions(), cancellationToken: ct);
            Assert.True(result.IsSuccess);

            Assert.True((await manager.UninstallServiceAsync("test", cancellationToken: ct)).IsSuccess);
            Assert.True((await manager.StartServiceAsync("test", cancellationToken: ct)).IsSuccess);
            Assert.True((await manager.StopServiceAsync("test", cancellationToken: ct)).IsSuccess);
            Assert.True((await manager.RestartServiceAsync("test", cancellationToken: ct)).IsSuccess);

            // Status branches
            Assert.Equal(ServiceControllerStatus.Stopped, manager.GetServiceStatus("test", cancellationToken: ct));
            Assert.False(manager.IsServiceInstalled("test", TestContext.Current.CancellationToken));
            Assert.Equal(ServiceStartType.Manual, manager.GetServiceStartupType("test", cancellationToken: ct));

            // Collection branches
            Assert.Empty(manager.GetAllServices(cancellationToken: ct));
            Assert.Null(manager.GetDependencies("test", TestContext.Current.CancellationToken));
        }

        #endregion

        #region UI Services Tests

        [Fact]
        public async Task DesignTimeMessageBoxService_ReturnsTrueAndCompletes()
        {
            var service = new DesignTimeMessageBoxService();
            var ct = TestContext.Current.CancellationToken;

            Assert.True(await service.ShowConfirmAsync("Message", "Caption"));

            await service.ShowErrorAsync("Err", "Cap");
            await service.ShowInfoAsync("Inf", "Cap");
            await service.ShowWarningAsync("Warn", "Cap");
        }

        [Fact]
        public void DesignTimeCursorService_ResetCursor_DoesNotThrow()
        {
            // Arrange
            var service = new DesignTimeCursorService();

            // Act & Assert
            // Branch: Simple no-op method body
            var exception = Record.Exception(() => service.ResetCursor());
            Assert.Null(exception);
        }

        [Fact]
        public void DesignTimeCursorService_UsingBlock_WorksCorrectly()
        {
            // Verifies the "mechanical necessity" of the NoOpDisposable 
            // within a standard ViewModel pattern.
            var service = new DesignTimeCursorService();

            var exception = Record.Exception(service.SetWaitCursor);

            Assert.Null(exception);
        }

        [Fact]
        public async Task DesignTimeHelpService_Methods_Complete()
        {
            var service = new DesignTimeHelpService();
            var ct = TestContext.Current.CancellationToken;

            await service.OpenDocumentation("caption");
            await service.CheckUpdates("caption");
            await service.OpenAboutDialog("about", "caption");
        }

        [Fact]
        public void DesignTimeFileDialogService_ReturnsNullForAllMethods()
        {
            // Arrange
            var service = new DesignTimeFileDialogService();
            const string testTitle = "Test Title";

            // Act & Assert
            // Branch: Each method is a direct return null;
            Assert.Null(service.OpenExecutable());
            Assert.Null(service.OpenFolder());
            Assert.Null(service.OpenJson());
            Assert.Null(service.OpenXml());
            Assert.Null(service.SaveFile(testTitle));
            Assert.Null(service.SaveJson(testTitle));
            Assert.Null(service.SaveXml(testTitle));
        }

        #endregion

        #region Infrastructure Tests

        [Fact]
        public async Task InvokeAsync_Action_CompletesSuccessfully()
        {
            bool wasExecuted = false;

            // Act
            Task task = _dispatcher.InvokeAsync(() => wasExecuted = true);

            // Assert
            Assert.True(task.IsCompletedSuccessfully, "Task should be completed immediately.");
            Assert.False(wasExecuted, "Action should not be executed in design-time mode.");
        }

        [Fact]
        public async Task InvokeAsync_ActionWithPriority_CompletesSuccessfully()
        {
            bool wasExecuted = false;

            // Act
            Task task = _dispatcher.InvokeAsync(() => wasExecuted = true, DispatcherPriority.Normal);

            // Assert
            Assert.True(task.IsCompletedSuccessfully, "Task should be completed immediately.");
            Assert.False(wasExecuted, "Action should not be executed in design-time mode.");
        }

        [Fact]
        public async Task InvokeAsync_GenericFunc_ReturnsDefaultValue()
        {
            // Act: Reference type
            Task<string> refTask = _dispatcher.InvokeAsync(() => "Value");

            // Act: Value type
            Task<int> valTask = _dispatcher.InvokeAsync(() => 42);

            // Assert
            Assert.Null(await refTask);
            Assert.Equal(0, await valTask);
        }

        [Fact]
        public async Task YieldAsync_CompletesSuccessfully()
        {
            // Act
            Task task = _dispatcher.YieldAsync();

            // Assert
            Assert.True(task.IsCompletedSuccessfully, "YieldAsync task should return completed state.");
        }

        #endregion
    }
}
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Services;
using Servy.UI.Design;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;

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

            // Assert: Verify it inherits from the base ProcessHelper
            Assert.IsAssignableFrom<ProcessHelper>(helper);
        }

        #endregion

        #region Repository Tests

        [Fact]
        public async Task DesignTimeServiceRepository_Methods_ReturnDefaultValues()
        {
            var repo = new DesignTimeServiceRepository();

            // Sync branches
            Assert.Null(repo.GetByName("test"));
            Assert.Null(repo.GetByName("test", decrypt: false));

            // Async branches (Task.FromResult coverage)
            Assert.Null(await repo.GetByIdAsync(1, cancellationToken: CancellationToken.None));
            Assert.Null(await repo.GetByNameAsync("test", cancellationToken: CancellationToken.None));
            Assert.Null(await repo.GetServicePidAsync("test", cancellationToken: CancellationToken.None));
            Assert.Null(await repo.GetServiceConsoleStateAsync("test", cancellationToken: CancellationToken.None));

            Assert.Empty(await repo.GetAllAsync(cancellationToken: CancellationToken.None));
            Assert.Empty(await repo.SearchAsync("key", cancellationToken: CancellationToken.None));

            Assert.Equal(string.Empty, await repo.ExportXmlAsync("test", cancellationToken: CancellationToken.None));
            Assert.Equal(string.Empty, await repo.ExportJsonAsync("test", cancellationToken: CancellationToken.None));
            Assert.True(await repo.ImportXmlAsync("<xml/>", cancellationToken: CancellationToken.None));
            Assert.True(await repo.ImportJsonAsync("{}", cancellationToken: CancellationToken.None));

            // Void/Int branches
            repo.Upsert(new ServiceDto());
            repo.Delete("test");
            Assert.Equal(0, repo.Update(new ServiceDto(), true, true));
            Assert.Equal(0, await repo.DeleteAsync(1, cancellationToken: CancellationToken.None));
        }

        #endregion

        #region Service Manager Tests

        [Fact]
        public async Task DesignTimeServiceManager_Methods_ReturnSafeDefaults()
        {
            var manager = new DesignTimeServiceManager();
            var ct = CancellationToken.None;

            // Result branches
            var result = await manager.InstallServiceAsync(new InstallServiceOptions(), cancellationToken: ct);
            Assert.True(result.IsSuccess);

            Assert.True((await manager.UninstallServiceAsync("test", cancellationToken: ct)).IsSuccess);
            Assert.True((await manager.StartServiceAsync("test", cancellationToken: ct)).IsSuccess);
            Assert.True((await manager.StopServiceAsync("test", cancellationToken: ct)).IsSuccess);
            Assert.True((await manager.RestartServiceAsync("test", cancellationToken: ct)).IsSuccess);

            // Status branches
            Assert.Equal(ServiceControllerStatus.Stopped, manager.GetServiceStatus("test", cancellationToken: ct));
            Assert.False(manager.IsServiceInstalled("test", ct));
            Assert.Equal(ServiceStartType.Manual, manager.GetServiceStartupType("test", cancellationToken: ct));

            // Collection branches
            Assert.Empty(manager.GetAllServices(cancellationToken: ct));
            Assert.Null(manager.GetDependencies("test", ct));
        }

        #endregion

        #region UI Services Tests

        [Fact]
        public async Task DesignTimeMessageBoxService_ReturnsTrueAndCompletes()
        {
            var service = new DesignTimeMessageBoxService();

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
        public void DesignTimeCursorService_SetWaitCursor_DoesNotThrow()
        {
            var service = new DesignTimeCursorService();

            var exception = Record.Exception(service.SetWaitCursor);

            Assert.Null(exception);
        }

        [Fact]
        public async Task DesignTimeHelpService_Methods_Complete()
        {
            var service = new DesignTimeHelpService();

            await service.OpenDocumentationAsync("caption");
            await service.CheckUpdatesAsync("caption");
            await service.OpenAboutDialogAsync("about", "caption");
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
        public void InvokeAsync_Action_CompletesSuccessfully()
        {
            bool wasExecuted = false;

            // Act
            Task task = _dispatcher.InvokeAsync(() => wasExecuted = true);

            // Assert
            Assert.True(task.IsCompleted, "Task should be completed immediately.");
            Assert.False(wasExecuted, "Action should not be executed in design-time mode.");
        }

        [Fact]
        public void InvokeAsync_ActionWithPriority_CompletesSuccessfully()
        {
            bool wasExecuted = false;

            // Act
            Task task = _dispatcher.InvokeAsync(() => wasExecuted = true, DispatcherPriority.Normal);

            // Assert
            Assert.True(task.IsCompleted, "Task should be completed immediately.");
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
        public void YieldAsync_CompletesSuccessfully()
        {
            // Act
            Task task = _dispatcher.YieldAsync();

            // Assert
            Assert.True(task.IsCompleted, "YieldAsync task should return completed state.");
        }

        #endregion
    }
}
using Servy.Core.DTOs;
using Servy.Core.Enums;
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
            // Arrange & Act & Assert
            // Covers the parameterless constructor the XAML designer needs, which runs the
            // base ProcessHelper constructor. Inheritance from ProcessHelper is compile-time
            // enforced, so asserting it here cannot fail.
            var exception = Record.Exception(() => new DesignTimeProcessHelper());
            Assert.Null(exception);
        }

        #endregion

        #region Repository Tests

        [Fact]
        public async Task DesignTimeServiceRepository_Methods_ReturnDefaultValues()
        {
            // Arrange
            var repo = new DesignTimeServiceRepository();
            var ct = TestContext.Current.CancellationToken;

            // Act & Assert - Sync branches
            Assert.Null(repo.GetByName("test"));
            Assert.Null(repo.GetByName("test", decrypt: false));

            // Act & Assert - Async branches (Task.FromResult coverage)
            Assert.Null(await repo.GetByIdAsync(1, cancellationToken: ct));
            Assert.Null(await repo.GetByNameAsync("test", cancellationToken: ct));
            Assert.Null(await repo.GetServicePidAsync("test", cancellationToken: ct));
            Assert.Null(await repo.GetServiceConsoleStateAsync("test", cancellationToken: ct));

            Assert.Empty(await repo.GetAllAsync(cancellationToken: ct));
            Assert.Empty(await repo.SearchAsync("key", cancellationToken: ct));

            Assert.Equal(string.Empty, await repo.ExportXmlAsync("test", cancellationToken: ct));
            Assert.Equal(string.Empty, await repo.ExportJsonAsync("test", cancellationToken: ct));
            Assert.True((await repo.ImportXmlAsync("<xml/>", cancellationToken: ct)).IsSuccess);
            Assert.True((await repo.ImportJsonAsync("{}", cancellationToken: ct)).IsSuccess);

            // Act & Assert - Void/Int branches
            repo.Upsert(new ServiceDto());
            repo.Delete("test");
            Assert.Equal(0, repo.Update(new ServiceDto(), true, true));
            Assert.Equal(0, await repo.DeleteAsync(1, cancellationToken: ct));
            Assert.Equal(0, await repo.DeleteAsync("test", cancellationToken: ct));
            Assert.Equal(0, await repo.AddAsync(new ServiceDto(), cancellationToken: ct));
            Assert.Equal(0, await repo.UpdateAsync(new ServiceDto(), preserveExistingRuntimeState: true, preserveExistingCredentials: true, cancellationToken: ct));
            Assert.Equal(0, await repo.UpsertAsync(new ServiceDto(), preserveExistingRuntimeState: true, preserveExistingCredentials: true, cancellationToken: ct));
            Assert.Equal(0, await repo.UpsertBatchAsync(new[] { new ServiceDto() }, cancellationToken: ct));
        }

        #endregion

        #region Service Manager Tests

        [Fact]
        public async Task DesignTimeServiceManager_Methods_ReturnSafeDefaults()
        {
            // Arrange
            var manager = new DesignTimeServiceManager();
            var ct = TestContext.Current.CancellationToken;

            // Act & Assert - Result branches
            var result = await manager.InstallServiceAsync(new InstallServiceOptions(), cancellationToken: ct);
            Assert.True(result.IsSuccess);

            Assert.True((await manager.UninstallServiceAsync("test", cancellationToken: ct)).IsSuccess);
            Assert.True((await manager.StartServiceAsync("test", cancellationToken: ct)).IsSuccess);
            Assert.True((await manager.StopServiceAsync("test", cancellationToken: ct)).IsSuccess);
            Assert.True((await manager.RestartServiceAsync("test", cancellationToken: ct)).IsSuccess);

            // Act & Assert - Status branches
            Assert.Equal(ServiceControllerStatus.Stopped, manager.GetServiceStatus("test", cancellationToken: ct));
            Assert.False(manager.IsServiceInstalled("test", ct));
            Assert.Equal(ServiceStartType.Manual, manager.GetServiceStartupType("test", cancellationToken: ct));

            // Act & Assert - Collection branches
            Assert.Empty(manager.GetAllServices(cancellationToken: ct));
            Assert.Null(manager.GetDependencies("test", ct));
        }

        #endregion

        #region Event Log Service Tests

        [Fact]
        public async Task DesignTimeEventLogService_SearchAsync_ReturnsEmpty()
        {
            // Arrange
            var service = new DesignTimeEventLogService();
            var ct = TestContext.Current.CancellationToken;

            // Act
            var result = await service.SearchAsync(keyword: "test", level: EventLogLevel.All, startDate: DateTime.MinValue, endDate: DateTime.Now, token: ct);

            // Assert
            Assert.Empty(result);
        }

        #endregion

        #region UI Services Tests

        [Fact]
        public async Task DesignTimeMessageBoxService_ReturnsTrueAndCompletes()
        {
            // Arrange
            var service = new DesignTimeMessageBoxService();

            // Act & Assert
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
            // Arrange
            var service = new DesignTimeCursorService();

            // Act
            var exception = Record.Exception(() => service.SetWaitCursor());

            // Assert
            Assert.Null(exception);
        }

        [Fact]
        public async Task DesignTimeHelpService_Methods_Complete()
        {
            // Arrange
            var service = new DesignTimeHelpService();

            // Act & Assert
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
            // Arrange
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
            // Arrange
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
            // Arrange & Act - Reference type
            Task<string> refTask = _dispatcher.InvokeAsync(() => "Value");

            // Arrange & Act - Value type
            Task<int> valTask = _dispatcher.InvokeAsync(() => 42);

            // Assert
            Assert.Null(await refTask);
            Assert.Equal(0, await valTask);
        }

        [Fact]
        public void YieldAsync_CompletesSuccessfully()
        {
            // Arrange & Act
            Task task = _dispatcher.YieldAsync();

            // Assert
            Assert.True(task.IsCompleted, "YieldAsync task should return completed state.");
        }

        #endregion
    }
}

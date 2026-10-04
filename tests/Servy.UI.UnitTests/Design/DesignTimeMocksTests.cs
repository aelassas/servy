using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Services;
using Servy.Testing;
using Servy.UI.Design;
using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
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
            var ct = CancellationToken.None;

            // Act & Assert - Async branches (Task.FromResult coverage)
            Assert.Null(await repo.GetByNameAsync("test", cancellationToken: ct));
            Assert.Null(await repo.GetServicePidAsync("test", cancellationToken: ct));
            Assert.Null(await repo.GetServiceConsoleStateAsync("test", cancellationToken: ct));

            Assert.Empty(await repo.GetAllAsync(cancellationToken: ct));
            Assert.Empty(await repo.SearchAsync("key", cancellationToken: ct));

            Assert.Equal(string.Empty, await repo.ExportXmlAsync("test", cancellationToken: ct));
            Assert.Equal(string.Empty, await repo.ExportJsonAsync("test", cancellationToken: ct));

            // Act & Assert - Void/Int branches
            repo.Upsert(new ServiceDto());
            repo.Delete("test");
            Assert.Equal(0, await repo.DeleteAsync(1, cancellationToken: ct));
            Assert.Equal(0, await repo.DeleteAsync("test", cancellationToken: ct));
            Assert.Equal(0, await repo.UpsertAsync(new ServiceDto(), preserveExistingRuntimeState: true, preserveExistingCredentials: true, cancellationToken: ct));
        }

        #endregion

        #region Service Manager Tests

        [Fact]
        public async Task DesignTimeServiceManager_Methods_ReturnSafeDefaults()
        {
            // Arrange
            var manager = new DesignTimeServiceManager();
            var ct = CancellationToken.None;

            // Act & Assert - Result branches
            var result = await manager.InstallServiceAsync(new InstallServiceOptions(), cancellationToken: ct);
            Assert.True(result.IsSuccess);

            Assert.True((await manager.UninstallServiceAsync("test", cancellationToken: ct)).IsSuccess);
            Assert.True((await manager.StartServiceAsync("test", cancellationToken: ct)).IsSuccess);
            Assert.True((await manager.StopServiceAsync("test", cancellationToken: ct)).IsSuccess);
            Assert.True((await manager.RestartServiceAsync("test", cancellationToken: ct)).IsSuccess);

            // Act & Assert - Status branches
            Assert.Null(manager.GetServiceStatus("test", cancellationToken: ct));
            Assert.False(manager.IsServiceInstalled("test", ct));
            Assert.Equal(ServiceStartType.Manual, manager.GetServiceStartupType("test", cancellationToken: ct));

            // Act & Assert - Collection branches
            Assert.Empty(manager.GetAllServices(cancellationToken: ct));
            Assert.Null(manager.GetDependencies("test", ct));

            // Act & Assert - No-op branches
            // Capture the tasks instead of awaiting them: awaiting an already-completed task
            // asserts nothing, while synchronous completion is the property the designer depends
            // on - a stub that started yielding would leave it awaiting something that never resumes.
            Assert.Equal(TaskStatus.RanToCompletion, manager.RevokeVaultAccessIfUnusedAsync(null, ct).Status);
            Assert.Equal(TaskStatus.RanToCompletion, manager.RevokeVaultAccessIfUnusedAsync(new ServiceDto { Name = "test" }, ct).Status);
        }

        #endregion

        #region Event Log Service Tests

        [Fact]
        public async Task DesignTimeEventLogService_SearchAsync_ReturnsEmpty()
        {
            // Arrange
            var service = new DesignTimeEventLogService();
            var ct = CancellationToken.None;

            // Act
            var result = await service.SearchAsync(keyword: "test", level: EventLogLevel.All, startDate: DateTime.MinValue, endDate: DateTime.Now, token: ct);

            // Assert
            Assert.Empty(result);
        }

        #endregion

        #region UI Services Tests

        [Fact]
        public void DesignTimeMessageBoxService_Methods_ReturnAlreadyCompletedTasks()
        {
            // Arrange
            var service = new DesignTimeMessageBoxService();

            // Act & Assert
            // Capture the tasks instead of awaiting them: awaiting an already-completed task
            // asserts nothing, while synchronous completion is the property the designer depends
            // on - a stub that started yielding would leave it awaiting something that never resumes.
            var confirm = service.ShowConfirmAsync("Message", "Caption");
            Assert.Equal(TaskStatus.RanToCompletion, confirm.Status);
            Assert.True(confirm.Result);
            Assert.Equal(TaskStatus.RanToCompletion, service.ShowErrorAsync("Err", "Cap").Status);
            Assert.Equal(TaskStatus.RanToCompletion, service.ShowInfoAsync("Inf", "Cap").Status);
            Assert.Equal(TaskStatus.RanToCompletion, service.ShowWarningAsync("Warn", "Cap").Status);
        }

        [Fact]
        public void DesignTimeCursorService_DoesNotTouchGlobalCursor()
        {
            // The contract worth pinning is not "the no-op did not crash" - an empty body has no
            // statement that could throw, so that assertion has no failing input - but that the
            // stub leaves WPF's global cursor exactly as it found it. That is observable on an
            // STA thread, and it fails the moment either stub is given a real body.
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var service = new DesignTimeCursorService();
                Mouse.OverrideCursor = Cursors.Hand;

                try
                {
                    // Act & Assert
                    service.SetWaitCursor();
                    Assert.Same(Cursors.Hand, Mouse.OverrideCursor);   // not Cursors.Wait

                    service.ResetCursor();
                    Assert.Same(Cursors.Hand, Mouse.OverrideCursor);   // not null
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            });
        }

        [Fact]
        public void DesignTimeHelpService_Methods_ReturnAlreadyCompletedTasks()
        {
            // Arrange
            var service = new DesignTimeHelpService();

            // Act & Assert
            // Capture the tasks instead of awaiting them: awaiting an already-completed task
            // asserts nothing, while synchronous completion is the property the designer depends
            // on - a stub that started yielding would leave it awaiting something that never resumes.
            Assert.Equal(TaskStatus.RanToCompletion, service.OpenDocumentationAsync("caption").Status);
            Assert.Equal(TaskStatus.RanToCompletion, service.CheckUpdatesAsync("caption").Status);
            Assert.Equal(TaskStatus.RanToCompletion, service.OpenAboutDialogAsync("about", "caption").Status);
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
            // A statement body has no value, so only the Action overload is applicable. The
            // expression form '() => wasExecuted = true' binds to InvokeAsync<bool>(Func<bool>)
            // instead, which is what left the Action overload without a test.
            Task task = _dispatcher.InvokeAsync(() => { wasExecuted = true; });

            // Assert
            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
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
            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            Assert.False(wasExecuted, "Action should not be executed in design-time mode.");
        }

        [Fact]
        public async Task InvokeAsync_GenericFunc_ReturnsDefaultValueWithoutInvokingCallback()
        {
            // Arrange
            bool wasExecuted = false;

            // Act - Reference type. A statement body still returns a value, so it binds to
            // InvokeAsync<string>(Func<string>); there is no Func<T> overload taking a
            // DispatcherPriority to compete with it.
            Task<string> refTask = _dispatcher.InvokeAsync(() => { wasExecuted = true; return "Value"; });

            // Act - Value type
            Task<int> valTask = _dispatcher.InvokeAsync(() => 42);

            // Assert
            Assert.Null(await refTask);
            Assert.Equal(0, await valTask);
            Assert.False(wasExecuted, "Callback should not be executed in design-time mode.");
        }

        [Fact]
        public void YieldAsync_CompletesSuccessfully()
        {
            // Arrange & Act
            Task task = _dispatcher.YieldAsync();

            // Assert
            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
        }

        #endregion
    }
}

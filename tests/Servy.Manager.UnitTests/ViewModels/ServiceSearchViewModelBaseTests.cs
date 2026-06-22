using Moq;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.UI.Services;

namespace Servy.Manager.UnitTests.ViewModels
{
    public class ServiceSearchViewModelBaseTests
    {
        private readonly Mock<ICursorService> _cursorServiceMock;
        private readonly Mock<IUiDispatcher> _uiDispatcherMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;

        public ServiceSearchViewModelBaseTests()
        {
            _cursorServiceMock = new Mock<ICursorService>();
            _uiDispatcherMock = new Mock<IUiDispatcher>();
            _serviceCommandsMock = new Mock<IServiceCommands>();

            // Ensure the dispatcher doesn't hang the async state machine during tests
            _uiDispatcherMock.Setup(d => d.YieldAsync()).Returns(Task.CompletedTask);
        }

        #region Factory Stub Class

        /// <summary>
        /// Concrete test double to instantiate and test abstract base features safely.
        /// </summary>
        private class TestServiceSearchViewModel : ServiceSearchViewModelBase
        {
            public Func<Service?, ServiceItemBase>? CustomCreateServiceItem { get; set; }

            public TestServiceSearchViewModel(
                ICursorService cursorService,
                IUiDispatcher uiDispatcher,
                IServiceCommands serviceCommands)
                : base(cursorService, uiDispatcher, serviceCommands)
            {
            }

            protected override ServiceItemBase CreateServiceItem(Service? service)
            {
                if (CustomCreateServiceItem != null)
                    return CustomCreateServiceItem(service);

                return new Mock<ServiceItemBase>().Object;
            }

            public CancellationTokenSource? GetCancellationTokenSource() => _serviceSearchCts;
            public void SetCancellationTokenSource(CancellationTokenSource cts) => _serviceSearchCts = cts;
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_NullArguments_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TestServiceSearchViewModel(null!, _uiDispatcherMock.Object, _serviceCommandsMock.Object));

            Assert.Throws<ArgumentNullException>(() =>
                new TestServiceSearchViewModel(_cursorServiceMock.Object, null!, _serviceCommandsMock.Object));

            Assert.Throws<ArgumentNullException>(() =>
                new TestServiceSearchViewModel(_cursorServiceMock.Object, _uiDispatcherMock.Object, null!));
        }

        [Fact]
        public void Constructor_ValidArguments_InitializesPropertiesAndCommands()
        {
            var sut = new TestServiceSearchViewModel(_cursorServiceMock.Object, _uiDispatcherMock.Object, _serviceCommandsMock.Object);

            Assert.NotNull(sut.Services);
            Assert.Empty(sut.Services);
            Assert.Equal(Strings.Button_Search, sut.SearchButtonText);
            Assert.False(sut.IsBusy);
            Assert.NotNull(sut.SearchCommand);
        }

        #endregion

        #region Property Mutation & INotifyPropertyChanged Tests

        [Fact]
        public void Properties_SetNewValues_TriggersPropertyNotificationEvents()
        {
            var sut = new TestServiceSearchViewModel(_cursorServiceMock.Object, _uiDispatcherMock.Object, _serviceCommandsMock.Object);
            var changedProperties = new List<string>();
            sut.PropertyChanged += (s, e) => { if (e.PropertyName != null) changedProperties.Add(e.PropertyName); };

            sut.SearchText = "Wexflow";
            sut.SearchButtonText = "Searching Now...";
            sut.IsBusy = true;

            Assert.Contains(nameof(sut.SearchText), changedProperties);
            Assert.Contains(nameof(sut.SearchButtonText), changedProperties);
            Assert.Contains(nameof(sut.IsBusy), changedProperties);
        }

        #endregion

        #region Asynchronous Search Flows

        [Fact]
        public async Task SearchServicesAsync_SuccessfulExecution_PopulatesCollectionsAndResetsCursor()
        {
            // Arrange
            var sut = new TestServiceSearchViewModel(_cursorServiceMock.Object, _uiDispatcherMock.Object, _serviceCommandsMock.Object);
            var mockRawServices = new List<Service> { new Service { Name = "ServyCore" } };

            _serviceCommandsMock
                .Setup(s => s.SearchServicesAsync("Servy", false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRawServices);

            var mockItem = new Mock<ServiceItemBase>().Object;
            sut.CustomCreateServiceItem = (srv) => mockItem;
            sut.SearchText = "Servy";

            // Act
            await sut.SearchCommand.ExecuteAsync(null);

            // Assert
            Assert.Single(sut.Services);
            Assert.Same(mockItem, sut.Services.First());

            // State resets verified in finally lock
            Assert.False(sut.IsBusy);
            Assert.Equal(Strings.Button_Search, sut.SearchButtonText);

            _cursorServiceMock.Verify(c => c.SetWaitCursor(), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
            _uiDispatcherMock.Verify(d => d.YieldAsync(), Times.Once);
        }

        [Fact]
        public async Task SearchServicesAsync_NullServiceCommands_AbortsGracefully()
        {
            var sut = new TestServiceSearchViewModel(_cursorServiceMock.Object, _uiDispatcherMock.Object, _serviceCommandsMock.Object);
            sut.ServiceCommands = null!; // Force bypass assignment checks via reflection swap

            await sut.SearchCommand.ExecuteAsync(null);

            _serviceCommandsMock.Verify(s => s.SearchServicesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            _cursorServiceMock.Verify(c => c.SetWaitCursor(), Times.Never);
        }

        [Fact]
        public async Task SearchServicesAsync_OperationCancelledExceptionThrown_HandlesGracefully()
        {
            var sut = new TestServiceSearchViewModel(_cursorServiceMock.Object, _uiDispatcherMock.Object, _serviceCommandsMock.Object);

            _serviceCommandsMock
                .Setup(s => s.SearchServicesAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            var exception = await Record.ExceptionAsync(() => sut.SearchCommand.ExecuteAsync(null));

            // Assert that target exception was caught and handled internally without blowing up the execution stack
            Assert.Null(exception);
            Assert.False(sut.IsBusy);
        }

        [Fact]
        public async Task SearchServicesAsync_GenericExceptionThrown_LogsErrorAndCleansUpState()
        {
            var sut = new TestServiceSearchViewModel(_cursorServiceMock.Object, _uiDispatcherMock.Object, _serviceCommandsMock.Object);

            _serviceCommandsMock
                .Setup(s => s.SearchServicesAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Database/SCM connection lost"));

            await sut.SearchCommand.ExecuteAsync(null);

            // Should reach finally block cleanly despite crash
            Assert.False(sut.IsBusy);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        [Fact]
        public async Task SearchServicesAsync_SupersededBeforeCompletion_DoesNotOverwriteUiState()
        {
            // Arrange
            var sut = new TestServiceSearchViewModel(_cursorServiceMock.Object, _uiDispatcherMock.Object, _serviceCommandsMock.Object);
            var tcs = new TaskCompletionSource<List<Service>>();

            _serviceCommandsMock
                .Setup(s => s.SearchServicesAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
                .Returns(tcs.Task);

            var executionTask = sut.SearchCommand.ExecuteAsync(null);
            var executionTokenSource = sut.GetCancellationTokenSource();

            // Force a newer token source allocation behind the scenes to mimic a racing search execution
            var racingCts = new CancellationTokenSource();
            sut.SetCancellationTokenSource(racingCts);

            // Force execution to proceed past the await boundary loop frame
            executionTokenSource!.Cancel();
            tcs.SetResult(new List<Service> { new Service { Name = "StaleResult" } });

            await executionTask;

            // Assert - The stale task shouldn't reset UI parameters because it no longer owns the current CTS reference window
            Assert.True(sut.IsBusy);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Never);

            // Clean up global mock allocations
            racingCts.Dispose();
            executionTokenSource.Dispose();
        }

        #endregion

        #region Resource Disposal & Race Conditions

        [Fact]
        public void Dispose_CalledMultipleTimes_ExecutesAtomicBlockOnce()
        {
            var sut = new TestServiceSearchViewModel(_cursorServiceMock.Object, _uiDispatcherMock.Object, _serviceCommandsMock.Object);
            using (var cts = new CancellationTokenSource())
            {
                sut.SetCancellationTokenSource(cts);

                sut.Dispose();

                // Second execution should return immediately through the Interlocked guard statement path
                sut.Dispose();

                Assert.Null(sut.GetCancellationTokenSource());
                Assert.True(cts.IsCancellationRequested);
            }
        }

        [Fact]
        public async Task SearchServicesAsync_TriggeredPostDisposal_AbortsImmediately()
        {
            var sut = new TestServiceSearchViewModel(_cursorServiceMock.Object, _uiDispatcherMock.Object, _serviceCommandsMock.Object);
            sut.Dispose();

            await sut.SearchCommand.ExecuteAsync(null);

            _serviceCommandsMock.Verify(s => s.SearchServicesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion
    }
}
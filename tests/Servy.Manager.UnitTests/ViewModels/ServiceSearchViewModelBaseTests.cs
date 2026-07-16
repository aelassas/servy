using Moq;
using Servy.Manager.Models;
using Servy.Manager.Resources;
using Servy.Manager.Services;
using Servy.Manager.ViewModels;
using Servy.UI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Manager.UnitTests.ViewModels
{
    public class ServiceSearchViewModelBaseTests : IDisposable
    {
        private readonly Mock<ICursorService> _cursorServiceMock;
        private readonly Mock<IUiDispatcher> _uiDispatcherMock;
        private readonly Mock<IServiceCommands> _serviceCommandsMock;
        private readonly TestServiceSearchViewModel _sut;

        public ServiceSearchViewModelBaseTests()
        {
            _cursorServiceMock = new Mock<ICursorService>();
            _uiDispatcherMock = new Mock<IUiDispatcher>();
            _serviceCommandsMock = new Mock<IServiceCommands>();

            // Ensure the dispatcher doesn't hang the async state machine during tests
            _uiDispatcherMock.Setup(d => d.YieldAsync()).Returns(Task.CompletedTask);

            // Initialize centralized SUT instance to establish a single cleanup vector
            _sut = new TestServiceSearchViewModel(_cursorServiceMock.Object, _uiDispatcherMock.Object, _serviceCommandsMock.Object);
        }

        public void Dispose()
        {
            // Centralized teardown guarantees everything including internal tokens get disposed cleanly
            _sut.Dispose();
        }

        #region Factory Stub Class

        /// <summary>
        /// Concrete test double to instantiate and test abstract base features safely.
        /// </summary>
        private class TestServiceSearchViewModel : ServiceSearchViewModelBase
        {
            public Func<Service, ServiceItemBase> CustomCreateServiceItem { get; set; }

            public TestServiceSearchViewModel(
                ICursorService cursorService,
                IUiDispatcher uiDispatcher,
                IServiceCommands serviceCommands)
                : base(cursorService, uiDispatcher, serviceCommands)
            {
            }

            protected override ServiceItemBase CreateServiceItem(Service service)
            {
                if (CustomCreateServiceItem != null)
                    return CustomCreateServiceItem(service);

                return new Mock<ServiceItemBase>().Object;
            }

            public CancellationTokenSource GetCancellationTokenSource() => _serviceSearchCts;
            public void SetCancellationTokenSource(CancellationTokenSource cts) => _serviceSearchCts = cts;
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_NullArguments_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new TestServiceSearchViewModel(null, _uiDispatcherMock.Object, _serviceCommandsMock.Object));

            Assert.Throws<ArgumentNullException>(() =>
                new TestServiceSearchViewModel(_cursorServiceMock.Object, null, _serviceCommandsMock.Object));

            Assert.Throws<ArgumentNullException>(() =>
                new TestServiceSearchViewModel(_cursorServiceMock.Object, _uiDispatcherMock.Object, null));
        }

        [Fact]
        public void Constructor_ValidArguments_InitializesPropertiesAndCommands()
        {
            // Arrange & Act & Assert
            Assert.NotNull(_sut.Services);
            Assert.Empty(_sut.Services);
            Assert.Equal(Strings.Button_Search, _sut.SearchButtonText);
            Assert.False(_sut.IsBusy);
            Assert.NotNull(_sut.SearchCommand);
        }

        #endregion

        #region Property Mutation & INotifyPropertyChanged Tests

        [Fact]
        public void Properties_SetNewValues_TriggersPropertyNotificationEvents()
        {
            // Arrange
            var changedProperties = new List<string>();
            _sut.PropertyChanged += (s, e) => { if (e.PropertyName != null) changedProperties.Add(e.PropertyName); };

            // Act
            _sut.SearchText = "Wexflow";
            _sut.SearchButtonText = "Searching Now...";
            _sut.IsBusy = true;

            // Assert
            Assert.Contains(nameof(_sut.SearchText), changedProperties);
            Assert.Contains(nameof(_sut.SearchButtonText), changedProperties);
            Assert.Contains(nameof(_sut.IsBusy), changedProperties);
        }

        #endregion

        #region Asynchronous Search Flows

        [Fact]
        public async Task SearchServicesAsync_SuccessfulExecution_PopulatesCollectionsAndResetsCursor()
        {
            // Arrange
            var mockRawServices = new List<Service> { new Service { Name = "ServyCore" } };

            _serviceCommandsMock
                .Setup(s => s.SearchServicesAsync("Servy", false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRawServices);

            var mockItem = new Mock<ServiceItemBase>().Object;
            _sut.CustomCreateServiceItem = (srv) => mockItem;
            _sut.SearchText = "Servy";

            // Act
            await _sut.SearchCommand.ExecuteAsync(null);

            // Assert
            Assert.Single(_sut.Services);
            Assert.Same(mockItem, _sut.Services.First());

            // State resets verified in finally lock
            Assert.False(_sut.IsBusy);
            Assert.Equal(Strings.Button_Search, _sut.SearchButtonText);

            _cursorServiceMock.Verify(c => c.SetWaitCursor(), Times.Once);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
            _uiDispatcherMock.Verify(d => d.YieldAsync(), Times.Once);
        }

        [Fact]
        public async Task SearchServicesAsync_NullServiceCommands_AbortsGracefully()
        {
            // Arrange
            _sut.ServiceCommands = null; // Simulate the dependency being cleared after construction

            // Act
            await _sut.SearchCommand.ExecuteAsync(null);

            // Assert
            _serviceCommandsMock.Verify(s => s.SearchServicesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
            _cursorServiceMock.Verify(c => c.SetWaitCursor(), Times.Never);
        }

        [Fact]
        public async Task SearchServicesAsync_OperationCancelledExceptionThrown_HandlesGracefully()
        {
            // Arrange
            _serviceCommandsMock
                .Setup(s => s.SearchServicesAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act
            var exception = await Record.ExceptionAsync(() => _sut.SearchCommand.ExecuteAsync(null));

            // Assert that target exception was caught and handled internally without blowing up the execution stack
            Assert.Null(exception);
            Assert.False(_sut.IsBusy);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        [Fact]
        public async Task SearchServicesAsync_GenericExceptionThrown_CleansUpState()
        {
            // Arrange
            _serviceCommandsMock
                .Setup(s => s.SearchServicesAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Database/SCM connection lost"));

            // Act
            await _sut.SearchCommand.ExecuteAsync(null);

            // Assert - Should reach finally block cleanly despite crash
            Assert.False(_sut.IsBusy);
            _cursorServiceMock.Verify(c => c.ResetCursor(), Times.Once);
        }

        [Fact]
        public async Task SearchServicesAsync_SupersededBeforeCompletion_DoesNotOverwriteUiState()
        {
            // Arrange
            var tcs = new TaskCompletionSource<List<Service>>();

            _serviceCommandsMock
                .Setup(s => s.SearchServicesAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
                .Returns(tcs.Task);

            var executionTask = _sut.SearchCommand.ExecuteAsync(null);
            var executionTokenSource = _sut.GetCancellationTokenSource();

            // Force a newer token source allocation behind the scenes to mimic a racing search execution
            var racingCts = new CancellationTokenSource();
            _sut.SetCancellationTokenSource(racingCts);

            // Force execution to proceed past the await boundary loop frame
            executionTokenSource.Cancel();
            tcs.SetResult(new List<Service> { new Service { Name = "StaleResult" } });

            // Act
            await executionTask;

            // Assert - The stale task shouldn't reset UI parameters because it no longer owns the current CTS reference window
            Assert.True(_sut.IsBusy);
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
            // Arrange
            using (var cts1 = new CancellationTokenSource())
            using (var cts2 = new CancellationTokenSource())
            {
                _sut.SetCancellationTokenSource(cts1);

                // Act
                // First execution passes through and triggers the main cancellation path cleanly
                _sut.Dispose();

                // Post-Condition Check for pass one
                Assert.True(cts1.IsCancellationRequested);
                Assert.Null(_sut.GetCancellationTokenSource());

                // Inject a fresh target payload. If the Interlocked guard is broken, this will get cleaned up too.
                _sut.SetCancellationTokenSource(cts2);

                // Second execution should return immediately through the Interlocked guard statement path
                _sut.Dispose();

                // Assert
                // Verifies that the atomic block short-circuited and never touched or cleared the second token instance
                Assert.NotNull(_sut.GetCancellationTokenSource());
                Assert.Same(cts2, _sut.GetCancellationTokenSource());
                Assert.False(cts2.IsCancellationRequested, "The second Dispose execution bypassed the Interlocked gate and modified the fresh token.");
            }
        }

        [Fact]
        public async Task SearchServicesAsync_TriggeredPostDisposal_AbortsImmediately()
        {
            // Arrange
            _sut.Dispose();

            // Act
            await _sut.SearchCommand.ExecuteAsync(null);

            // Assert
            _serviceCommandsMock.Verify(s => s.SearchServicesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion
    }
}
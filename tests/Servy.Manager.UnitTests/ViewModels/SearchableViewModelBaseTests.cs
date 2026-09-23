using Moq;
using Servy.Manager.Resources;
using Servy.Manager.ViewModels;
using Servy.UI.Services;

namespace Servy.Manager.UnitTests.ViewModels
{
    /// <summary>
    /// Covers the disposal race window in <see cref="SearchableViewModelBase.ExecuteSearchPipelineAsync"/>
    /// that opens between the atomic search-token swap and the recheck a few statements later.
    /// </summary>
    public class SearchableViewModelBaseTests
    {
        #region Test Class Implementation

        // The window under test is a handful of synchronous statements with no await in it, so a
        // single-threaded test can never land inside it and a loop racing Dispose against the
        // pipeline can only reach it by luck. OnSearchCtsSwapped is the production hook that makes
        // the interleaving deterministic: the override below parks the search thread exactly where
        // a racing Dispose would have to land, runs that Dispose on a second OS thread, and joins.
        private sealed class RacingSearchViewModel : SearchableViewModelBase
        {
            private readonly Action<RacingSearchViewModel> _duringCtsSwap;

            private int _fetchCallCount;

            public RacingSearchViewModel(ICursorService cursorService, Action<RacingSearchViewModel> duringCtsSwap)
                : base(cursorService)
            {
                _duringCtsSwap = duringCtsSwap;
            }

            public int FetchCallCount => Volatile.Read(ref _fetchCallCount);

            protected override void OnSearchCtsSwapped() => _duringCtsSwap(this);

            // Dispose(disposing: false) trips _isDisposed and stops short of ClearActiveSearchContext,
            // which is exactly the state a real Dispose is in between its first and second statements:
            // the flag is set and the freshly swapped-in token source is still alive.
            public void TripDisposedFlagOnly() => Dispose(disposing: false);

            public Task RunSearchAsync() =>
                ExecuteSearchPipelineAsync(_ =>
                {
                    Interlocked.Increment(ref _fetchCallCount);
                    return Task.FromResult(0);
                });

            // The wait state is set AFTER the token swap, so a Dispose racing the swap never sees it.
            // The pre-fetch yield hook is the point where the pipeline holds both a live token source
            // and the applied wait state, which is the only state in which ClearActiveSearchContext's
            // restore has anything to undo.
            public Task RunSearchDisposingAtPreFetchAsync() =>
                ExecuteSearchPipelineAsync(
                    _ =>
                    {
                        Interlocked.Increment(ref _fetchCallCount);
                        return Task.FromResult(0);
                    },
                    onPreFetchYieldAsync: () =>
                    {
                        RunOnSecondThread(Dispose);
                        return Task.CompletedTask;
                    });
        }

        private static void RunOnSecondThread(Action action)
        {
            var thread = new Thread(() => action());
            thread.Start();
            thread.Join();
        }

        #endregion

        [Fact]
        public async Task ExecuteSearchPipelineAsync_DisposalFlagSetDuringCtsSwap_ReturnsBeforeFetching()
        {
            // Arrange
            var cursorService = new Mock<ICursorService>();
            var viewModel = new RacingSearchViewModel(
                cursorService.Object,
                self => RunOnSecondThread(self.TripDisposedFlagOnly));

            // Act - the search thread is parked between the token swap and the recheck while a second
            // thread trips the disposal flag, so the recheck is the guard that has to stop the pipeline.
            await viewModel.RunSearchAsync();

            // Assert
            Assert.Equal(0, viewModel.FetchCallCount);
            Assert.False(viewModel.IsBusy);
            Assert.False(viewModel.HasSearched);
        }

        [Fact]
        public async Task ExecuteSearchPipelineAsync_TokenSourceDisposedDuringCtsSwap_SwallowsObjectDisposedException()
        {
            // Arrange
            var cursorService = new Mock<ICursorService>();
            var viewModel = new RacingSearchViewModel(
                cursorService.Object,
                self => RunOnSecondThread(self.Dispose));

            // Act - a full Dispose on the second thread also disposes the token source the pipeline
            // just swapped in, so reading its Token throws and only the catch can absorb it. The
            // absence of an escaping exception here is the assertion; awaiting an unhandled
            // ObjectDisposedException would fail the test on the line above.
            await viewModel.RunSearchAsync();

            // Assert
            Assert.Equal(0, viewModel.FetchCallCount);
            Assert.False(viewModel.IsBusy);
            Assert.False(viewModel.HasSearched);
        }

        [Fact]
        public async Task Dispose_WhileSearchHoldsTheWaitState_RestoresTheIdleSearchUiState()
        {
            // Arrange
            // Disposing from the pre-fetch yield hook nulls the active token source, so the pipeline's
            // own Step 7 gate no longer matches and does not restore. ClearActiveSearchContext is then
            // the only path that can undo the wait state, which is what this asserts.
            var cursorService = new Mock<ICursorService>();
            var viewModel = new RacingSearchViewModel(cursorService.Object, _ => { });

            // Act
            await viewModel.RunSearchDisposingAtPreFetchAsync();

            // Assert
            Assert.False(viewModel.IsBusy);
            Assert.Equal(Strings.Button_Search, viewModel.SearchButtonText);
            cursorService.Verify(c => c.SetWaitCursor(), Times.Once);
            cursorService.Verify(c => c.ResetCursor(), Times.Once);
        }
    }
}

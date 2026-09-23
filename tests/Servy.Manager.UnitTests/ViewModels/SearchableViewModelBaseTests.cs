using Moq;
using Servy.Manager.ViewModels;
using Servy.UI.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

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
    }
}

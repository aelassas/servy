using System;

namespace Servy.UI.IntegrationTests
{
    /// <summary>
    /// Manages process-global <see cref="UiHeadless"/> state for the UI test collection.
    /// Ensures <see cref="UiHeadless.IsEnabled"/> is enabled during test execution
    /// and safely restored to <c>false</c> upon collection teardown.
    /// </summary>
    public sealed class UiHeadlessFixture : IDisposable
    {
        public UiHeadlessFixture()
        {
            // Enable process-global headless UI execution mode for the test suite
            UiHeadless.IsEnabled = true;
        }

        public void Dispose()
        {
            // Restore process-global headless state upon collection completion to prevent cross-test leakage
            UiHeadless.IsEnabled = false;
        }
    }
}

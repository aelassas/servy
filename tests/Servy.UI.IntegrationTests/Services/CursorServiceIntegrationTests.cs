using Servy.Testing;
using Servy.UI.Services;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Xunit;

namespace Servy.UI.IntegrationTests.Services
{
    [Collection("UiSta")]
    public class CursorServiceIntegrationTests
    {
        private readonly CursorService _service;

        public CursorServiceIntegrationTests()
        {
            _service = new CursorService();
        }

        #region Branch: Headless / Null Dispatcher

        [Fact]
        public void SetWaitCursorAndResetCursor_WhenApplicationIsNull_DoNotThrow()
        {
            // Branch: if (Application.Current?.Dispatcher == null) return;
            // This is the default state in standard xUnit runners because Application.Current is null.

            // Reset the process-global Application instance using TestReflection to ensure branch isolation
            TestReflection.SetFieldStatic(typeof(System.Windows.Application), "_appInstance", null);

            // Act
            var exception = Record.Exception(() =>
            {
                _service.SetWaitCursor();
                _service.ResetCursor();
            });

            // Assert
            Assert.Null(exception);
            // Coverage: This confirms the guard clause effectively prevented an 
            // ObjectDisposedException or NullReferenceException when the WPF context is missing.
        }

        #endregion

        #region Branch: Background Thread (Dispatcher.CheckAccess == false)

        [Fact(Skip = "Skipped due to persistent background thread deadlocks on headless CI environments.")]
        public async Task ResetCursor_FromBackgroundThread_InvokesOnDispatcher()
        {
            await Helper.RunOnSTA(async () =>
            {
                // Arrange
                Helper.EnsureApplication();
                Mouse.OverrideCursor = Cursors.Hand;

                // Act
                // The service should detect we are on a background thread 
                // and use Dispatcher.InvokeAsync
                await Task.Run(() =>
                {
                    _service.ResetCursor();
                });

                // Force the Dispatcher to process the Reset operation
                // This flushes the queue up to 'Background' priority
                int retries = 0, maxRetries = 10;
                while (Mouse.OverrideCursor != null && retries < maxRetries)
                {
                    await Dispatcher.Yield(DispatcherPriority.Background);
                    await Task.Delay(100); // Small delay to allow the UI thread to process
                    retries++;
                }
                // Assert
                // Verification: Since we are back on the STA thread after the await,
                // we can check the cursor state immediately.
                Assert.Null(Mouse.OverrideCursor);
            });
        }

        #endregion
    }
}
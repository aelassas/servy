using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Servy.Manager.UnitTests
{
    public static class Helper
    {
        private static readonly object _lock = new object();
        private static bool _applicationCreated;

        private static void EnsureApplication()
        {
            lock (_lock)
            {
                if (_applicationCreated)
                    return;

                if (Application.Current == null)
                {
                    new App
                    {
                        ShutdownMode = ShutdownMode.OnExplicitShutdown
                    };
                }

                _applicationCreated = true;
            }
        }

        public static void RunOnSTA(Action action, bool createApp = false)
        {
            Exception exception = null;

            Thread thread = new Thread(() =>
            {
                try
                {
                    if (createApp)
                        EnsureApplication();

                    action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (exception != null)
                throw exception;
        }

        public static T RunOnSTA<T>(Func<T> func, bool createApp = false)
        {
            T result = default;
            Exception exception = null;

            Thread thread = new Thread(() =>
            {
                try
                {
                    if (createApp)
                        EnsureApplication();

                    result = func();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (exception != null)
                throw exception;

            return result;
        }

        public static async Task RunOnSTA(Func<Task> action)
        {
            var tcs = new TaskCompletionSource<bool>();
            var thread = new Thread(() =>
            {
                try
                {
                    // Initialize the Dispatcher for the STA thread
                    var dispatcher = Dispatcher.CurrentDispatcher;

                    // Queue the test execution onto the STA thread's dispatcher.
                    // This guarantees that Dispatcher.CurrentDispatcher inside the test 
                    // resolves to THIS dispatcher, which has an active message pump.
                    dispatcher.InvokeAsync(async () =>
                    {
                        try
                        {
                            await action();
                            tcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            tcs.SetException(ex);
                        }
                        finally
                        {
                            // Shut down the dispatcher loop to exit the thread cleanly
                            dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
                        }
                    });

                    // Start the message pump. It will process the InvokeAsync above.
                    Dispatcher.Run();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            // Wait for the test (and the STA thread lifecycle) to complete
            await tcs.Task;
        }
    }
}

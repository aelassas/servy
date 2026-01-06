using System;
using System.Threading;
using System.Windows;

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
    }
}

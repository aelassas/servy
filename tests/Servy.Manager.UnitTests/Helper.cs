using System.Windows;

namespace Servy.Manager.UnitTests
{
    public static class Helper
    {
        public static void RunOnSTA(Action action)
        {
            Exception? ex = null;

            var thread = new Thread(() =>
            {
                try
                {
                    if (Application.Current == null)
                        new Application(); // create only once per thread

                    action();
                }
                catch (Exception e)
                {
                    ex = e;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (ex != null) throw ex;
        }

        public static T RunOnSTA<T>(Func<T> func, bool createApp = false)
        {
            T? result = default;
            Exception? ex = null;

            var thread = new Thread(() =>
            {
                try
                {
                    if (createApp && Application.Current == null)
                        new App();

                    result = func();
                }
                catch (Exception e)
                {
                    ex = e;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (ex != null) throw ex;
            return result!;
        }

    }
}

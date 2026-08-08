using System.Diagnostics;
using System.Reflection;

namespace Servy.Service.UnitTests.Helpers
{
    public static class DataReceivedEventArgsFactory
    {
        /// <summary>
        /// Uses reflection to invoke the non-public DataReceivedEventArgs(string) constructor,
        /// which is the only way to synthesize these args in a test.
        /// </summary>
        /// <param name="data">
        /// The line payload. Pass <see langword="null"/> to reproduce the end-of-stream sentinel
        /// that <see cref="System.Diagnostics.Process"/> raises when redirected output closes.
        /// </param>
        /// <returns>A <see cref="DataReceivedEventArgs"/> whose <c>Data</c> is <paramref name="data"/>.</returns>
        public static DataReceivedEventArgs CreateDataReceivedEventArgs(string? data)
        {
            var constructor = typeof(DataReceivedEventArgs).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null)
                ?? throw new InvalidOperationException(
                    "Internal DataReceivedEventArgs(string) constructor not found; the runtime layout may have changed.");

            return (DataReceivedEventArgs)constructor.Invoke(new object?[] { data });
        }
    }
}

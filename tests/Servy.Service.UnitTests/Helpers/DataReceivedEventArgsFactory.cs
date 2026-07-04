using System.Diagnostics;
using System.Reflection;

namespace Servy.Service.UnitTests.Helpers
{
    public static class DataReceivedEventArgsFactory
    {
        /// <summary>
        /// Uses reflection to instantiate the internal DataReceivedEventArgs class for testing streams.
        /// </summary>
        /// <param name="data">The data to be passed to the DataReceivedEventArgs constructor.</param>
        public static DataReceivedEventArgs CreateDataReceivedEventArgs(string data)
        {
            var constructor = typeof(DataReceivedEventArgs).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault();
            return (DataReceivedEventArgs)constructor?.Invoke(new object[] { data })!;
        }
    }
}

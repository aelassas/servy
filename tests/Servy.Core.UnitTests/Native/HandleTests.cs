using Servy.Core.Native;
using Servy.Testing;
using System;
using System.Diagnostics;
using Xunit;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.UnitTests.Native
{
    public class HandleTests
    {
        [Fact]
        public void Handle_Dispose_ShouldBeIdempotent()
        {
            // Arrange
            int currentPid = GetCurrentProcessId();
            SafeWinProcessHandle handle = OpenProcess(ProcessAccess.QueryLimitedInformation, false, currentPid);

            // Act
            handle.Dispose();

            // Assert
            Assert.True(handle.IsClosed, "The handle should be marked as closed after the first Dispose call.");

            // Act & Assert
            // Calling Dispose again should NOT throw an exception.
            // This verifies the SafeHandle internal state protection against double-closing.
            var exception = Record.Exception(() => handle.Dispose());
            Assert.Null(exception);
        }

        private int GetCurrentProcessId()
        {
            using (var process = Process.GetCurrentProcess())
            {
                return process.Id;
            }
        }
    }
}

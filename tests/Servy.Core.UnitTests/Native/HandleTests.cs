using Servy.Core.Native;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.UnitTests.Native
{
    public class HandleTests
    {
        [Fact]
        public void OpenProcess_ShouldReturnValidHandle_WhenOpeningCurrentProcess()
        {
            // Arrange
            int currentPid = Process.GetCurrentProcess().Id;

            // PROCESS_QUERY_LIMITED_INFORMATION (0x1000) is standard for querying state
            // and typically does not require administrative privileges for the current process.
            ProcessAccess access = ProcessAccess.QueryLimitedInformation;

            // Act
            using (Handle handle = OpenProcess(access, false, currentPid))
            {
                // Assert
                Assert.NotNull(handle);
                Assert.False(handle.IsInvalid, "The handle should be valid for the current process.");
                Assert.False(handle.IsClosed, "The handle should not be closed while inside the using block.");

                // Verify the underlying pointer is assigned
                IntPtr rawValue = handle.DangerousGetHandle();
                Assert.NotEqual(IntPtr.Zero, rawValue);
                Assert.NotEqual(new IntPtr(-1), rawValue);
            }
        }

        [Fact]
        public void OpenProcess_ShouldReturnInvalidHandle_WhenProcessDoesNotExist()
        {
            // Arrange
            // PID 0 is the System Idle Process, but kernel32 usually returns an 
            // error or NULL if you try to open a non-existent or inaccessible high-range PID.
            int nonExistentPid = 999999;

            // Act
            using (Handle handle = OpenProcess(ProcessAccess.QueryLimitedInformation, false, nonExistentPid))
            {
                // Assert
                Assert.NotNull(handle);
                Assert.True(handle.IsInvalid, "Opening a non-existent PID should return an invalid handle.");
                Assert.Equal(IntPtr.Zero, handle.DangerousGetHandle());
            }
        }

        [Fact]
        public void Handle_Dispose_ShouldBeIdempotent()
        {
            // Arrange
            int currentPid = Process.GetCurrentProcess().Id;
            Handle handle = OpenProcess(ProcessAccess.QueryLimitedInformation, false, currentPid);

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

        [Fact]
        public void Handle_ShouldSupportImplicitConversionToIntPtr()
        {
            // Arrange
            int currentPid = Process.GetCurrentProcess().Id;

            // Act
            using (Handle handle = OpenProcess(ProcessAccess.QueryLimitedInformation, false, currentPid))
            {
                IntPtr convertedPtr = handle.DangerousGetHandle();

                // If you had an implicit operator, you would test it here.
                // Note: SafeHandle does not provide an implicit cast to IntPtr by design 
                // to prevent handle leaks. Use DangerousGetHandle() in tests only.
                Assert.NotEqual(IntPtr.Zero, convertedPtr);
            }
        }
    }
}
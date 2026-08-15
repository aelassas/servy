using Microsoft.Win32.SafeHandles;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Core.Native
{
    /// <summary>
    /// Represents a safe wrapper around a Windows process handle.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class SafeWinProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SafeWinProcessHandle"/> class for P/Invoke marshalling or manual allocation.
        /// </summary>
        public SafeWinProcessHandle() : base(ownsHandle: true) { }

        /// <summary>
        /// Returns the underlying handle value, or <see cref="IntPtr.Zero"/> if the handle is closed or invalid.
        /// </summary>
        public IntPtr GetHandleOrZero()
        {
            return IsClosed || IsInvalid ? IntPtr.Zero : base.DangerousGetHandle();
        }

        /// <summary>
        /// Executes the code required to free the native handle using <c>CloseHandle</c>.
        /// </summary>
        /// <returns><see langword="true"/> if the handle is released successfully; otherwise, <see langword="false"/>.</returns>
        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }
}

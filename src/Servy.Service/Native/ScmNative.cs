using Servy.Core.Native;
using System;
using System.Runtime.InteropServices;

namespace Servy.Service.Native
{
    /// <summary>
    /// Production implementation of <see cref="IScmNative"/>.
    /// </summary>
    /// <remarks>
    /// Every member is a one-line forward to the call it replaced in
    /// <see cref="Servy.Service.Service"/>: same target, same arguments, same order, same thread and
    /// the same treatment of the return value. The type holds no state, so an instance can be shared.
    /// </remarks>
    internal sealed class ScmNative : IScmNative
    {
        /// <summary>
        /// Calls <c>FreeConsole</c> and then <c>SetConsoleCtrlHandler(IntPtr.Zero, true)</c>,
        /// discarding both return values.
        /// </summary>
        public void DetachConsole()
        {
            _ = NativeMethods.FreeConsole();
            _ = NativeMethods.SetConsoleCtrlHandler(IntPtr.Zero, true);
        }

        /// <summary>
        /// Calls the Win32 <c>SetServiceStatus</c> API.
        /// </summary>
        /// <param name="handle">The native service status handle to report against.</param>
        /// <param name="status">The status structure to publish.</param>
        /// <returns><see langword="true"/> when the call succeeded; otherwise <see langword="false"/>.</returns>
        public bool SetServiceStatus(IntPtr handle, ref NativeMethods.SERVICE_STATUS status)
        {
            return NativeMethods.SetServiceStatus(handle, ref status);
        }

        /// <summary>
        /// Returns <see cref="Marshal.GetLastWin32Error"/>.
        /// </summary>
        /// <returns>The Win32 error code the last native call on this thread set.</returns>
        public int GetLastWin32Error()
        {
            return Marshal.GetLastWin32Error();
        }
    }
}

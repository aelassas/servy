using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Core.Native
{
    /// <summary>
    /// Represents a safe wrapper around a Windows process handle.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class SafeWinProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeWinProcessHandle() : base(ownsHandle: true) { }

        /// <summary>
        /// Returns the underlying handle value, or <see cref="IntPtr.Zero"/> if the handle is closed or invalid.
        /// </summary>
        public IntPtr GetHandleOrZero()
        {
            return IsClosed || IsInvalid ? IntPtr.Zero : base.DangerousGetHandle();
        }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }
}
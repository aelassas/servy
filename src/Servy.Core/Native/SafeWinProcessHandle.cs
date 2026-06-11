using Microsoft.Win32.SafeHandles;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Core.Native
{
    /// <summary>
    /// Represents a safe wrapper around a Windows process handle.
    /// </summary>
    /// <remarks>
    /// Deriving from <see cref="SafeHandleZeroOrMinusOneIsInvalid"/> ensures the handle 
    /// is closed exactly once, even if the object is finalized or disposed multiple times.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public sealed class SafeWinProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeWinProcessHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }
}
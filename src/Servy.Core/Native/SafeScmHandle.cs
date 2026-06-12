using Microsoft.Win32.SafeHandles;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Core.Native
{
    /// <summary>
    /// Represents a safe wrapper around a Service Control Manager (SCM) database handle.
    /// </summary>
    /// <remarks>
    /// Deriving from <see cref="SafeHandleZeroOrMinusOneIsInvalid"/> ensures the unmanaged handle 
    /// is closed exactly once via <c>CloseServiceHandle</c>, even if the object is finalized or disposed multiple times.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public sealed class SafeScmHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeScmHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseServiceHandle(handle);
        }
    }
}
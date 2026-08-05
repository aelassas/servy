using Microsoft.Win32.SafeHandles;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Core.Native
{
    /// <summary>
    /// Serves as an abstract base for safe handles released through <c>CloseServiceHandle</c>.
    /// </summary>
    /// <remarks>
    /// Deriving from <see cref="SafeHandleZeroOrMinusOneIsInvalid"/> ensures the unmanaged handle
    /// is closed exactly once via <c>CloseServiceHandle</c>, even if the object is finalized or disposed multiple times.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public abstract class SafeCloseServiceHandleBase : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SafeCloseServiceHandleBase"/> class,
        /// specifying that the handle is to be reliably released.
        /// </summary>
        protected SafeCloseServiceHandleBase() : base(ownsHandle: true) { }

        /// <summary>
        /// Executes the code required to free the native handle using <c>CloseServiceHandle</c>.
        /// </summary>
        /// <returns><see langword="true"/> if the handle is released successfully; otherwise, <see langword="false"/>.</returns>
        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseServiceHandle(handle);
        }
    }
}

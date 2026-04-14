using Microsoft.Win32.SafeHandles;
using System.Runtime.ConstrainedExecution;

namespace Servy.Core.Native
{
    /// <summary>
    /// Represents a safe wrapper around a Windows process handle.
    /// </summary>
    /// <remarks>
    /// Deriving from <see cref="SafeHandleZeroOrMinusOneIsInvalid"/> ensures the handle 
    /// is closed exactly once, even if the object is finalized or disposed multiple times.
    /// </remarks>
    public sealed class Handle : SafeHandleZeroOrMinusOneIsInvalid
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Handle"/> class.
        /// </summary>
        public Handle() : base(true)
        {
        }

        /// <summary>
        /// Executes the code required to free the handle.
        /// </summary>
        /// <summary>
        /// Executes the native code required to free the handle.
        /// </summary>
        /// <returns>True if the handle is released successfully; otherwise, in the event of a catastrophic failure, false.</returns>
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }
}
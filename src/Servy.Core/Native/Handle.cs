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
    public sealed class Handle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public Handle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }

    /// <summary>
    /// Represents a safe wrapper around a Service Control Manager (SCM) database handle.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class SafeScmHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeScmHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseServiceHandle(handle);
        }
    }

    /// <summary>
    /// Represents a safe wrapper around a Windows Service handle.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class SafeServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeServiceHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseServiceHandle(handle);
        }
    }

    /// <summary>
    /// Represents a safe wrapper around a Job Object handle.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class SafeJobObjectHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeJobObjectHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }
}
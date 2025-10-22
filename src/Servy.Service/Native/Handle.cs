using System;
using System.Runtime.InteropServices;
using static Servy.Service.Native.NativeMethods;

namespace Servy.Service.Native
{
    /// <summary>
    /// Represents a safe wrapper around a native handle, ensuring it is properly released when disposed.
    /// </summary>
    /// <remarks>
    /// This struct encapsulates an unmanaged handle (<see cref="IntPtr"/>) and provides deterministic cleanup
    /// through the <see cref="IDisposable"/> pattern. It calls the native <c>CloseHandle</c> function
    /// defined in <see cref="NativeMethods"/> when disposed.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Handle : IDisposable
    {
        /// <summary>
        /// The underlying native handle.
        /// </summary>
        private readonly IntPtr handle;

        /// <summary>
        /// Initializes a new instance of the <see cref="Handle"/> struct with the specified native handle.
        /// </summary>
        /// <param name="handle">The unmanaged handle to wrap.</param>
        internal Handle(IntPtr handle) => this.handle = handle;

        /// <summary>
        /// Releases the underlying handle by invoking the native <c>CloseHandle</c> function.
        /// </summary>
        public void Dispose() => CloseHandle(this.handle);

        /// <summary>
        /// Implicitly converts the <see cref="Handle"/> to an <see cref="IntPtr"/>.
        /// </summary>
        /// <param name="value">The <see cref="Handle"/> instance to convert.</param>
        /// <returns>The underlying <see cref="IntPtr"/> value.</returns>
        public static implicit operator IntPtr(Handle value) => value.handle;
    }
}
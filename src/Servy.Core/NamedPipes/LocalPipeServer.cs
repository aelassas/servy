using Servy.Core.Native;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace Servy.Core.NamedPipes
{
    /// <summary>
    /// Creates server instances of a named pipe that only clients on this computer can open.
    /// </summary>
    public static class LocalPipeServer
    {
        private const uint PipeAccessDuplex = 0x00000003;
        private const uint WriteDac = 0x00040000;
        private const uint FileFlagOverlapped = 0x40000000;
        private const uint FileFlagFirstPipeInstance = 0x00080000;
        private const uint PipeTypeByte = 0x00000000;
        private const uint PipeRejectRemoteClients = 0x00000008;
        private const uint PipeUnlimitedInstances = 255;

        /// <summary>
        /// Creates an asynchronous, byte-mode, duplex server instance with <c>PIPE_REJECT_REMOTE_CLIENTS</c>, so a client
        /// on another computer (over SMB) cannot even open it, and applies <paramref name="security"/> to the pipe itself;
        /// when <paramref name="firstInstance"/> is <see langword="true"/>, the instance must be the first of its name.
        /// </summary>
        /// <param name="pipeName">The pipe name, without the <c>\\.\pipe\</c> prefix.</param>
        /// <param name="security">The DACL of the pipe.</param>
        /// <param name="firstInstance">
        /// <see langword="true"/> to create the instance with <c>FILE_FLAG_FIRST_PIPE_INSTANCE</c>, so the call fails with
        /// <c>ERROR_ACCESS_DENIED</c> when an instance of the name already exists (another process holds the name, or a
        /// client still holds an instance of a previous server); <see langword="false"/> (the default) to create it whether
        /// or not the name already exists.
        /// </param>
        /// <returns>The server instance, not yet connected.</returns>
        /// <remarks>
        /// Every instance of a pipe name shares one security descriptor, set by the first instance; the descriptor
        /// passed for any later instance is ignored. Writing the DACL through the new instance's handle (it is opened with
        /// WRITE_DAC) replaces the shared descriptor, so a rebuilt DACL takes effect without restarting the server.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="pipeName"/> or <paramref name="security"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="Win32Exception">
        /// Thrown when the instance cannot be created; its <see cref="Win32Exception.NativeErrorCode"/> is
        /// <see cref="Errors.ERROR_ACCESS_DENIED"/> when <paramref name="firstInstance"/> is <see langword="true"/> and the
        /// name already exists.
        /// </exception>
        [ExcludeFromCodeCoverage]
        public static NamedPipeServerStream Create(string pipeName, PipeSecurity security, bool firstInstance = false)
        {
            if (pipeName == null) throw new ArgumentNullException(nameof(pipeName));
            if (security == null) throw new ArgumentNullException(nameof(security));

            var descriptor = security.GetSecurityDescriptorBinaryForm();
            var pinned = GCHandle.Alloc(descriptor, GCHandleType.Pinned);
            Microsoft.Win32.SafeHandles.SafePipeHandle handle;
            try
            {
                var attributes = new NativeMethods.PipeSecurityAttributes
                {
                    Length = Marshal.SizeOf(typeof(NativeMethods.PipeSecurityAttributes)),
                    SecurityDescriptor = pinned.AddrOfPinnedObject(),
                    InheritHandle = 0,
                };

                handle = NativeMethods.CreateNamedPipe(
                    @"\\.\pipe\" + pipeName,
                    PipeAccessDuplex | WriteDac | FileFlagOverlapped | (firstInstance ? FileFlagFirstPipeInstance : 0),
                    PipeTypeByte | PipeRejectRemoteClients,
                    PipeUnlimitedInstances,
                    0,
                    0,
                    0,
                    ref attributes);
            }
            finally
            {
                pinned.Free();
            }

            if (handle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error, $"Failed to create an instance of the named pipe '{pipeName}'. Win32 error: {error}");
            }

            var stream = new NamedPipeServerStream(PipeDirection.InOut, isAsync: true, isConnected: false, handle);
            try
            {
                stream.SetAccessControl(security);
                return stream;
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }
    }
}

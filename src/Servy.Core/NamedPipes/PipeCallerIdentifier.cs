using Servy.Core.Logging;
using Servy.Core.Native;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Security.Principal;

namespace Servy.Core.NamedPipes
{
    /// <summary>
    /// Identifies a pipe client by its process identifier, pinned at connect time by a held process handle, and, through an
    /// identification-level impersonation of the client, by whether its token is Local System or an elevated member of
    /// Builtin Administrators.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class PipeCallerIdentifier : IPipeCallerIdentifier
    {
        private static readonly SecurityIdentifier LocalSystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

        /// <summary>
        /// Pins the client of a server pipe right after the connection completed, before anything is read from it: reads
        /// the client's process identifier and opens a query handle to that process, which keeps the identifier from being
        /// given to another process while the pin is held.
        /// </summary>
        /// <param name="connectedServer">The connected server end of the pipe.</param>
        /// <returns>
        /// The pinned client; its process identifier is 0 when it could not be read or the process could not be opened,
        /// because an identifier no handle holds could already belong to another process. Never <see langword="null"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="connectedServer"/> is <see langword="null"/>.</exception>
        public PipeClientPin Pin(NamedPipeServerStream connectedServer)
        {
            if (connectedServer == null) throw new ArgumentNullException(nameof(connectedServer));

            if (!PipePeer.TryGetClientProcessId(connectedServer, out var processId))
                return new PipeClientPin(0, null);

            SafeWinProcessHandle process = null;
            try
            {
                process = NativeMethods.OpenProcess(NativeMethods.ProcessAccess.QueryLimitedInformation, false, processId);
            }
            catch (Exception ex)
            {
                Logger.Debug($"The pipe client process {processId} could not be opened: {ex.Message}");
            }

            if (process == null || process.IsInvalid)
            {
                process?.Dispose();
                Logger.Debug($"The pipe client process {processId} could not be pinned; it is not identified by its process identifier.");
                return new PipeClientPin(0, null);
            }

            return new PipeClientPin(processId, process);
        }

        /// <summary>
        /// Identifies the client of a connected server pipe by the process identifier pinned at connect time, by whether it
        /// is on another computer, and by whether its token is Local System or an elevated administrator.
        /// </summary>
        /// <param name="connectedServer">The connected server end of the pipe; data must have been read from it.</param>
        /// <param name="pin">The client as pinned by <see cref="Pin"/>; its process identifier is the one identified.</param>
        /// <returns>The client's pinned process identifier and whether it is an administrator; never <see langword="null"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="connectedServer"/> or <paramref name="pin"/> is <see langword="null"/>.</exception>
        public PipeCaller Identify(NamedPipeServerStream connectedServer, PipeClientPin pin)
        {
            if (connectedServer == null) throw new ArgumentNullException(nameof(connectedServer));
            if (pin == null) throw new ArgumentNullException(nameof(pin));

            var isRemote = PipePeer.IsRemoteClient(connectedServer, out _);
            return new PipeCaller(pin.ProcessId, IsAdministrator(connectedServer), isRemote);
        }

        /// <summary>
        /// Determines whether the client's token is Local System or holds an enabled Builtin Administrators group, i.e.
        /// the client runs elevated. A client that did not allow at least identification is not an administrator.
        /// </summary>
        /// <param name="connectedServer">The connected server end of the pipe.</param>
        /// <returns><see langword="true"/> for Local System and elevated administrators.</returns>
        private static bool IsAdministrator(NamedPipeServerStream connectedServer)
        {
            bool isAdministrator = false;
            try
            {
                connectedServer.RunAsClient(() =>
                {
                    using (var identity = WindowsIdentity.GetCurrent(ifImpersonating: true))
                    {
                        if (identity == null)
                            return;

                        isAdministrator = (identity.User != null && identity.User.Equals(LocalSystemSid))
                            || new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Debug($"The pipe client could not be identified as an administrator: {ex.Message}");
                return false;
            }

            return isAdministrator;
        }
    }
}

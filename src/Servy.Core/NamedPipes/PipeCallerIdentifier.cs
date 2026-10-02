using Servy.Core.Logging;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Security.Principal;

namespace Servy.Core.NamedPipes
{
    /// <summary>
    /// Identifies a pipe client by its process identifier and, through an identification-level impersonation of the
    /// client, by whether its token is Local System or an elevated member of Builtin Administrators.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class PipeCallerIdentifier : IPipeCallerIdentifier
    {
        private static readonly SecurityIdentifier LocalSystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

        /// <inheritdoc />
        public PipeCaller Identify(NamedPipeServerStream connectedServer)
        {
            if (connectedServer == null) throw new ArgumentNullException(nameof(connectedServer));

            PipePeer.TryGetClientProcessId(connectedServer, out var processId);
            return new PipeCaller(processId, IsAdministrator(connectedServer));
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

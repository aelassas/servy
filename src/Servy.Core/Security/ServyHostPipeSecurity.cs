using Servy.Core.Logging;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Servy.Core.Security
{
    /// <summary>
    /// Builds the DACL of the Servy host named pipe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Local System and Builtin Administrators get Full Control. Every service account that runs a Servy service gets
    /// Read, Write and Synchronize, which is what a client needs to connect, send a request and read the answer, and
    /// nothing else: in particular not <see cref="PipeAccessRights.CreateNewInstance"/>, so a service account can never
    /// create a server instance of the pipe and answer another service's requests. Network logons are allowed: a local
    /// process whose token carries <c>NT AUTHORITY\NETWORK</c> (for example the CLI in a PowerShell remoting session) is
    /// checked like any other. The pipe stays local only: its instances are created with <c>PIPE_REJECT_REMOTE_CLIENTS</c>
    /// (<see cref="NamedPipes.LocalPipeServer"/>), so a client on another computer cannot open it.
    /// </para>
    /// <para>
    /// Being able to connect is not being allowed to read anything: the host still answers a request about a service only
    /// to the process that service runs in, or to an elevated administrator.
    /// </para>
    /// </remarks>
    public static class ServyHostPipeSecurity
    {
        /// <summary>
        /// The rights a service account gets on the pipe.
        /// </summary>
        public const PipeAccessRights ClientRights = PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize;

        private static readonly SecurityIdentifier LocalSystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        private static readonly SecurityIdentifier AdministratorsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

        /// <summary>
        /// Builds the pipe's security for the accounts the installed services run under.
        /// </summary>
        /// <param name="serviceAccounts">The service accounts; Local System aliases and blank entries are ignored.</param>
        /// <param name="resolveAccount">Resolves an account name to its SID; <see langword="null"/> when it cannot.</param>
        /// <returns>The security descriptor to create the pipe's server instances with.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="resolveAccount"/> is <see langword="null"/>.</exception>
        public static PipeSecurity Create(IEnumerable<string>? serviceAccounts, Func<string, SecurityIdentifier?> resolveAccount)
        {
            if (resolveAccount == null) throw new ArgumentNullException(nameof(resolveAccount));

            return Create(ResolveGrantees(serviceAccounts, resolveAccount));
        }

        /// <summary>
        /// Builds the pipe's security for the given SIDs.
        /// </summary>
        /// <param name="grantees">The SIDs that may connect as clients.</param>
        /// <returns>The security descriptor to create the pipe's server instances with.</returns>
        public static PipeSecurity Create(IEnumerable<SecurityIdentifier>? grantees)
        {
            var security = new PipeSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            // No explicit owner: the creating process becomes the owner (Local System in production). Naming an owner
            // the creator does not hold fails CreateNamedPipe with ERROR_INVALID_OWNER, for example in an elevated test.

            security.AddAccessRule(new PipeAccessRule(LocalSystemSid, PipeAccessRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(AdministratorsSid, PipeAccessRights.FullControl, AccessControlType.Allow));

            foreach (var sid in grantees ?? Enumerable.Empty<SecurityIdentifier>())
            {
                if (sid == null || sid.Equals(LocalSystemSid) || sid.Equals(AdministratorsSid) || IsBroadGroup(sid))
                    continue;

                security.AddAccessRule(new PipeAccessRule(sid, ClientRights, AccessControlType.Allow));
            }

            return security;
        }

        /// <summary>
        /// Resolves the service accounts to the distinct SIDs that are granted client access.
        /// </summary>
        /// <param name="serviceAccounts">The service accounts.</param>
        /// <param name="resolveAccount">Resolves an account name to its SID.</param>
        /// <returns>The distinct SIDs, without Local System, Administrators and the broad groups.</returns>
        public static IReadOnlyList<SecurityIdentifier> ResolveGrantees(IEnumerable<string>? serviceAccounts, Func<string, SecurityIdentifier?> resolveAccount)
        {
            if (resolveAccount == null) throw new ArgumentNullException(nameof(resolveAccount));

            var result = new List<SecurityIdentifier>();
            foreach (var account in serviceAccounts ?? Enumerable.Empty<string>())
            {
                if (!ServyExePermissionsHardener.IsHardeningCandidate(account))
                    continue;

                var sid = resolveAccount(account.Trim());
                if (sid == null)
                {
                    Logger.Warn($"The account '{account}' could not be resolved; it is not granted access to the Servy host named pipe.");
                    continue;
                }

                if (IsBroadGroup(sid))
                {
                    Logger.Warn($"The account '{account}' is a broad group (Everyone, Users or Authenticated Users); it is not granted access to the Servy host named pipe.");
                    continue;
                }

                if (sid.Equals(LocalSystemSid) || sid.Equals(AdministratorsSid) || result.Contains(sid))
                    continue;

                result.Add(sid);
            }

            return result;
        }

        /// <summary>
        /// Determines whether a SID is Everyone, Users or Authenticated Users.
        /// </summary>
        /// <param name="sid">The SID to test.</param>
        /// <returns><see langword="true"/> for one of the broad groups.</returns>
        private static bool IsBroadGroup(SecurityIdentifier sid)
            => SecurityHelper.BroadUnprivilegedSids.Any(broad => broad.Equals(sid));
    }
}

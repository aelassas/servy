using Servy.Core.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Core.Security
{
    /// <summary>
    /// Hardens the permissions Servy's vault (<c>%ProgramData%\Servy</c>), binaries, settings files and logs grant to a
    /// service account, and keeps it out of the database, the encryption keys and the administrative logs.
    /// </summary>
    public interface IServyExePermissionsHardener
    {
        /// <summary>
        /// Grants <paramref name="targetAccount"/> the least privilege a Servy service needs in the vault and locks
        /// Servy's files down for it.
        /// </summary>
        /// <param name="targetAccount">The account the service runs under (e.g. <c>DOMAIN\svc-servy</c>, <c>.\user</c>, <c>DOMAIN\gMSA$</c>).</param>
        /// <param name="cancellationToken">A token that stops the hardening between two files.</param>
        /// <returns>
        /// <see langword="false"/> when the hardening could not be applied (no account was given, the process is not
        /// elevated, the vault is missing or is a link, the account cannot be resolved or is a broad group such as
        /// Everyone, the run was cancelled, or the vault root, a writable folder or a file could not be hardened);
        /// otherwise <see langword="true"/>, including when there was nothing to do. Every outcome is logged, and the
        /// method does not throw.
        /// </returns>
        Task<bool> HardenAsync(string targetAccount, CancellationToken cancellationToken);

        /// <summary>
        /// Re-applies the hardening for every account a Servy service runs under, other than Local System.
        /// </summary>
        /// <param name="serviceRepository">The repository the service accounts are read from.</param>
        /// <param name="cancellationToken">A token that stops the hardening between two accounts or two files.</param>
        /// <returns>A task that completes when every account has been processed. Apart from a null
        /// <paramref name="serviceRepository"/>, it does not throw.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="serviceRepository"/> is null.</exception>
        Task HardenServiceAccountsAsync(IServiceRepository serviceRepository, CancellationToken cancellationToken);

        /// <summary>
        /// Revokes what the hardening granted <paramref name="targetAccount"/> in the vault - its explicit entries on
        /// the vault root, the writable folders and every hardened file - unless a service in
        /// <paramref name="serviceRepository"/> still runs under that account.
        /// </summary>
        /// <param name="targetAccount">The account a removed or reconfigured service ran under.</param>
        /// <param name="serviceRepository">The repository of the services that remain; an account one of them runs
        /// under keeps its access. Accounts are compared by SID, so <c>.\user</c> and <c>MACHINE\user</c> are the same
        /// account.</param>
        /// <param name="cancellationToken">A token that stops the revocation between two items.</param>
        /// <returns>
        /// <see langword="true"/> when the access was revoked, when a remaining service still uses the account, or when
        /// there was nothing to revoke (Local System, the Administrators group or Local System's own SID);
        /// <see langword="false"/> when it could not be revoked (the process is not elevated, the vault is missing or is
        /// a link, the account cannot be resolved or is a broad group, the repository cannot be read, the run was
        /// cancelled, or an item could not be rewritten). Every outcome is logged. Apart from a null
        /// <paramref name="serviceRepository"/>, it does not throw.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="serviceRepository"/> is null.</exception>
        Task<bool> RevokeIfUnusedAsync(string targetAccount, IServiceRepository serviceRepository, CancellationToken cancellationToken);
    }
}

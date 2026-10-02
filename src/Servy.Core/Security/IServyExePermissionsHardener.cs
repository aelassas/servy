using Servy.Core.Data;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Core.Security
{
    /// <summary>
    /// Hardens the permissions Servy's vault (<c>%ProgramData%\Servy</c>), binaries, settings files and logs grant to a
    /// service account, and keeps it out of the database, the encryption keys, the administrative logs and the log
    /// folders of the services that run under other accounts.
    /// </summary>
    public interface IServyExePermissionsHardener
    {
        /// <summary>
        /// Grants <paramref name="targetAccount"/> the least privilege its Servy services need in the vault and locks
        /// Servy's files down for it: it can write in the log folder of each of <paramref name="serviceNames"/>
        /// (<c>logs\service\&lt;ServiceName&gt;\</c>) and nowhere else.
        /// </summary>
        /// <param name="targetAccount">The account the services run under (e.g. <c>DOMAIN\svc-servy</c>, <c>.\user</c>, <c>DOMAIN\gMSA$</c>).</param>
        /// <param name="serviceNames">Every service that runs under the account. The account loses its access to the log
        /// folder of any service not listed, so a partial list takes the logs away from the services left out.</param>
        /// <param name="cancellationToken">A token that stops the hardening between two files.</param>
        /// <returns>
        /// <see langword="false"/> when the hardening could not be applied (no account was given, the process is not
        /// elevated, the vault is missing or is a link, the account cannot be resolved or is a broad group such as
        /// Everyone, the run was cancelled, or the vault root, a writable folder or a file could not be hardened);
        /// otherwise <see langword="true"/>, including when there was nothing to do. Every outcome is logged, and the
        /// method does not throw.
        /// </returns>
        Task<bool> HardenAsync(string targetAccount, IReadOnlyCollection<string> serviceNames, CancellationToken cancellationToken);

        /// <summary>
        /// Applies <see cref="HardenAsync"/> for the account a service was just installed under, with every service that
        /// runs under that account read from <paramref name="serviceRepository"/> plus <paramref name="serviceName"/>.
        /// </summary>
        /// <param name="serviceName">The service that was installed.</param>
        /// <param name="targetAccount">The account the service runs under.</param>
        /// <param name="serviceRepository">The repository the other services of the account are read from. Accounts are
        /// compared by SID, so <c>.\user</c> and <c>MACHINE\user</c> are the same account.</param>
        /// <param name="cancellationToken">A token that stops the hardening between two files.</param>
        /// <returns>
        /// What <see cref="HardenAsync"/> returns; <see langword="false"/> as well when the repository cannot be read, in
        /// which case nothing is changed. Every outcome is logged. Apart from a null
        /// <paramref name="serviceRepository"/>, it does not throw.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="serviceRepository"/> is null.</exception>
        Task<bool> HardenServiceAsync(string serviceName, string targetAccount, IServiceRepository serviceRepository, CancellationToken cancellationToken);

        /// <summary>
        /// Re-applies the hardening for every account a Servy service runs under, other than Local System, each with the
        /// log folders of its own services.
        /// </summary>
        /// <param name="serviceRepository">The repository the service accounts are read from.</param>
        /// <param name="cancellationToken">A token that stops the hardening between two accounts or two files.</param>
        /// <returns>A task that completes when every account has been processed. Apart from a null
        /// <paramref name="serviceRepository"/>, it does not throw.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="serviceRepository"/> is null.</exception>
        Task HardenServiceAccountsAsync(IServiceRepository serviceRepository, CancellationToken cancellationToken);

        /// <summary>
        /// Revokes what the hardening granted <paramref name="targetAccount"/> for <paramref name="serviceName"/> - its
        /// entries on that service's log folder - and, unless a service in <paramref name="serviceRepository"/> still
        /// runs under that account, everything else it granted in the vault: its explicit entries on the vault root,
        /// the log folders and every hardened file.
        /// </summary>
        /// <param name="targetAccount">The account a removed or reconfigured service ran under.</param>
        /// <param name="serviceName">The service that was removed or moved to another account; <see langword="null"/>
        /// or blank revokes no single log folder. The folder is kept when a remaining service of that name still runs
        /// under the account.</param>
        /// <param name="serviceRepository">The repository of the services that remain; an account one of them runs
        /// under keeps its access. Accounts are compared by SID, so <c>.\user</c> and <c>MACHINE\user</c> are the same
        /// account.</param>
        /// <param name="cancellationToken">A token that stops the revocation between two items.</param>
        /// <returns>
        /// <see langword="true"/> when the access was revoked, when a remaining service still uses the account (and the
        /// service's own log folder, if any, was revoked), or when
        /// there was nothing to revoke (Local System, the Administrators group or Local System's own SID);
        /// <see langword="false"/> when it could not be revoked (the process is not elevated, the vault is missing or is
        /// a link, the account cannot be resolved or is a broad group, the repository cannot be read, the run was
        /// cancelled, or an item could not be rewritten). Every outcome is logged. Apart from a null
        /// <paramref name="serviceRepository"/>, it does not throw.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="serviceRepository"/> is null.</exception>
        Task<bool> RevokeIfUnusedAsync(string targetAccount, string serviceName, IServiceRepository serviceRepository, CancellationToken cancellationToken);
    }
}

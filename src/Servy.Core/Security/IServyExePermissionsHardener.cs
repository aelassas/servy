using Servy.Core.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Core.Security
{
    /// <summary>
    /// Hardens the permissions Servy's vault (<c>%ProgramData%\Servy</c>), binaries, configuration files, database,
    /// encryption key, logs and recovery state grant to a service account.
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
        /// <see langword="false"/> when the hardening could not be applied (not elevated, the vault is missing, the account
        /// cannot be resolved, or a file could not be hardened); otherwise <see langword="true"/>, including when there
        /// was nothing to do. Every outcome is logged, and the method does not throw.
        /// </returns>
        Task<bool> HardenAsync(string targetAccount, CancellationToken cancellationToken);

        /// <summary>
        /// Re-applies the hardening for every account a Servy service runs under, other than Local System.
        /// </summary>
        /// <param name="serviceRepository">The repository the service accounts are read from.</param>
        /// <param name="cancellationToken">A token that stops the hardening between two accounts or two files.</param>
        /// <returns>A task that completes when every account has been processed. It does not throw.</returns>
        Task HardenServiceAccountsAsync(IServiceRepository serviceRepository, CancellationToken cancellationToken);
    }
}

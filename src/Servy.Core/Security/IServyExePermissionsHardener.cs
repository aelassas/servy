using System.Threading;
using System.Threading.Tasks;

namespace Servy.Core.Security
{
    /// <summary>
    /// Hardens the permissions Servy's binaries, configuration files and database grant to a service account,
    /// by running <c>Set-ServyExePermissions.ps1</c> for that account.
    /// </summary>
    public interface IServyExePermissionsHardener
    {
        /// <summary>
        /// Runs <c>Set-ServyExePermissions.ps1 -TargetAccount &lt;targetAccount&gt;</c>.
        /// </summary>
        /// <param name="targetAccount">The account the service runs under (e.g. <c>DOMAIN\svc-servy</c>, <c>.\user</c>, <c>DOMAIN\gMSA$</c>).</param>
        /// <param name="cancellationToken">A token that stops the script if it is still running.</param>
        /// <returns>
        /// <see langword="true"/> when the script reports that every file was hardened (exit code 0);
        /// otherwise <see langword="false"/>. Every outcome is logged, and the method does not throw.
        /// </returns>
        Task<bool> HardenAsync(string targetAccount, CancellationToken cancellationToken);
    }
}

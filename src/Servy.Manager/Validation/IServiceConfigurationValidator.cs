using Servy.Core.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Manager.Validation
{
    /// <summary>
    /// Provides functionality to validate service configurations.
    /// </summary>
    public interface IServiceConfigurationValidator
    {
        /// <summary>
        /// Validates the provided service configuration and displays a message box if any issues are found.
        /// </summary>
        /// <param name="dto">The service configuration data to validate.</param>
        /// <param name="importMode">
        /// When <see langword="true"/>, skips the credential stage (account identity and password matching)
        /// in the shared validation rules. Used for configuration imports, which carry no confirmation input.
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// A task representing the asynchronous validation operation.
        /// The result is <see langword="true"/> if the configuration is valid; otherwise, <see langword="false"/>.
        /// </returns>
        Task<bool> ValidateAsync(ServiceDto dto, bool importMode = false, CancellationToken cancellationToken = default);
    }
}

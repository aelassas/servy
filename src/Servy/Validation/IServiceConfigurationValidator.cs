using Servy.Core.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Validation
{
    /// <summary>
    /// Provides functionality to validate service configurations.
    /// </summary>
    public interface IServiceConfigurationValidator
    {
        /// <summary>
        /// Validates the specified service configuration and displays a message box if validation fails.
        /// </summary>
        /// <param name="dto">The service configuration data to validate.</param>
        /// <param name="wrapperExePath">The optional path to the service wrapper executable.</param>
        /// <param name="confirmPassword">The password confirmation string to compare against the configuration's password.</param>
        /// <param name="importMode">Import mode flag to skip credentials validation.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// A task that represents the asynchronous validation operation. 
        /// The task result contains <see langword="true"/> if validation passed; otherwise, <see langword="false"/>.
        /// </returns>
        Task<bool> ValidateAsync(ServiceDto dto, string wrapperExePath = null, string confirmPassword = null, bool importMode = false, CancellationToken cancellationToken = default);
    }
}

using Servy.Core.DTOs;
using System.Threading.Tasks;

namespace Servy.Manager.Helpers
{
    /// <summary>
    /// Provides functionality to validate service configurations.
    /// </summary>
    public interface IServiceConfigurationValidator
    {
        /// <summary>
        /// Validates the given service configuration.
        /// </summary>
        /// <param name="dto">The service DTO containing configuration.</param>
        /// <returns>True if valid, otherwise false.</returns>
        Task<bool> Validate(ServiceDto dto);
    }
}

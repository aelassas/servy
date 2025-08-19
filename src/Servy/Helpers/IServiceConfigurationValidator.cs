using Servy.Core.DTOs;

namespace Servy.Helpers
{
    /// <summary>
    /// Provides functionality to validate service configurations.
    /// </summary>
    public interface IServiceConfigurationValidator
    {
        /// <summary>
        /// Validates the configuration of the specified service.
        /// </summary>
        /// <param name="dto">
        /// The <see cref="ServiceDto"/> containing the service configuration to validate.
        /// </param>
        /// <param name="wrapperExePath">
        /// Optional path to the wrapper executable, used for validation.
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> that resolves to <c>true</c> if the configuration is valid; otherwise <c>false</c>.
        /// </returns>
        Task<bool> Validate(ServiceDto dto, string wrapperExePath = null);
    }
}

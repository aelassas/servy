using Servy.Core.DTOs;

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
        /// <param name="wrapperExePath">
        /// Optional absolute path to the service wrapper executable. If provided, the validator ensures
        /// the file exists on disk; when <c>null</c> that check is skipped.
        /// </param>
        /// <param name="confirmPassword">
        /// Optional password confirmation string. If provided as a non-null string (including empty <c>""</c>),
        /// an exact match against the configuration's password is enforced. Pass <c>null</c> from contexts
        /// with no confirmation input field, such as imports.
        /// </param>
        /// <param name="importMode">
        /// When <see langword="true"/>, skips the credential stage (account identity and password matching)
        /// in the shared validation rules. Used for configuration imports, which carry no confirmation input.
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// A task that represents the asynchronous validation operation.
        /// The task result contains <see langword="true"/> if validation passed; otherwise, <see langword="false"/>.
        /// </returns>
        Task<bool> ValidateAsync(ServiceDto? dto, string? wrapperExePath = null, string? confirmPassword = null, bool importMode = false, CancellationToken cancellationToken = default);
    }
}

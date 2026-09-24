using Servy.Core.DTOs;
using Servy.Core.Validation;
using Servy.UI.Services;

namespace Servy.UI.Validation
{
    /// <summary>
    /// Provides UI-facing validation for service configurations by aggregating core validation rules
    /// and displaying issues via a message box.
    /// </summary>
    /// <remarks>
    /// One implementation serves both applications. The only genuinely per-application value is the
    /// message box caption, which is passed in by the hosting application in the same way the other
    /// shared UI services take their per-application state.
    /// </remarks>
    public class ServiceConfigurationValidator : IServiceConfigurationValidator
    {
        private readonly IMessageBoxService _messageBoxService;
        private readonly IServiceValidationRules _serviceValidationRules;
        private readonly string _caption;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceConfigurationValidator"/> class.
        /// </summary>
        /// <param name="messageBoxService">The service used to display the first validation error to the user.</param>
        /// <param name="serviceValidationRules">Shared validation rules for service installation.</param>
        /// <param name="caption">The caption of the message box shown when validation fails, supplied by the hosting application.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="messageBoxService"/>, <paramref name="serviceValidationRules"/> or <paramref name="caption"/> is null.</exception>
        public ServiceConfigurationValidator(IMessageBoxService messageBoxService, IServiceValidationRules serviceValidationRules, string caption)
        {
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
            _serviceValidationRules = serviceValidationRules ?? throw new ArgumentNullException(nameof(serviceValidationRules));
            _caption = caption ?? throw new ArgumentNullException(nameof(caption));
        }

        /// <inheritdoc />
        /// <remarks>
        /// This implementation follows a fail-fast approach, showing only the first identified
        /// error to prevent overwhelming the user with multiple dialog boxes.
        /// </remarks>
        public async Task<bool> ValidateAsync(ServiceDto? dto, string? wrapperExePath = null, string? confirmPassword = null, bool importMode = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Delegate logic to the shared Core rules engine
            var result = _serviceValidationRules.Validate(dto, wrapperExePath, confirmPassword, importMode: importMode);

            // Display the first validation error
            if (!result.IsValid)
            {
                await _messageBoxService.ShowErrorAsync(result.Errors.First(), _caption);
                return false;
            }

            return true;
        }
    }
}

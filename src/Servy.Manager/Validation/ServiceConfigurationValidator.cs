using Servy.Core.DTOs;
using Servy.Core.Validation;
using Servy.Manager.Config;
using Servy.UI.Services;

namespace Servy.Manager.Validation
{
    /// <summary>
    /// Implements service configuration validation for the Manager application, 
    /// utilizing shared domain rules and displaying feedback via the UI.
    /// </summary>
    public class ServiceConfigurationValidator : IServiceConfigurationValidator
    {
        private readonly IMessageBoxService _messageBoxService;
        private readonly IServiceValidationRules _serviceValidationRules;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceConfigurationValidator"/> class.
        /// </summary>
        /// <param name="messageBoxService">The service used to display error and warning messages to the user.</param>
        /// <param name="serviceValidationRules">Shared validation rules for service installation.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="messageBoxService"/> or <paramref name="serviceValidationRules"/> is null.</exception>
        public ServiceConfigurationValidator(IMessageBoxService messageBoxService, IServiceValidationRules serviceValidationRules)
        {
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
            _serviceValidationRules = serviceValidationRules ?? throw new ArgumentNullException(nameof(serviceValidationRules));
        }

        /// <summary>
        /// Validates the provided service configuration and displays a message box if any issues are found.
        /// </summary>
        /// <param name="dto">The service configuration data to validate.</param>
        /// <returns>
        /// A task representing the asynchronous validation operation. 
        /// The result is <see langword="true"/> if the configuration is valid; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// This implementation follows a fail-fast approach, showing only the first identified 
        /// error to prevent overwhelming the user with multiple dialog boxes.
        /// </remarks>
        public async Task<bool> ValidateAsync(ServiceDto dto)
        {
            // Delegate core validation logic to the centralized rules engine
            var result = _serviceValidationRules.Validate(dto, importMode: true);

            // Display critical errors first
            if (!result.IsValid)
            {
                await _messageBoxService.ShowErrorAsync(result.Errors.First(), UiAppConfig.Caption);
                return false;
            }

            return true;
        }
    }
}
using Servy.Config;
using Servy.Core.DTOs;
using Servy.Core.Validators;
using Servy.UI.Services;

namespace Servy.Validators
{
    /// <summary>
    /// Provides UI-facing validation for service configurations by aggregating core validation rules 
    /// and displaying issues via a message box.
    /// </summary>
    public class ServiceConfigurationValidator : IServiceConfigurationValidator
    {
        private readonly IMessageBoxService _messageBoxService;
        private readonly ServiceValidationRules _serviceValidationRules;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceConfigurationValidator"/> class.
        /// </summary>
        /// <param name="messageBoxService">The service used to display validation errors and warnings to the user.</param>
        /// <param name="serviceValidationRules">Shared validation rules for service installation.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="messageBoxService"/> or <paramref name="serviceValidationRules"/> is null.</exception>
        public ServiceConfigurationValidator(IMessageBoxService messageBoxService, ServiceValidationRules serviceValidationRules)
        {
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
            _serviceValidationRules = serviceValidationRules ?? throw new ArgumentNullException(nameof(serviceValidationRules));
        }

        /// <summary>
        /// Validates the specified service configuration and displays a message box if validation fails.
        /// </summary>
        /// <param name="dto">The service configuration data to validate.</param>
        /// <param name="wrapperExePath">The optional path to the service wrapper executable.</param>
        /// <param name="checkServiceStatus">If set to <see langword="true"/>, performs additional checks against the current status of the service.</param>
        /// <param name="confirmPassword">The password confirmation string to compare against the configuration's password.</param>
        /// <returns>
        /// A task that represents the asynchronous validation operation. 
        /// The task result contains <see langword="true"/> if validation passed; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// This method prioritizes warnings over errors, displaying only the first identified issue 
        /// to the user to maintain a clean "fail-fast" UI experience.
        /// </remarks>
        public async Task<bool> Validate(ServiceDto dto, string wrapperExePath = null, bool checkServiceStatus = true, string confirmPassword = "")
        {
            // Delegate logic to the shared Core rules engine
            var result = _serviceValidationRules.Validate(dto, wrapperExePath, confirmPassword);

            // Handle Warnings first (as per legacy behavior)
            if (result.Warnings.Any())
            {
                await _messageBoxService.ShowWarningAsync(result.Warnings.First(), AppConfig.Caption);
                return false;
            }

            // Handle Critical Errors
            if (result.Errors.Any())
            {
                await _messageBoxService.ShowErrorAsync(result.Errors.First(), AppConfig.Caption);
                return false;
            }

            return true;
        }
    }
}
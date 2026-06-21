using Servy.Config;
using Servy.Core.DTOs;
using Servy.Core.Validation;
using Servy.UI.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Servy.Validation
{
    /// <summary>
    /// Provides UI-facing validation for service configurations by aggregating core validation rules 
    /// and displaying issues via a message box.
    /// </summary>
    public class ServiceConfigurationValidator : IServiceConfigurationValidator
    {
        private readonly IMessageBoxService _messageBoxService;
        private readonly IServiceValidationRules _serviceValidationRules;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceConfigurationValidator"/> class.
        /// </summary>
        /// <param name="messageBoxService">The service used to display validation errors and warnings to the user.</param>
        /// <param name="serviceValidationRules">Shared validation rules for service installation.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="messageBoxService"/> or <paramref name="serviceValidationRules"/> is null.</exception>
        public ServiceConfigurationValidator(IMessageBoxService messageBoxService, IServiceValidationRules serviceValidationRules)
        {
            _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
            _serviceValidationRules = serviceValidationRules ?? throw new ArgumentNullException(nameof(serviceValidationRules));
        }

        /// <summary>
        /// Validates the specified service configuration and displays a message box if validation fails.
        /// </summary>
        /// <param name="dto">The service configuration data to validate.</param>
        /// <param name="wrapperExePath">The optional path to the service wrapper executable.</param>
        /// <param name="confirmPassword">The password confirmation string to compare against the configuration's password.</param>
        /// <returns>
        /// A task that represents the asynchronous validation operation. 
        /// The task result contains <see langword="true"/> if validation passed; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// This implementation follows a fail-fast approach, showing only the first identified 
        /// error to prevent overwhelming the user with multiple dialog boxes.
        /// </remarks>
        public async Task<bool> ValidateAsync(ServiceDto dto, string wrapperExePath = null, string confirmPassword = null)
        {
            if (dto == null) return false;

            // Delegate logic to the shared Core rules engine
            var result = _serviceValidationRules.Validate(dto, wrapperExePath, confirmPassword);

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
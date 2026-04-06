using Servy.CLI.Models;
using Servy.CLI.Options;

namespace Servy.CLI.Validators
{
    /// <summary>
    /// Handles complex validation for service installation.
    /// </summary>
    /// <remarks>
    /// Note: This is currently the only dedicated validator in the CLI project. 
    /// Other commands (Start, Stop, etc.) use inline validation because they 
    /// only require simple 'ServiceName' checks. Dedicated validators are 
    /// reserved for commands with complex option sets to avoid unnecessary 
    /// boilerplate.
    /// </remarks>
    public interface IServiceInstallValidator
    {
        /// <summary>
        /// Validates the provided service installation options.
        /// </summary>
        /// <param name="opts">
        /// The <see cref="InstallServiceOptions"/> containing the configuration 
        /// (paths, account details, and hooks) for the new service.
        /// </param>
        /// <returns>
        /// A <see cref="CommandResult"/> representing the outcome of the validation. 
        /// Returns <c>Success</c> if all paths and configurations are valid; 
        /// otherwise, returns <c>Fail</c> with a specific error message.
        /// </returns>
        CommandResult Validate(InstallServiceOptions opts);
    }
}
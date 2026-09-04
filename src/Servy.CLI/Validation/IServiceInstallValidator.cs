using Servy.CLI.Models;
using Servy.CLI.Options;

namespace Servy.CLI.Validation
{
    /// <summary>
    /// Handles complex validation for service installation.
    /// </summary>
    /// <remarks>
    /// Note: This is currently the only dedicated validator in the CLI project.
    /// The lifecycle commands (Start, Stop, Restart, Status, Uninstall) validate
    /// inline because they only need a service-name check. Export and Import also
    /// validate inline, but additionally check the configuration file type via
    /// <c>Helper.TryParseFileType</c>; Import takes no service name at all and
    /// delegates path canonicalization, UNC blocking and size limits to
    /// <c>ImportGuard.ValidatePathSecurityAndSize</c>. A dedicated validator is
    /// used here because Install is the only command whose option set needs
    /// cross-field rules over a mapped <c>ServiceDto</c>.
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

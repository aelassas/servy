using Servy.Core.DTOs;

namespace Servy.Core.Validators
{
    /// <summary>
    /// Defines the contract for centralized validation logic of service configurations.
    /// This interface ensures that service definitions meet domain requirements, 
    /// security constraints, and system-level path accessibility before persistence or deployment.
    /// </summary>
    public interface IServiceValidationRules
    {
        /// <summary>
        /// Validates a <see cref="ServiceDto"/> against domain requirements, path accessibility, and configuration bounds.
        /// </summary>
        /// <param name="dto">The service configuration data transfer object to validate. Can be null.</param>
        /// <param name="wrapperExePath">
        /// Optional absolute path to the service wrapper executable. 
        /// If provided, the validator ensures the file exists on the physical disk.
        /// </param>
        /// <param name="confirmPassword">
        /// Optional password string used to verify that a user-entered password matches 
        /// the password stored in the <paramref name="dto"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValidationResult"/> containing a collection of errors (blocking issues) 
        /// and warnings (non-blocking suggestions or length limit notifications).
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method performs a multi-stage validation:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// <description><b>Vital Requirements:</b> Checks for null DTOs and missing mandatory fields (Name, ExecutablePath).</description>
        /// </item>
        /// <item>
        /// <description><b>Path Integrity:</b> Uses an injected process helper to verify that executables and directories are valid and accessible.</description>
        /// </item>
        /// <item>
        /// <description><b>Configuration Bounds:</b> Ensures timeouts, rotation sizes, and health intervals stay within defined application limits.</description>
        /// </item>
        /// <item>
        /// <description><b>Credential Security:</b> Validates account identities via native methods and enforces password matching logic.</description>
        /// </item>
        /// </list>
        /// </remarks>
        ValidationResult Validate(ServiceDto dto, string wrapperExePath = null, string confirmPassword = "");
    }
}
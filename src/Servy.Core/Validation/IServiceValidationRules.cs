using Servy.Core.DTOs;

namespace Servy.Core.Validation
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
        /// Optional password confirmation string. If provided as a non-null string (including empty <c>""</c>),
        /// the validator enforces an exact match against <see cref="ServiceDto.Password"/>. Pass <c>null</c>
        /// when invoking from contexts without a confirmation input field (e.g. CLI or background imports).
        /// </param>
        /// <param name="importMode">
        /// When <c>true</c>, skips the credential-validation stage entirely (used by XML/JSON imports,
        /// whose identity fields are reset to LocalSystem after deserialization).
        /// </param>
        /// <returns>
        /// A <see cref="ValidationResult"/> containing a collection of errors (blocking issues).
        /// All structural validation failures - including string length-limit or boundary violations - are reported
        /// as blocking errors; there is no separate warnings channel.
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
        /// <description>
        /// <b>Credential Security:</b> Validates account identities via native methods and enforces password
        /// matching logic. This stage runs only when <paramref name="importMode"/> is <c>false</c> <b>and</b>
        /// <see cref="ServiceDto.RunAsLocalSystem"/> is not <c>true</c>; a service configured to run as
        /// LocalSystem has no account or password to verify. Note that an unset (<c>null</c>)
        /// <c>RunAsLocalSystem</c> takes the validating branch and will report a missing-username error.
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        ValidationResult Validate(ServiceDto? dto, string? wrapperExePath = null, string? confirmPassword = null, bool importMode = false);
    }
}

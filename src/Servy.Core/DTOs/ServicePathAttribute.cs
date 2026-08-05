namespace Servy.Core.DTOs
{
    /// <summary>
    /// Specifies that a property represents an existing system path (an executable file or startup directory)
    /// that must already exist and pass existence/permission checks during service creation, import, or startup.
    /// </summary>
    /// <remarks>
    /// Destination output paths (such as stdout/stderr log files) that are created dynamically at runtime
    /// should not use this attribute and are validated separately for path syntax integrity.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ServicePathAttribute : Attribute
    {
        /// <summary>
        /// Gets the human-readable label for the path, used for diagnostic messaging.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gets a value indicating whether the path is expected to be a file.
        /// </summary>
        public bool IsFile { get; }

        /// <summary>
        /// Gets a value indicating whether this path is mandatory for a valid service configuration.
        /// </summary>
        public bool Required { get; }

        /// <summary>
        /// Gets the resource key name in <see cref="Resources.Strings"/> used for localized validation error messages.
        /// </summary>
        public string? ErrorResourceKey { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServicePathAttribute"/> class.
        /// </summary>
        /// <param name="label">The human-readable label used in error messages (e.g., "startup directory").</param>
        /// <param name="isFile"><c>true</c> if the path must point to an existing file; <c>false</c> if it must point to an existing directory.</param>
        /// <param name="required"><c>true</c> if the path must be provided and cannot be null or whitespace.</param>
        /// <param name="errorResourceKey">The key corresponding to the localized error string in <see cref="Resources.Strings"/>.</param>
        public ServicePathAttribute(string label, bool isFile = true, bool required = false, string? errorResourceKey = null)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("label cannot be null, empty or whitespace.", nameof(label));

            Label = label;
            IsFile = isFile;
            Required = required;
            ErrorResourceKey = errorResourceKey;
        }
    }
}

using System;

namespace Servy.Core.DTOs
{
    /// <summary>
    /// Specifies that a property represents a system path that requires validation during service import or installation.
    /// </summary>
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
        /// Initializes a new instance of the <see cref="ServicePathAttribute"/> class.
        /// </summary>
        /// <param name="label">The human-readable label used in error messages (e.g., "startup directory").</param>
        /// <param name="isFile"><c>true</c> if the path must point to a file; <c>false</c> if it must point to a directory.</param>
        /// <param name="required"><c>true</c> if the path must be provided and cannot be null or whitespace.</param>
        public ServicePathAttribute(string label, bool isFile = true, bool required = false)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("label cannot be null, empty or whitespace.", nameof(label));
            Label = label;
            IsFile = isFile;
            Required = required;
        }
    }
}
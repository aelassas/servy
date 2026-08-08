using Servy.Core.DTOs;
using System.Reflection;

namespace Servy.Core.Validation
{
    /// <summary>
    /// Represents a single <see cref="ServicePathAttribute"/> rule violation discovered on a decorated property.
    /// </summary>
    public sealed class ServicePathViolation
    {
        /// <summary>
        /// Gets the property information that triggered the validation failure.
        /// </summary>
        public PropertyInfo Property { get; }

        /// <summary>
        /// Gets the <see cref="ServicePathAttribute"/> instance attached to the property.
        /// </summary>
        public ServicePathAttribute Attribute { get; }

        /// <summary>
        /// Gets the raw string path value retrieved from the property.
        /// </summary>
        public string? Value { get; }

        /// <summary>
        /// Gets a value indicating whether the violation was caused by a missing required path (<c>true</c>)
        /// or an invalid/non-existent path (<c>false</c>).
        /// </summary>
        public bool IsMissing { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServicePathViolation"/> class.
        /// </summary>
        /// <param name="property">The property information that triggered the validation failure.</param>
        /// <param name="attribute">The <see cref="ServicePathAttribute"/> instance attached to the property.</param>
        /// <param name="value">The raw string path value retrieved from the property.</param>
        /// <param name="isMissing">
        /// <c>true</c> if a required path was null or whitespace; <c>false</c> if present but failed file/directory checks.
        /// </param>
        public ServicePathViolation(
            PropertyInfo property,
            ServicePathAttribute attribute,
            string? value,
            bool isMissing)
        {
            Property = property;
            Attribute = attribute;
            Value = value;
            IsMissing = isMissing;
        }
    }
}

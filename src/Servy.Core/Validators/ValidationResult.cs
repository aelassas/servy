using System.Collections.Generic;
using System.Linq;

namespace Servy.Core.Validators
{
    /// <summary>
    /// Represents the outcome of a validation operation, including any encountered errors or warnings.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Gets a value indicating whether the validation was successful.
        /// Returns <see langword="true"/> if there are no errors and no warnings; otherwise, <see langword="false"/>.
        /// </summary>
        public bool IsValid => !Errors.Any() && !Warnings.Any();

        /// <summary>
        /// Gets the collection of error messages identified during the validation process.
        /// Errors typically represent critical failures that prevent the operation from proceeding.
        /// </summary>
        public List<string> Errors { get; } = new List<string>();

        /// <summary>
        /// Gets the collection of warning messages identified during the validation process.
        /// Warnings represent non-critical issues that should be brought to the user's attention but may not block the operation.
        /// </summary>
        public List<string> Warnings { get; } = new List<string>();
    }
}
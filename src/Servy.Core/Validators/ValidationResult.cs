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
        /// Returns <see langword="true"/> if there are no errors; otherwise, <see langword="false"/>.
        /// </summary>
        public bool IsValid => !Errors.Any();

        /// <summary>
        /// Gets the collection of error messages identified during the validation process.
        /// Errors typically represent critical failures that prevent the operation from proceeding.
        /// </summary>
        public List<string> Errors { get; } = new List<string>();
    }
}
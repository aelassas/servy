namespace Servy.Core.Validation
{
    /// <summary>
    /// A secure token representing a file path that has successfully passed all defense-in-depth security invariants.
    /// The constructor is intentionally hidden to prevent arbitrary instantiation outside of the security gate.
    /// </summary>
    public sealed class ValidatedPath
    {
        /// <summary>
        /// Gets the absolute, fully qualified, and structurally verified filesystem path.
        /// </summary>
        public string ResolvedPath { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidatedPath"/> class.
        /// </summary>
        /// <param name="resolvedPath">The fully canonicalized filesystem path that has passed validation.</param>
        internal ValidatedPath(string resolvedPath)
        {
            ResolvedPath = resolvedPath;
        }
    }

    /// <summary>
    /// Represents the outcome of the path security validation pipeline.
    /// </summary>
    public sealed class PathSecurityResult
    {
        /// <summary>
        /// Gets a value indicating whether the path successfully passed all security validation checks.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the validated path token containing the verified path string. 
        /// Guaranteed to be non-null when <see cref="IsValid"/> is <c>true</c>; otherwise, <c>null</c>.
        /// </summary>
        public ValidatedPath? ValidPath { get; }

        /// <summary>
        /// Gets the descriptive message detailing the specific rule or infrastructural failure that caused the validation to fail.
        /// Guaranteed to be non-null when <see cref="IsValid"/> is <c>false</c>; otherwise, <c>null</c>.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PathSecurityResult"/> class representing a successful validation.
        /// </summary>
        /// <param name="path">The secure, verified path token instance.</param>
        private PathSecurityResult(ValidatedPath path)
        {
            IsValid = true;
            ValidPath = path;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PathSecurityResult"/> class representing a failed validation.
        /// </summary>
        /// <param name="error">The descriptive validation failure error string.</param>
        private PathSecurityResult(string error)
        {
            IsValid = false;
            ErrorMessage = error;
        }

        /// <summary>
        /// Creates a successful <see cref="PathSecurityResult"/> encapsulating a verified path token.
        /// </summary>
        /// <param name="path">The raw filesystem path string that passed security clearance invariants.</param>
        /// <returns>An initialized success descriptor containing a valid <see cref="ValidatedPath"/> instance.</returns>
        internal static PathSecurityResult Success(string path) => new PathSecurityResult(new ValidatedPath(path));

        /// <summary>
        /// Creates a failed <see cref="PathSecurityResult"/> capturing a validation rule violation error string.
        /// </summary>
        /// <param name="error">The descriptive error explaining the security or infrastructural restriction trip.</param>
        /// <returns>An initialized failure descriptor containing an explicit error message.</returns>
        internal static PathSecurityResult Fail(string error) => new PathSecurityResult(error);
    }
}
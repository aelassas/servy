namespace Servy.Core.Validation
{
    /// <summary>
    /// A secure token representing a file path that has successfully passed all defense-in-depth security invariants.
    /// The constructor is intentionally hidden to prevent arbitrary instantiation outside of the security gate.
    /// </summary>
    public sealed class ValidatedImportPath
    {
        public string ResolvedPath { get; }

        internal ValidatedImportPath(string resolvedPath)
        {
            ResolvedPath = resolvedPath;
        }
    }

    /// <summary>
    /// Represents the outcome of the path security validation pipeline.
    /// </summary>
    public sealed class PathSecurityResult
    {
        public bool IsValid { get; }
        public ValidatedImportPath ValidPath { get; }
        public string ErrorMessage { get; }

        private PathSecurityResult(ValidatedImportPath path)
        {
            IsValid = true;
            ValidPath = path;
        }

        private PathSecurityResult(string error)
        {
            IsValid = false;
            ErrorMessage = error;
        }

        internal static PathSecurityResult Success(string path) => new PathSecurityResult(new ValidatedImportPath(path));
        internal static PathSecurityResult Fail(string error) => new PathSecurityResult(error);
    }
}

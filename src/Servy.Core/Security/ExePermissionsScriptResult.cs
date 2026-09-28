namespace Servy.Core.Security
{
    /// <summary>
    /// The outcome of one run of <c>Set-ServyExePermissions.ps1</c>.
    /// </summary>
    public sealed class ExePermissionsScriptResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExePermissionsScriptResult"/> class.
        /// </summary>
        /// <param name="exitCode">The script's exit code, or <see langword="null"/> when it timed out and was stopped.</param>
        /// <param name="output">The script's combined standard output and standard error.</param>
        public ExePermissionsScriptResult(int? exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output ?? string.Empty;
        }

        /// <summary>
        /// Gets the script's exit code, or <see langword="null"/> when it timed out and was stopped.
        /// </summary>
        public int? ExitCode { get; }

        /// <summary>
        /// Gets the script's combined standard output and standard error.
        /// </summary>
        public string Output { get; }
    }
}

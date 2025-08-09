namespace Servy.CLI.Models
{
    /// <summary>
    /// Represents the result of a command execution, including success status, message, and exit code.
    /// </summary>
    public class CommandResult
    {
        /// <summary>
        /// Gets a value indicating whether the command succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets the message associated with the command result.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the exit code representing the command result.
        /// 0 indicates success; non-zero indicates failure.
        /// </summary>
        public int ExitCode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandResult"/> class.
        /// </summary>
        /// <param name="success">True if the command succeeded; otherwise, false.</param>
        /// <param name="message">The message associated with the result.</param>
        /// <param name="exitCode">The exit code to return.</param>
        private CommandResult(bool success, string message, int exitCode)
        {
            Success = success;
            Message = message;
            ExitCode = exitCode;
        }

        /// <summary>
        /// Creates a successful command result.
        /// </summary>
        /// <param name="message">Optional success message.</param>
        /// <returns>A <see cref="CommandResult"/> indicating success.</returns>
        public static CommandResult Ok(string message = "") => new CommandResult(true, message, 0);

        /// <summary>
        /// Creates a failed command result.
        /// </summary>
        /// <param name="message">Optional failure message.</param>
        /// <returns>A <see cref="CommandResult"/> indicating failure.</returns>
        public static CommandResult Fail(string message = "") => new CommandResult(false, message, 1);
    }
}

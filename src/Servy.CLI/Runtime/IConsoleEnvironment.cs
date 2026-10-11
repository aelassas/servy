namespace Servy.CLI.Runtime
{
    /// <summary>
    /// The console and session properties <see cref="Program.IsRealConsole(IConsoleEnvironment)"/> probes to decide
    /// whether a real, interactive console window is attached.
    /// </summary>
    /// <remarks>
    /// Production uses <see cref="ConsoleEnvironment"/>. Tests use a fake so every probe can be driven, including
    /// the Win32 window handle and the console buffer, which a test host cannot control.
    /// </remarks>
    internal interface IConsoleEnvironment
    {
        /// <summary>
        /// Gets a value indicating whether the process can interact with a desktop (it is not in Session 0).
        /// </summary>
        bool UserInteractive { get; }

        /// <summary>
        /// Gets a value indicating whether standard output is redirected to a file or another process.
        /// </summary>
        bool IsOutputRedirected { get; }

        /// <summary>
        /// Gets a value indicating whether standard error is redirected to a file or another process.
        /// </summary>
        bool IsErrorRedirected { get; }

        /// <summary>
        /// Gets the handle of the window used by the console of the calling process.
        /// </summary>
        /// <returns>The window handle, or <see cref="IntPtr.Zero"/> when the process has no console window.</returns>
        IntPtr GetConsoleWindow();

        /// <summary>
        /// Gets the height of the console window, in rows.
        /// </summary>
        /// <returns>The height of the console window.</returns>
        /// <exception cref="IOException">Thrown when the console buffer cannot be reached.</exception>
        int GetWindowHeight();
    }
}

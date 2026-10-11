using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Servy.CLI.Runtime
{
    /// <summary>
    /// Reads the console and session properties of the current process from <see cref="Environment"/>,
    /// <see cref="Console"/> and the Win32 console API.
    /// </summary>
    internal sealed class ConsoleEnvironment : IConsoleEnvironment
    {
        /// <summary>
        /// Gets a value indicating whether the process can interact with a desktop (it is not in Session 0).
        /// </summary>
        public bool UserInteractive => Environment.UserInteractive;

        /// <summary>
        /// Gets a value indicating whether standard output is redirected to a file or another process.
        /// </summary>
        public bool IsOutputRedirected => Console.IsOutputRedirected;

        /// <summary>
        /// Gets a value indicating whether standard error is redirected to a file or another process.
        /// </summary>
        public bool IsErrorRedirected => Console.IsErrorRedirected;

        /// <summary>
        /// Gets the handle of the window used by the console of the calling process.
        /// </summary>
        /// <returns>The window handle, or <see cref="IntPtr.Zero"/> when the process has no console window.</returns>
        public IntPtr GetConsoleWindow() => NativeGetConsoleWindow();

        /// <summary>
        /// Gets the height of the console window, in rows.
        /// </summary>
        /// <returns>The height of the console window.</returns>
        /// <exception cref="IOException">Thrown when the console buffer cannot be reached.</exception>
        public int GetWindowHeight() => Console.WindowHeight;

        /// <summary>
        /// Retrieves the window handle used by the console associated with the calling process.
        /// </summary>
        /// <returns>
        /// A handle to the window used by the console, or <see cref="IntPtr.Zero"/> if there is no such associated window.
        /// </returns>
        [ExcludeFromCodeCoverage]
        [DllImport("kernel32.dll", EntryPoint = "GetConsoleWindow")]
        private static extern IntPtr NativeGetConsoleWindow();
    }
}

using System;
using System.Diagnostics;

namespace Servy.Service
{
    /// <summary>
    /// Defines a wrapper interface for managing and interacting with a system process.
    /// </summary>
    public interface IProcessWrapper : IDisposable
    {
        /// <summary>
        /// Process handle.
        /// </summary>
        IntPtr ProcessHandle { get; }

        /// <summary>
        /// Occurs when the associated process writes a line to its standard output stream.
        /// </summary>
        event DataReceivedEventHandler OutputDataReceived;

        /// <summary>
        /// Occurs when the associated process writes a line to its standard error stream.
        /// </summary>
        event DataReceivedEventHandler ErrorDataReceived;

        /// <summary>
        /// Occurs when the associated process exits.
        /// </summary>
        event EventHandler Exited;

        /// <summary>
        /// Gets the unique identifier for the associated process.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Gets a value indicating whether the associated process has exited.
        /// </summary>
        bool HasExited { get; }

        /// <summary>
        /// Gets the native handle for the associated process.
        /// </summary>
        IntPtr Handle { get; }

        /// <summary>
        /// Gets the exit code of the associated process.
        /// </summary>
        int ExitCode { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the <see cref="Exited"/> event should be raised when the process terminates.
        /// </summary>
        bool EnableRaisingEvents { get; set; }

        /// <summary>
        /// Starts the process.
        /// </summary>
        void Start();

        /// <summary>
        /// Immediately stops the associated process.
        /// </summary>
        void Kill();

        /// <summary>
        /// Instructs the process to wait for exit for a specified time.
        /// </summary>
        /// <param name="milliseconds">The amount of time, in milliseconds, to wait for the associated process to exit.</param>
        /// <returns><c>true</c> if the process exited within the specified time; otherwise, <c>false</c>.</returns>
        bool WaitForExit(int milliseconds);

        /// <summary>
        /// Instructs the process to wait indefinitely for exit.
        /// </summary>
        void WaitForExit();

        /// <summary>
        /// Closes the main window of the associated process.
        /// </summary>
        /// <returns><c>true</c> if the main window has been successfully closed; otherwise, <c>false</c>.</returns>
        bool CloseMainWindow();

        /// <summary>
        /// Gets the window handle of the main window for the associated process.
        /// </summary>
        IntPtr MainWindowHandle { get; }

        /// <summary>
        /// Gets or sets the overall priority category for the associated process.
        /// </summary>
        ProcessPriorityClass PriorityClass { get; set; }

        /// <summary>
        /// Begins asynchronous read operations on the redirected standard output stream of the application.
        /// </summary>
        void BeginOutputReadLine();

        /// <summary>
        /// Begins asynchronous read operations on the redirected standard error stream of the application.
        /// </summary>
        void BeginErrorReadLine();
    }
}

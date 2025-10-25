using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Service.ProcessManagement
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
        /// Standard output stream of the process.
        /// </summary>
        StreamReader StandardOutput { get; }

        /// <summary>
        /// Standard error stream of the process.
        /// </summary>
        StreamReader StandardError { get; }

        /// <summary>
        /// Starts the process.
        /// </summary>
        void Start();

        /// <summary>
        /// Waits until the child process remains alive for the specified timeout,
        /// which indicates that the process has started successfully and did not
        /// exit prematurely.
        /// </summary>
        /// <param name="timeout">
        /// The maximum time to wait for the process to remain running.
        /// </param>
        /// <param name="cancellationToken">
        /// An optional token to observe while waiting. If cancellation is requested,
        /// the task is canceled.
        /// </param>
        /// <returns>
        /// A task that resolves to <c>true</c> if the process is still running after
        /// the timeout period; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method polls the process state every 500 milliseconds until the timeout
        /// is reached. If the process exits during this time, the method returns <c>false</c>.
        /// </remarks>
        Task<bool> WaitUntilHealthyAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the associated process.
        /// </summary>
        /// <param name="timeoutMs">The timeout in milliseconds to wait for the process to stop.</param>
        bool? Stop(int timeoutMs);

        /// <summary>
        /// Stops all descendant processes of the associated process.
        /// </summary>
        /// <param name="timeoutMs">The timeout in milliseconds to wait for the descendant processes to stop.</param>
        void StopDescendants(int timeoutMs);

        /// <summary>
        /// Formats the process information as a string.
        /// </summary>
        /// <returns></returns>
        string Format();

        /// <summary>
        /// Immediately stops the associated process and optionally its child/descendent processes.
        /// </summary>
        /// <param name="entireProcessTree">Kill entire process tree.</param>
        void Kill(bool entireProcessTree = false);

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

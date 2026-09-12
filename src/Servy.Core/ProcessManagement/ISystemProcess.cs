namespace Servy.Core.ProcessManagement
{
    /// <summary>
    /// Abstraction wrapping operations on a system process context.
    /// </summary>
    public interface ISystemProcess : IDisposable
    {
        /// <summary>
        /// Gets the unique identifier for the associated process.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Gets the name of the process.
        /// </summary>
        string ProcessName { get; }

        /// <summary>
        /// Gets the time that the associated process was started.
        /// </summary>
        /// <exception cref="Win32Exception">Thrown when process information cannot be read due to permission limits.</exception>
        DateTime StartTime { get; }

        /// <summary>
        /// Gets a value indicating whether the associated process has been terminated.
        /// </summary>
        bool HasExited { get; }

        /// <summary>
        /// Immediately stops the associated process.
        /// </summary>
        void Kill();

        /// <summary>
        /// Instructs the <see cref="ISystemProcess"/> to wait the specified number of milliseconds for the associated process to exit.
        /// </summary>
        /// <param name="milliseconds">The amount of time, in milliseconds, to wait for the associated process to exit.</param>
        /// <returns><see langword="true"/> if the associated process has exited; otherwise, <see langword="false"/>.</returns>
        bool WaitForExit(int milliseconds);
    }
}

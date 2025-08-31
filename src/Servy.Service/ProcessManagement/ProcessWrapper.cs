using System.Diagnostics;

namespace Servy.Service.ProcessManagement
{
    /// <summary>
    /// Wraps a <see cref="System.Diagnostics.Process"/> to allow abstraction and easier testing.
    /// </summary>
    public class ProcessWrapper : IProcessWrapper
    {
        private readonly Process _process;

        /// <inheritdoc/>
        public IntPtr ProcessHandle => _process.Handle;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessWrapper"/> class with the specified <see cref="ProcessStartInfo"/>.
        /// </summary>
        /// <param name="psi">The process start information.</param>
        public ProcessWrapper(ProcessStartInfo psi)
        {
            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        }

        /// <inheritdoc/>
        public event DataReceivedEventHandler OutputDataReceived
        {
            add { _process.OutputDataReceived += value; }
            remove { _process.OutputDataReceived -= value; }
        }

        /// <inheritdoc/>
        public event DataReceivedEventHandler ErrorDataReceived
        {
            add { _process.ErrorDataReceived += value; }
            remove { _process.ErrorDataReceived -= value; }
        }

        /// <inheritdoc/>
        public event EventHandler Exited
        {
            add { _process.Exited += value; }
            remove { _process.Exited -= value; }
        }

        /// <inheritdoc/>
        public int Id => _process.Id;

        /// <inheritdoc/>
        public bool HasExited => _process.HasExited;

        /// <inheritdoc/>
        public IntPtr Handle => _process.Handle;

        /// <inheritdoc/>
        public int ExitCode => _process.ExitCode;

        /// <inheritdoc/>
        public IntPtr MainWindowHandle => _process.MainWindowHandle;

        /// <inheritdoc/>
        public bool EnableRaisingEvents
        {
            get => _process.EnableRaisingEvents;
            set => _process.EnableRaisingEvents = value;
        }

        /// <inheritdoc/>
        public ProcessPriorityClass PriorityClass
        {
            get => _process.PriorityClass;
            set => _process.PriorityClass = value;
        }

        /// <inheritdoc/>
        public void Start() => _process.Start();

        /// <inheritdoc/>
        public void Kill(bool entireProcessTree = true) => _process.Kill(entireProcessTree);

        /// <inheritdoc/>
        public bool WaitForExit(int milliseconds) => _process.WaitForExit(milliseconds);

        /// <inheritdoc/>
        public void WaitForExit() => _process.WaitForExit();

        /// <inheritdoc/>
        public bool CloseMainWindow() => _process.CloseMainWindow();

        /// <inheritdoc/>
        public void BeginOutputReadLine() => _process.BeginOutputReadLine();

        /// <inheritdoc/>
        public void BeginErrorReadLine() => _process.BeginErrorReadLine();

        /// <inheritdoc/>
        public void Dispose() => _process.Dispose();
    }
}

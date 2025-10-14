using System.Diagnostics;

namespace Servy.Service.ProcessManagement
{
    /// <summary>
    /// Wraps a <see cref="System.Diagnostics.Process"/> to allow abstraction and easier testing.
    /// </summary>
    public class ProcessWrapper : IProcessWrapper
    {
        private readonly Process _process;
        private bool _disposed;

        /// <inheritdoc/>
        public IntPtr ProcessHandle
        {
            get
            {
                ThrowIfDisposed();
                return _process.Handle;
            }
        }

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
        public int Id
        {
            get
            {
                ThrowIfDisposed();
                return _process.Id;
            }
        }

        /// <inheritdoc/>
        public bool HasExited
        {
            get
            {
                ThrowIfDisposed();
                return _process.HasExited;
            }
        }

        /// <inheritdoc/>
        public IntPtr Handle
        {
            get
            {
                ThrowIfDisposed();
                return _process.Handle;
            }
        }

        /// <inheritdoc/>
        public int ExitCode
        {
            get
            {
                ThrowIfDisposed();
                return _process.ExitCode;
            }
        }

        /// <inheritdoc/>
        public IntPtr MainWindowHandle
        {
            get
            {
                ThrowIfDisposed();
                return _process.MainWindowHandle;
            }
        }

        /// <inheritdoc/>
        public bool EnableRaisingEvents
        {
            get
            {
                ThrowIfDisposed();
                return _process.EnableRaisingEvents;
            }
            set
            {
                ThrowIfDisposed();
                _process.EnableRaisingEvents = value;
            }
        }

        /// <inheritdoc/>
        public ProcessPriorityClass PriorityClass
        {
            get
            {
                ThrowIfDisposed();
                return _process.PriorityClass;
            }
            set
            {
                ThrowIfDisposed();
                _process.PriorityClass = value;
            }
        }

        /// <inheritdoc/>
        public void Start()
        {
            ThrowIfDisposed();
            _process.Start();
        }

        /// <inheritdoc/>
        public async Task<bool> WaitUntilHealthyAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var start = DateTime.UtcNow;

            while (DateTime.UtcNow - start < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_process.HasExited)
                    return false; // process exited before becoming healthy

                await Task.Delay(500, cancellationToken);
            }

            return !_process.HasExited;
        }

        /// <inheritdoc/>
        public void Kill(bool entireProcessTree = true)
        {
            ThrowIfDisposed();
            _process.Kill(entireProcessTree);
        }

        /// <inheritdoc/>
        public bool WaitForExit(int milliseconds)
        {
            ThrowIfDisposed();
            return _process.WaitForExit(milliseconds);
        }

        /// <inheritdoc/>
        public void WaitForExit()
        {
            ThrowIfDisposed();
            _process.WaitForExit();
        }

        /// <inheritdoc/>
        public bool CloseMainWindow()
        {
            ThrowIfDisposed();
            return _process.CloseMainWindow();
        }

        /// <inheritdoc/>
        public void BeginOutputReadLine()
        {
            ThrowIfDisposed();
            _process.BeginOutputReadLine();
        }

        /// <inheritdoc/>
        public void BeginErrorReadLine()
        {
            ThrowIfDisposed();
            _process.BeginErrorReadLine();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and optionally managed resources.
        /// </summary>
        /// <param name="disposing">
        /// True if called from <see cref="Dispose()"/>; false if called from a finalizer.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _process.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if this instance has already been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ProcessWrapper));
        }
    }
}

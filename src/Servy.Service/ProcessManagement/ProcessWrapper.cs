using Servy.Service.Helpers;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

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
                    return false;

                await Task.Delay(500, cancellationToken);
            }

            return !_process.HasExited;
        }

        /// <inheritdoc/>
        public void Kill(bool entireProcessTree = true)
        {
            ThrowIfDisposed();

            if (entireProcessTree)
                ProcessHelper.KillProcessTree(_process);
            else
                _process.Kill();
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
        /// Protected dispose pattern implementation.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from a finalizer.</param>
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

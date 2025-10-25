using Servy.Core.Logging;
using Servy.Service.Helpers;
using Servy.Service.Native;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static Servy.Service.Native.NativeMethods;

namespace Servy.Service.ProcessManagement
{
    /// <summary>
    /// Wraps a <see cref="System.Diagnostics.Process"/> to allow abstraction and easier testing.
    /// </summary>
    public class ProcessWrapper : IProcessWrapper
    {
        private readonly Process _process;
        private readonly ILogger _logger;
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
        /// <param name="logger">The logger.</param>
        public ProcessWrapper(ProcessStartInfo psi, ILogger logger)
        {
            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _logger = logger;
        }

        #region Properties and Events

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

        /// <summary>
        /// Standard output stream of the process.
        /// </summary>
        public StreamReader StandardOutput
        {
            get
            {
                ThrowIfDisposed();
                return _process.StandardOutput;
            }
        }

        /// <summary>
        /// Standard error stream of the process.
        /// </summary>
        public StreamReader StandardError
        {
            get
            {
                ThrowIfDisposed();
                return _process.StandardError;
            }
        }

        #endregion

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
        public bool? Stop(int timeoutMs)
        {
            ThrowIfDisposed();

            if (_process.HasExited)
            {
                return null;
            }

            bool? sent = SendCtrlC(_process);
            if (!sent.HasValue)
            {
                return null;
            }

            if (!sent.Value)
            {
                try
                {
                    sent = _process.CloseMainWindow();
                }
                catch (InvalidOperationException)
                {
                    return null;
                }
            }

            if (sent.Value && _process.WaitForExit(timeoutMs))
            {
                return true;
            }

            // Force kill
            _logger?.Info("Graceful shutdown not supported. Forcing kill.");

            try
            {
                ProcessHelper.KillProcessTree(_process);
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Kill failed: {ex.Message}");
            }

            if (!_process.WaitForExit(timeoutMs))
            {
                _logger?.Warning($"Process did not exit within {timeoutMs / 1000.0} seconds after forced kill.");
            }

            return false;
        }

        /// <summary>
        /// Stops the specified process.
        /// </summary>
        /// <param name="process">Process.</param>
        /// <param name="timeoutMs">Timeout in Milliseconds.</param>
        private void StopPrivate(Process process, int timeoutMs)
        {
            _logger?.Info($"Stopping process '{process.Format()}'...");

            if (process.HasExited)
            {
                goto Exited;
            }

            bool? sent = SendCtrlC(process);
            if (!sent.HasValue)
            {
                goto Exited;
            }

            if (!sent.Value)
            {
                try
                {
                    sent = process.CloseMainWindow();
                }
                catch (InvalidOperationException)
                {
                    goto Exited;
                }
            }

            if (sent.Value && process.WaitForExit(timeoutMs))
            {
                _logger?.Info($"Process '{process.Format()}' canceled with code {process.ExitCode}.");
                return;
            }

            _logger?.Info($"Graceful shutdown not supported. Forcing kill: {process.Format()}");
            try
            {
                ProcessHelper.KillProcessTree(process);
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Kill failed: {ex.Message}");
            }

            _logger?.Info($"Process '{process.Format()}' terminated.");
            return;

        Exited:
            _logger?.Info($"Process '{process.Format()}' has already exited.");
        }

        /// <summary>
        /// Stops the specified process and all its descendant processes.
        /// </summary>
        /// <param name="process">Process.</param>
        /// <param name="timeoutMs">Timeout in Milliseconds.</param>
        private void StopTree(Process process, int timeoutMs)
        {
            StopPrivate(process, timeoutMs);

            foreach (var child in process.GetChildren())
            {
                using (child.Process)
                using (child.Handle)
                {
                    StopTree(child.Process, timeoutMs);
                }
            }
        }

        /// <inheritdoc/>
        public void StopDescendants(int timeoutMs)
        {
            ThrowIfDisposed();

            foreach (var child in _process.GetChildren())
            {
                using (child.Process)
                using (child.Handle)
                {
                    StopTree(child.Process, timeoutMs);
                }
            }
        }

        /// <inheritdoc/>
        public string Format()
        {
            ThrowIfDisposed();
            return _process.Format();
        }

        /// <inheritdoc/>
        public void Kill(bool entireProcessTree = false)
        {
            ThrowIfDisposed();
            try
            {
                if (_process.HasExited) return;

                if (entireProcessTree)
                {
                    ProcessHelper.KillProcessTree(_process);
                }
                else
                {
                    _process.Kill();
                }
            }
            catch (Exception ex)
            {
                _logger?.Warning($"Kill failed: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public bool WaitForExit(int milliseconds)
        {
            ThrowIfDisposed();
            if (_process.HasExited) return true;
            return _process.WaitForExit(milliseconds);
        }

        /// <inheritdoc/>
        public void WaitForExit()
        {
            ThrowIfDisposed();
            if (_process.HasExited) return;
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

        /// <summary>
        /// Sends a CTRL+C signal to the specified process.
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        private bool? SendCtrlC(Process process)
        {
            // Save current stdout/stderr
            //IntPtr originalOut = GetStdHandle(STD_OUTPUT_HANDLE);
            //IntPtr originalErr = GetStdHandle(STD_ERROR_HANDLE);

            if (!AttachConsole(process.Id))
            {
                int error = Marshal.GetLastWin32Error();
                switch (error)
                {
                    // The process does not have a console.
                    case Errors.ERROR_INVALID_HANDLE:
                        //_logger?.Info($"Sending Ctrl+C: The child process '{process.Format()}' does not have a console.");
                        return false;

                    // The process has exited.
                    case Errors.ERROR_INVALID_PARAMETER:
                        return null;

                    // The calling process is already attached to a console.
                    default:
                        _logger?.Warning($"Sending Ctrl+C: Failed to attach the child process '{process.Format()}' to console: {new Win32Exception(error).Message}");
                        return false;
                }
            }

            // Don't call GenerateConsoleCtrlEvent immediately after SetConsoleCtrlHandler.
            // A delay was observed as of Windows 10, version 2004 and Windows Server 2019.
            _ = GenerateConsoleCtrlEvent(CtrlEvents.CTRL_C_EVENT, 0);
            _logger?.Info($"Sent Ctrl+C to process '{process.Format()}'.");

            _ = FreeConsole();
            //SetStdHandle(STD_OUTPUT_HANDLE, originalOut);
            //SetStdHandle(STD_ERROR_HANDLE, originalErr);

            return true;
        }

    }
}

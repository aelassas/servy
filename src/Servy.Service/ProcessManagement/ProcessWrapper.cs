using Servy.Core.Logging;
using Servy.Core.Native;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Service.ProcessManagement
{
    /// <summary>
    /// Wraps a <see cref="System.Diagnostics.Process"/> to allow abstraction and easier testing.
    /// </summary>
    public class ProcessWrapper : IProcessWrapper
    {
        private readonly Process _process;
        private readonly IServyLogger? _logger;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessWrapper"/> class with the specified <see cref="ProcessStartInfo"/>.
        /// </summary>
        /// <param name="psi">The process start information.</param>
        /// <param name="logger">The logger.</param>
        public ProcessWrapper(ProcessStartInfo psi, IServyLogger? logger)
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
        public DateTime StartTime
        {
            get
            {
                ThrowIfDisposed();
                return _process.StartTime;
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
        public StreamReader StandardOutput
        {
            get
            {
                ThrowIfDisposed();
                return _process.StandardOutput;
            }
        }

        /// <inheritdoc/>
        public StreamReader StandardError
        {
            get
            {
                ThrowIfDisposed();
                return _process.StandardError;
            }
        }

        /// <inheritdoc/>
        public ProcessStartInfo StartInfo
        {
            get
            {
                ThrowIfDisposed();
                return _process.StartInfo;
            }
        }

        /// <inheritdoc/>
        public Process UnderlyingProcess
        {
            get
            {
                ThrowIfDisposed();
                return _process;
            }
        }

        #endregion

        /// <inheritdoc/>
        public bool Start()
        {
            ThrowIfDisposed();
            return _process.Start();
        }

        /// <inheritdoc/>
        public async Task<bool> WaitForExitOrTimeoutAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
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
                _process.Kill();
            }
            catch (Exception ex)
            {
                _logger?.Warn($"Kill failed: {ex.Message}");
            }

            if (!_process.WaitForExit(timeoutMs))
            {
                _logger?.Warn($"Process did not exit within {timeoutMs / 1000.0} seconds after forced kill.");
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

            void LogProcessExited()
            {
                _logger?.Info($"Process '{process.Format()}' has already exited.");
            }

            if (process.HasExited)
            {
                LogProcessExited();
                return;
            }

            bool? sent = SendCtrlC(process);
            if (!sent.HasValue)
            {
                LogProcessExited();
                return;
            }

            if (!sent.Value)
            {
                try
                {
                    sent = process.CloseMainWindow();
                }
                catch (InvalidOperationException)
                {
                    LogProcessExited();
                    return;
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
                process.Kill();

                // Wait for the kernel to finish cleaning up the process.
                // This ensures the process tree is stable before the caller enumerates children.
                if (!process.WaitForExit(3000))
                {
                    _logger?.Warn($"Process '{process.Format()}' killed, but did not exit within 3s. Moving to children anyway.");
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn($"Kill failed: {ex.Message}");
            }

            _logger?.Info($"Process '{process.Format()}' terminated.");
        }

        /// <summary>
        /// Stops the specified process and all its descendant processes.
        /// </summary>
        /// <param name="process">Process.</param>
        /// <param name="timeoutMs">Timeout in Milliseconds.</param>
        private void StopTree(Process process, int timeoutMs)
        {
            var parentPid = 0;
            var parentStartTime = DateTime.MinValue;
            try
            {
                parentPid = process.Id;
                parentStartTime = process.StartTime;
            }
            catch
            {
                // Process already dead
            }

            // 1. RECURSION: Hunt down grandchildren first
            foreach (var child in ProcessExtensions.GetChildren(parentPid, parentStartTime))
            {
                using (child)
                {
                    _logger?.Info($"Cascading stop to deeper descendant: {child.ProcessName} (PID: {child.Id})...");
                    StopTree(child, timeoutMs);
                }
            }

            // 2. TERMINATION: Kill the current node now that its children are dead
            _logger?.Info($"Terminating node: {process.ProcessName} (PID: {process.Id})");
            StopPrivate(process, timeoutMs);
        }

        /// <inheritdoc/>
        public void StopDescendants(int parentPid, DateTime parentStartTime, int timeoutMs)
        {
            ThrowIfDisposed();

            _logger?.Info($"Scanning for top-level descendants of PID {parentPid}...");

            var children = ProcessExtensions.GetChildren(parentPid, parentStartTime);

            if (children.Count == 0)
            {
                _logger?.Info($"No active descendants found for PID {parentPid}.");
                return;
            }

            foreach (var child in children)
            {
                using (child) // We no longer need to dispose a native Handle, just the Process object
                {
                    _logger?.Info($"Found descendant: {child.ProcessName} (PID: {child.Id}). Initiating cascaded kill...");
                    StopTree(child, timeoutMs);
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
                _process.Kill(entireProcessTree);
            }
            catch (Exception ex)
            {
                _logger?.Warn($"Kill failed: {ex.Message}");
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

        ///<inheritdoc/>
        public void CancelOutputRead()
        {
            ThrowIfDisposed();
            _process.CancelOutputRead();
        }

        ///<inheritdoc/>
        public void CancelErrorRead()
        {
            ThrowIfDisposed();
            _process.CancelErrorRead();
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
        /// Attempts to send a CTRL+C signal to a console-based process to initiate a graceful shutdown.
        /// </summary>
        /// <param name="process">The native process to which the signal will be sent.</param>
        /// <returns>
        /// <see langword="true"/> if the signal was successfully generated; 
        /// <see langword="false"/> if the process does not have a console or the signal could not be sent; 
        /// <see langword="null"/> if the process has already exited.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method temporarily attaches the current process to the target's console using the Win32 
        /// <c>AttachConsole</c> API. While attached, it uses <c>GenerateConsoleCtrlEvent</c> to broadcast 
        /// the CTRL_C_EVENT to the console group.
        /// </para>
        /// <para>
        /// <b>Safety:</b> To prevent the calling service from terminating itself when the signal is broadcast, 
        /// the service's own Ctrl+C handler is suppressed using <c>SetConsoleCtrlHandler(null, true)</c> 
        /// for the duration of the signal generation.
        /// </para>
        /// </remarks>
        private bool? SendCtrlC(Process process)
        {
            // FIX: ALWAYS free the console first to prevent stale locks from previous iterations
            _ = FreeConsole();

            if (!AttachConsole(process.Id))
            {
                int error = Marshal.GetLastWin32Error();

                // ERROR_PIPE_NOT_CONNECTED (233)
                // The child shares the parent's console. It already received the broadcasted Ctrl+C 
                // when the parent was signaled. We return TRUE to force the wrapper to wait for 
                // a graceful exit rather than instantly triggering process.Kill().
                if (error == 233)
                {
                    _logger?.Info($"Process '{process.Format()}' shares a console group. Awaiting graceful shutdown...");
                    return true;
                }

                // ERROR_INVALID_HANDLE (6) or ERROR_GEN_FAILURE (31)
                if (error == Errors.ERROR_INVALID_HANDLE || error == 31)
                {
                    return false;
                }

                if (error == Errors.ERROR_INVALID_PARAMETER)
                {
                    return null; // Process already exited
                }

                _logger?.Warn($"Sending Ctrl+C: Failed to attach to '{process.Format()}': {new Win32Exception(error).Message} (Error: {error})");
                return false;
            }

            // CRITICAL: Temporarily ignore Ctrl+C in the calling process (the service).
            // Passing 'null' as the handler and 'true' as the add flag tells the OS 
            // to ignore CTRL_C_EVENT for this specific process.
            if (!SetConsoleCtrlHandler(null, true))
            {
                int error = Marshal.GetLastWin32Error();
                _logger?.Error($"Failed to suppress console control handlers in the service (Win32 Error: {error}). Aborting signal to prevent service self-termination.");
                _ = FreeConsole();
                return false;
            }

            try
            {
                // CRITICAL: Yield to the OS to allow the handler registration 
                // and console attachment to propagate through conhost.exe before firing the event.
                Thread.Sleep(50);

                // Don't call GenerateConsoleCtrlEvent immediately after SetConsoleCtrlHandler.
                // A delay was observed as of Windows 10, version 2004 and Windows Server 2019.
                // The Win32 API Trap:
                // CTRL_C_EVENT: This signal cannot be limited to a specific process group.
                // If dwProcessGroupId is nonzero, this function will succeed, but
                // the CTRL + C signal will not be received by processes within
                // the specified process group.
                // So passing the specific process group ID instead of 0 will not work.
                _ = GenerateConsoleCtrlEvent(CtrlEvents.CTRL_C_EVENT, 0);
                _logger?.Info($"Sent Ctrl+C to process '{process.Format()}'.");
            }
            finally
            {
                // Detach from the child's console
                _ = FreeConsole();

                // Restore default Ctrl+C handling (remove the ignore flag) for the service process
                if (!SetConsoleCtrlHandler(null, false))
                {
                    int error = Marshal.GetLastWin32Error();
                    _logger?.Error($"Failed to restore console control handlers in the service (Win32 Error: {error}).");
                }
            }

            return true;
        }

    }
}

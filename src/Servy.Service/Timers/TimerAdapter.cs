using System;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Servy.Service.Timers
{
    /// <summary>
    /// Adapter for <see cref="Timer"/>, implementing the <see cref="ITimer"/> interface.
    /// Wraps the <see cref="Timer"/> class to provide a testable abstraction.
    /// </summary>
    public class TimerAdapter : ITimer
    {
        private Timer _timer;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimerAdapter"/> class with the specified interval.
        /// </summary>
        /// <param name="interval">The interval in milliseconds at which to raise the Elapsed event.</param>
        public TimerAdapter(double interval)
        {
            _timer = new Timer(interval);
        }

        /// <inheritdoc/>
        public event ElapsedEventHandler Elapsed
        {
            add { _timer.Elapsed += value; }
            remove { _timer.Elapsed -= value; }
        }

        /// <inheritdoc/>
        public bool AutoReset
        {
            get => _timer.AutoReset;
            set => _timer.AutoReset = value;
        }

        /// <inheritdoc/>
        public void Start()
        {
            ThrowIfDisposed();
            _timer.Start();
        }

        /// <inheritdoc/>
        public void Stop()
        {
            ThrowIfDisposed();
            _timer.Stop();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of the Dispose pattern.
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from a finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Dispose managed resources
                _timer?.Dispose();
                _timer = null;
            }

            _disposed = true;
        }

        /// <summary>
        /// Throws <see cref="ObjectDisposedException"/> if this instance has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TimerAdapter));
        }
    }
}

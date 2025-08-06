using System.Timers;
using Timer = System.Timers.Timer;

namespace Servy.Service
{
    /// <summary>
    /// Adapter for <see cref="Timer"/>, implementing the <see cref="ITimer"/> interface.
    /// Wraps the <see cref="Timer"/> class to provide a testable abstraction.
    /// </summary>
    public class TimerAdapter : ITimer
    {
        private readonly Timer _timer;

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
        public void Start() => _timer.Start();

        /// <inheritdoc/>
        public void Stop() => _timer.Stop();

        /// <inheritdoc/>
        public void Dispose() => _timer.Dispose();
    }
}

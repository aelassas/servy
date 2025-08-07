using System;
using System.Timers;

namespace Servy.Service.Timers
{
    /// <summary>
    /// Represents a timer that raises an event at specified intervals.
    /// </summary>
    public interface ITimer : IDisposable
    {
        /// <summary>
        /// Occurs when the timer interval has elapsed.
        /// </summary>
        event ElapsedEventHandler Elapsed;

        /// <summary>
        /// Gets or sets a value indicating whether the timer should raise the Elapsed event repeatedly.
        /// </summary>
        bool AutoReset { get; set; }

        /// <summary>
        /// Starts raising the Elapsed event by enabling the timer.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops raising the Elapsed event by disabling the timer.
        /// </summary>
        void Stop();
    }
}

namespace Servy.Service.Timers
{
    /// <summary>
    /// Factory interface to create <see cref="ITimer"/> instances.
    /// </summary>
    public interface ITimerFactory
    {
        /// <summary>
        /// Creates a new <see cref="ITimer"/> instance with the specified interval.
        /// </summary>
        /// <param name="intervalInMilliseconds">The timer interval in milliseconds.</param>
        /// <returns>A new <see cref="ITimer"/> instance.</returns>
        ITimer Create(double intervalInMilliseconds);
    }
}

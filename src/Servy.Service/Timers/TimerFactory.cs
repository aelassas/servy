namespace Servy.Service.Timers
{
    /// <summary>
    /// Concrete factory that creates <see cref="ITimer"/> instances
    /// using the <see cref="TimerAdapter"/> implementation.
    /// </summary>
    public class TimerFactory : ITimerFactory
    {
        /// <inheritdoc />
        public ITimer Create(double intervalInMilliseconds)
        {
            return new TimerAdapter(intervalInMilliseconds);
        }
    }
}

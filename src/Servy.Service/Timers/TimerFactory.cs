namespace Servy.Service
{
    public class TimerFactory : ITimerFactory
    {
        public ITimer Create(double intervalInMilliseconds)
        {
            return new TimerAdapter(intervalInMilliseconds);
        }
    }
}

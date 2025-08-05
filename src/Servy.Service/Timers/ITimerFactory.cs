namespace Servy.Service
{
    public interface ITimerFactory
    {
        ITimer Create(double intervalInMilliseconds);
    }
}

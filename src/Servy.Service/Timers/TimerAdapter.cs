using System.Timers;

namespace Servy.Service
{
    public class TimerAdapter : ITimer
    {
        private readonly Timer _timer;

        public TimerAdapter(double interval)
        {
            _timer = new Timer(interval);
        }

        public event ElapsedEventHandler Elapsed
        {
            add { _timer.Elapsed += value; }
            remove { _timer.Elapsed -= value; }
        }

        public bool AutoReset
        {
            get => _timer.AutoReset;
            set => _timer.AutoReset = value;
        }

        public void Start() => _timer.Start();

        public void Stop() => _timer.Stop();

        public void Dispose() => _timer.Dispose();
    }
}

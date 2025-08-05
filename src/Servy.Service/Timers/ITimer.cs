using System;
using System.Timers;

namespace Servy.Service
{
    public interface ITimer : IDisposable
    {
        event ElapsedEventHandler Elapsed;
        bool AutoReset { get; set; }
        void Start();
        void Stop();
    }
}

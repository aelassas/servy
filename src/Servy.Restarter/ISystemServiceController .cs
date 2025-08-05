using System;
using System.ServiceProcess;

namespace Servy.Restarter
{
    public interface ISystemServiceController : IDisposable
    {
        ServiceControllerStatus Status { get; }
        void Start();
        void WaitForStatus(ServiceControllerStatus desiredStatus, TimeSpan timeout);
    }
}

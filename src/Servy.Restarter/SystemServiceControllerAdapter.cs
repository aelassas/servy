using System;
using System.ServiceProcess;

namespace Servy.Restarter
{
    public class SystemServiceControllerAdapter : ISystemServiceController
    {
        private readonly System.ServiceProcess.ServiceController _controller;

        public SystemServiceControllerAdapter(string serviceName)
        {
            _controller = new System.ServiceProcess.ServiceController(serviceName);
        }

        public ServiceControllerStatus Status => _controller.Status;
        public void Start() => _controller.Start();
        public void WaitForStatus(ServiceControllerStatus desiredStatus, TimeSpan timeout) => _controller.WaitForStatus(desiredStatus, timeout);
        public void Dispose() => _controller.Dispose();
    }
}

using Servy.Manager.Models;
using Servy.Manager.Services;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Manager.Design
{
    /// <summary>
    /// Lightweight no-op implementation of IServiceCommands for XAML design-time support.
    /// </summary>
    /// <remarks>
    /// This implementation provides safe defaults to prevent runtime exceptions 
    /// during UI layout and preview sessions in Visual Studio or Blend.
    /// </remarks>
    public class DesignTimeServiceCommands : IServiceCommands
    {
        /// <summary>
        /// Returns an empty list of services to satisfy design-time search bindings.
        /// </summary>
        public Task<List<Service>> SearchServicesAsync(string searchText, bool calculatePerf, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<Service>());
        }

        public Task<bool> StartServiceAsync(Service service, bool showMessageBox = true, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> StopServiceAsync(Service service, bool showMessageBox = true, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> RestartServiceAsync(Service service, bool showMessageBox = true, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task ConfigureServiceAsync(Service service, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> InstallServiceAsync(Service service, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> UninstallServiceAsync(Service service, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> RemoveServiceAsync(Service service, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task ExportServiceToXmlAsync(Service service, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ExportServiceToJsonAsync(Service service, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ImportXmlConfigAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ImportJsonConfigAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CopyPidAsync(Service service, CancellationToken cancellationToken = default) => Task.CompletedTask;

        /// <summary>
        /// No-op implementation of IDisposable for design-time.
        /// </summary>
        public void Dispose()
        {
            // Nothing to release in design-time
        }
    }
}
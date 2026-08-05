using Servy.Services;

namespace Servy.Design
{
    /// <summary>
    /// Lightweight no-op implementation of IServiceCommands for XAML design-time support.
    /// </summary>
    public class DesignTimeServiceCommands : IServiceCommands
    {
        public Task<bool> InstallServiceAsync(Models.ServiceConfiguration config, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> UninstallServiceAsync(string? serviceName, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> StartServiceAsync(string? serviceName, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> StopServiceAsync(string? serviceName, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> RestartServiceAsync(string? serviceName, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task ExportXmlConfigAsync(string? confirmPassword, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ExportJsonConfigAsync(string? confirmPassword, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ImportXmlConfigAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ImportJsonConfigAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task OpenManagerAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

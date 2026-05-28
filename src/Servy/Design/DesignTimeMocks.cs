using Servy.Services;

namespace Servy.Design
{
    /// <summary>
    /// Lightweight no-op implementation of IServiceCommands for XAML design-time support.
    /// </summary>
    public class DesignTimeServiceCommands : IServiceCommands
    {
        public Task<bool> InstallService(Models.ServiceConfiguration config, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> UninstallService(string? serviceName, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> StartService(string? serviceName, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> StopService(string? serviceName, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> RestartService(string? serviceName, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task ExportXmlConfig(string? confirmPassword) => Task.CompletedTask;
        public Task ExportJsonConfig(string? confirmPassword) => Task.CompletedTask;
        public Task ImportXmlConfig() => Task.CompletedTask;
        public Task ImportJsonConfig() => Task.CompletedTask;
        public Task OpenManager() => Task.CompletedTask;
    }
}
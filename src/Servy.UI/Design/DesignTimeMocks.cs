using Servy.Core.Common;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Services;
using Servy.UI.Services;
using System.ServiceProcess;

namespace Servy.UI.Design
{
    /// <summary>
    /// Provides a no-op implementation of process-related utility methods for XAML design-time support.
    /// </summary>
    /// <remarks>
    /// This class inherits from <see cref="ProcessHelper"/> to satisfy dependency requirements 
    /// in ViewModels and validators without invoking real Windows process management logic 
    /// during a design-time session.
    /// </remarks>
    public class DesignTimeProcessHelper : ProcessHelper
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DesignTimeProcessHelper"/> class.
        /// </summary>
        public DesignTimeProcessHelper()
        {
            // Empty constructor to allow instantiation by the XAML designer.
        }
    }

    /// <summary>
    /// Lightweight no-op implementation of IServiceRepository for XAML design-time support.
    /// </summary>
    public class DesignTimeServiceRepository : IServiceRepository
    {
        public ServiceDto? GetByName(string? name) => null;
        public ServiceDto? GetByName(string? name, bool decrypt = true) => null;
        public void Upsert(ServiceDto service) { }
        public void Delete(string name) { }
        public int Update(ServiceDto service, bool updateRuntimeState) => 0;

        public Task<int> AddAsync(ServiceDto service, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> UpdateAsync(ServiceDto service, bool updateRuntimeState, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> UpsertAsync(ServiceDto service, bool updateRuntimeState, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> UpsertBatchAsync(IEnumerable<ServiceDto> services, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> DeleteAsync(string? name, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<ServiceDto?> GetByIdAsync(int id, bool decrypt = true, CancellationToken cancellationToken = default)
            => Task.FromResult<ServiceDto?>(null);

        public Task<ServiceDto?> GetByNameAsync(string? name, bool decrypt = true, CancellationToken cancellationToken = default)
            => Task.FromResult<ServiceDto?>(null);

        public Task<int?> GetServicePidAsync(string? serviceName, CancellationToken cancellationToken = default)
            => Task.FromResult<int?>(null);

        public Task<ServiceConsoleStateDto?> GetServiceConsoleStateAsync(string? serviceName, CancellationToken cancellationToken = default)
            => Task.FromResult<ServiceConsoleStateDto?>(null);

        public Task<IEnumerable<ServiceDto>> GetAllAsync(bool decrypt = true, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<ServiceDto>());

        public Task<IEnumerable<ServiceDto>> SearchAsync(string keyword, bool decrypt = true, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<ServiceDto>());

        public Task<string> ExportXmlAsync(string? name, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<bool> ImportXmlAsync(string xml, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<string> ExportJsonAsync(string? name, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<bool> ImportJsonAsync(string json, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    /// <summary>
    /// Provides a no-op implementation of <see cref="IServiceManager"/> for XAML design-time support.
    /// </summary>
    /// <remarks>
    /// This implementation satisfies dependency requirements in ViewModels and commands without 
    /// invoking real Service Control Manager (SCM) logic, ensuring stability in Visual Studio and Blend.
    /// </remarks>
    public class DesignTimeServiceManager : IServiceManager
    {
        /// <summary>
        /// Returns a successful operation result for design-time installation simulation.
        /// </summary>
        public Task<OperationResult> InstallServiceAsync(InstallServiceOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Success());

        /// <summary>
        /// Returns a successful operation result for design-time uninstallation simulation.
        /// </summary>
        public Task<OperationResult> UninstallServiceAsync(string? serviceName, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Success());

        /// <summary>
        /// Returns a successful operation result for design-time start simulation.
        /// </summary>
        public Task<OperationResult> StartServiceAsync(string? serviceName, bool logSuccessfulStart = true, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Success());

        /// <summary>
        /// Returns a successful operation result for design-time stop simulation.
        /// </summary>
        public Task<OperationResult> StopServiceAsync(string? serviceName, bool logSuccessfulStop = true, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Success());

        /// <summary>
        /// Returns a successful operation result for design-time restart simulation.
        /// </summary>
        public Task<OperationResult> RestartServiceAsync(string? serviceName, bool logSuccessfulRestart = true, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Success());

        /// <summary>
        /// Returns a default "Stopped" status for design-time display.
        /// </summary>
        public ServiceControllerStatus GetServiceStatus(string? serviceName, CancellationToken cancellationToken = default)
            => ServiceControllerStatus.Stopped;

        /// <summary>
        /// Returns false for design-time checks to simplify the initial layout.
        /// </summary>
        public bool IsServiceInstalled(string? serviceName) => false;

        /// <summary>
        /// Returns Manual startup type as a safe default for design-time.
        /// </summary>
        public ServiceStartType? GetServiceStartupType(string? serviceName, CancellationToken cancellationToken = default)
            => ServiceStartType.Manual;

        /// <summary>
        /// Returns an empty list to avoid rendering overhead in the designer.
        /// </summary>
        public List<ServiceInfo> GetAllServices(CancellationToken cancellationToken = default)
            => new List<ServiceInfo>();

        /// <summary>
        /// Returns null to avoid recursive dependency resolution during design-time.
        /// </summary>
        public ServiceDependencyNode? GetDependencies(string? serviceName) => null;
    }

    /// <summary>
    /// Lightweight no-op implementation of IHelpService for XAML design-time support.
    /// </summary>
    public class DesignTimeHelpService : IHelpService
    {
        // Standard void no-ops
        public void OpenDocumentation() { }
        public void OpenAboutDialog() { }

        // Standard Task no-ops
        public Task CheckUpdatesAsync() => Task.CompletedTask;

        /// <summary>
        /// No-op implementation for opening documentation with a specific caption.
        /// </summary>
        public Task OpenDocumentation(string caption)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// No-op implementation for update checks triggered by the designer.
        /// </summary>
        public Task CheckUpdates(string caption)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// No-op implementation for the about dialog preview.
        /// </summary>
        public Task OpenAboutDialog(string about, string caption)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Lightweight no-op implementation of IFileDialogService for XAML design-time support.
    /// </summary>
    /// <remarks>
    /// This implementation returns null for all path-related queries to satisfy 
    /// ViewModel initialization without triggering native Windows dialogs or exceptions.
    /// </remarks>
    public class DesignTimeFileDialogService : IFileDialogService
    {
        public string? OpenExecutable() => null;

        public string? OpenFolder() => null;

        public string? OpenJson() => null;

        public string? OpenXml() => null;

        public string? SaveFile(string title) => null;

        public string? SaveJson(string title) => null;

        public string? SaveXml(string title) => null;
    }

    /// <summary>
    /// Lightweight no-op implementation of IMessageBoxService for XAML design-time support.
    /// </summary>
    /// <remarks>
    /// This implementation prevents ArgumentNullExceptions during ViewModel initialization
    /// and ensures the designer process does not hang or crash if a message box is triggered.
    /// </remarks>
    public class DesignTimeMessageBoxService : IMessageBoxService
    {
        /// <summary>
        /// Returns a successful confirmation result by default to allow designer logic to proceed.
        /// </summary>
        public Task<bool> ShowConfirmAsync(string? message, string caption)
        {
            return Task.FromResult(true);
        }

        public Task ShowErrorAsync(string? message, string caption)
        {
            return Task.CompletedTask;
        }

        public Task ShowInfoAsync(string? message, string caption)
        {
            return Task.CompletedTask;
        }

        public Task ShowWarningAsync(string? message, string caption)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Lightweight no-op implementation of ICursorService for XAML design-time support.
    /// </summary>
    public class DesignTimeCursorService : ICursorService
    {
        /// <summary>
        /// No-op implementation for the designer.
        /// </summary>
        public void ResetCursor() { }

        /// <summary>
        /// Returns a dummy disposable to satisfy 'using' blocks in ViewModels.
        /// </summary>
        public IDisposable SetWaitCursor()
        {
            return new NoOpDisposable();
        }

        private class NoOpDisposable : IDisposable
        {
            public void Dispose()
            {
                // Do nothing
            }
        }
    }

    /// <summary>
    /// Lightweight no-op implementation of IUiDispatcher for XAML design-time support.
    /// </summary>
    /// <remarks>
    /// This implementation provides a safe way to bypass UI threading requirements during 
    /// layout sessions, preventing the "Design-Time Trap" where constructors fail due 
    /// to missing dispatcher contexts.
    /// </remarks>
    public class DesignTimeUiDispatcher : IUiDispatcher
    {
        /// <summary>
        /// Immediately returns a completed task to satisfy the yielding requirement 
        /// without disrupting the design-time environment.
        /// </summary>
        /// <returns>A completed <see cref="Task"/>.</returns>
        public Task YieldAsync() => Task.CompletedTask;
    }

}
using Servy.Core.Common;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Services;
using Servy.UI.Services;
using System.ServiceProcess;
using System.Windows.Threading;

namespace Servy.UI.Design
{
    /// <summary>
    /// Provides a concrete <see cref="ProcessHelper"/> subtype used purely so the designer can instantiate a stand-in
    /// (the real metric methods are simply never invoked at design time).
    /// </summary>
    /// <remarks>
    /// This class inherits from <see cref="ProcessHelper"/> to satisfy dependency requirements
    /// in ViewModels and validators without invoking real Windows process management logic
    /// during a design-time session.
    /// </remarks>
    public class DesignTimeProcessHelper : ProcessHelper
    {
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
        public ServiceDto? GetByName(string? name, bool decrypt = true) => null;
        public void Upsert(ServiceDto service) { /* no-op */ }
        public void Delete(string name) { /* no-op */ }
        public int Update(ServiceDto service, bool preserveExistingRuntimeState, bool preserveExistingCredentials) => 0;

        public Task<int> AddAsync(ServiceDto service, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> UpdateAsync(ServiceDto service, bool preserveExistingRuntimeState, bool preserveExistingCredentials, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> UpsertAsync(ServiceDto service, bool preserveExistingRuntimeState, bool preserveExistingCredentials, CancellationToken cancellationToken = default)
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

        public Task<int?> GetServicePidAsync(string? name, CancellationToken cancellationToken = default)
            => Task.FromResult<int?>(null);

        public Task<ServiceConsoleStateDto?> GetServiceConsoleStateAsync(string? name, CancellationToken cancellationToken = default)
            => Task.FromResult<ServiceConsoleStateDto?>(null);

        public Task<IEnumerable<ServiceDto>> GetAllAsync(bool decrypt = true, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<ServiceDto>());

        public Task<IEnumerable<ServiceDto>> SearchAsync(string? keyword, bool decrypt = true, CancellationToken cancellationToken = default)
            => Task.FromResult(Enumerable.Empty<ServiceDto>());

        public Task<string> ExportXmlAsync(string? name, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<OperationResult> ImportXmlAsync(string xml, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Success());

        public Task<string> ExportJsonAsync(string? name, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<OperationResult> ImportJsonAsync(string json, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Success());
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
        public Task<OperationResult> InstallServiceAsync(InstallServiceOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Success());

        public Task<OperationResult> UninstallServiceAsync(string? serviceName, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Success());

        public Task<OperationResult> StartServiceAsync(string? serviceName, bool logSuccessfulStart = true, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Success());

        public Task<OperationResult> StopServiceAsync(string? serviceName, bool logSuccessfulStop = true, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Success());

        public Task<OperationResult> RestartServiceAsync(string? serviceName, bool logSuccessfulRestart = true, CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Success());

        public ServiceControllerStatus? GetServiceStatus(string? serviceName, CancellationToken cancellationToken = default)
            => ServiceControllerStatus.Stopped;

        public bool IsServiceInstalled(string? serviceName, CancellationToken cancellationToken = default) => false;

        public ServiceStartType GetServiceStartupType(string? serviceName, CancellationToken cancellationToken = default)
            => ServiceStartType.Manual;

        public List<ServiceInfo> GetAllServices(CancellationToken cancellationToken = default)
            => new List<ServiceInfo>();

        /// <summary>
        /// Returns null to avoid recursive dependency resolution during design-time.
        /// </summary>
        public ServiceDependencyNode? GetDependencies(string? serviceName, CancellationToken cancellationToken = default) => null;
    }

    /// <summary>
    /// Lightweight no-op implementation of IHelpService for XAML design-time support.
    /// </summary>
    public class DesignTimeHelpService : IHelpService
    {
        public Task OpenDocumentationAsync(string caption)
        {
            return Task.CompletedTask;
        }

        public Task CheckUpdatesAsync(string caption)
        {
            return Task.CompletedTask;
        }

        public Task OpenAboutDialogAsync(string about, string caption)
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
        public string? OpenExecutable(string? title = null) => null;

        public string? OpenFolder(string? title = null) => null;

        public string? OpenJson(string? title = null) => null;

        public string? OpenXml(string? title = null) => null;

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
        public void ResetCursor() { /* no-op */ }

        public void SetWaitCursor() { /* no-op */ }
    }

    /// <summary>
    /// Lightweight no-op implementation of <see cref="IUiDispatcher"/> for XAML design-time support.
    /// </summary>
    /// <remarks>
    /// This implementation provides a safe way to bypass UI threading requirements during
    /// layout sessions, preventing the "Design-Time Trap" where constructors fail due
    /// to missing dispatcher contexts in the Visual Studio or Rider designer.
    /// </remarks>
    public class DesignTimeUiDispatcher : IUiDispatcher
    {
        public Task InvokeAsync(Action action) => Task.CompletedTask;

        public Task InvokeAsync(Action action, DispatcherPriority priority) => Task.CompletedTask;

        public Task<T> InvokeAsync<T>(Func<T> callback)
        {
            // Task.FromResult requires a value.
            // Returning default(T) allows the caller to proceed without a NullReferenceException.
            return Task.FromResult(default(T)!);
        }

        public Task YieldAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// Lightweight no-op implementation of <see cref="IEventLogService"/> for XAML design-time support.
    /// </summary>
    public class DesignTimeEventLogService : IEventLogService
    {
        public Task<IEnumerable<ServyEventLogEntry>> SearchAsync(EventLogLevel? level, DateTime? startDate, DateTime? endDate, string? keyword, CancellationToken token = default)
        {
            return Task.FromResult(Enumerable.Empty<ServyEventLogEntry>());
        }
    }
}

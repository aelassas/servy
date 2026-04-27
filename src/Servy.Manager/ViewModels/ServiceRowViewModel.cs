using Servy.Core.Enums;
using Servy.Core.Logging;
using Servy.Manager.Models;
using Servy.Manager.Services;
using Servy.UI.Commands;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Servy.Manager.ViewModels
{
    /// <summary>
    /// ViewModel representing a single row in the Services DataGrid.
    /// Exposes the underlying Service model and row-level commands.
    /// </summary>
    public class ServiceRowViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IServiceCommands _serviceCommands;
        private bool _isSelected;
        private bool _isChecked;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of <see cref="ServiceRowViewModel"/>.
        /// </summary>
        /// <param name="service">The service model for this row.</param>
        /// <param name="serviceCommands">Service commands for row operations.</param>
        public ServiceRowViewModel(Service? service, IServiceCommands? serviceCommands)
        {
            Service = service ?? throw new ArgumentNullException(nameof(service));
            _serviceCommands = serviceCommands ?? throw new ArgumentNullException(nameof(serviceCommands));

            // Subscribe to property changes in the Service model
            Service.PropertyChanged += Service_PropertyChanged;

            StartCommand = new AsyncCommand(StartServiceAsync, CanExecuteServiceCommand);
            StopCommand = new AsyncCommand(StopServiceAsync, CanExecuteServiceCommand);
            RestartCommand = new AsyncCommand(RestartServiceAsync, CanExecuteServiceCommand);
            ConfigureCommand = new AsyncCommand(ConfigureServiceAsync);
            InstallCommand = new AsyncCommand(InstallServiceAsync, CanExecuteServiceCommand);
            UninstallCommand = new AsyncCommand(UninstallServiceAsync, CanExecuteServiceCommand);
            RemoveCommand = new AsyncCommand(RemoveServiceAsync, CanExecuteServiceCommand);
            ExportXmlCommand = new AsyncCommand(ExportServiceToXmlAsync, CanExecuteServiceCommand);
            ExportJsonCommand = new AsyncCommand(ExportServiceToJsonAsync, CanExecuteServiceCommand);

            CopyPidCommand = new AsyncCommand(CopyPidAsync, CanExecuteServiceCommand);
        }

        #region Properties

        /// <summary>
        /// The underlying service model.
        /// </summary>
        public Service? Service { get; }

        /// <summary>
        /// Gets or sets whether this service row is selected in the UI.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether this service row is checked (for bulk operations).
        /// </summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Name => Service?.Name ?? string.Empty;
        public string Description => Service?.Description ?? string.Empty;
        public ServiceStatus? Status => Service?.Status;
        public ServiceStartType? StartupType => Service?.StartupType;
        public string LogOnAs => Service?.LogOnAs ?? string.Empty;
        public bool IsInstalled => Service?.IsInstalled ?? false;
        public bool IsDesktopAppAvailable => Service?.IsDesktopAppAvailable ?? false;
        public int? Pid => Service?.Pid;
        public bool IsPidEnabled => Service?.IsPidEnabled ?? false;
        public double? CpuUsage => Service?.CpuUsage;
        public long? RamUsage => Service?.RamUsage;

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event.
        /// </summary>
        /// <param name="propertyName">Name of the changed property.</param>
        private void OnPropertyChanged([CallerMemberName] string? propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Handles property changes in the underlying <see cref="Service"/> and forwards them to the UI.
        /// </summary>
        private void Service_PropertyChanged(object? sender, PropertyChangedEventArgs? e)
        {
            if (e == null) return;

            // Forward all property changes from Service to ViewModel
            switch (e.PropertyName)
            {
                case nameof(Service.Description):
                    OnPropertyChanged(nameof(Description));
                    break;
                case nameof(Service.Status):
                    OnPropertyChanged(nameof(Status));
                    break;
                case nameof(Service.StartupType):
                    OnPropertyChanged(nameof(StartupType));
                    break;
                case nameof(Service.LogOnAs):
                    OnPropertyChanged(nameof(LogOnAs));
                    break;
                case nameof(Service.IsInstalled):
                    OnPropertyChanged(nameof(IsInstalled));
                    break;
                case nameof(Service.IsDesktopAppAvailable):
                    OnPropertyChanged(nameof(IsDesktopAppAvailable));
                    break;
                case nameof(Service.Name):
                    OnPropertyChanged(nameof(Name));
                    break;
                default:
                    OnPropertyChanged(e.PropertyName);
                    break;
            }

            // RE-EVALUATE COMMANDS: 
            // If status or installation state changes, tell the commands to re-check CanExecute.
            // This ensures buttons in the UI reflect the current OS state immediately.
            if (e.PropertyName == nameof(Service.Status) || e.PropertyName == nameof(Service.IsInstalled))
            {
                (StartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
                (StopCommand as AsyncCommand)?.RaiseCanExecuteChanged();
                (RestartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
                (InstallCommand as AsyncCommand)?.RaiseCanExecuteChanged();
                (UninstallCommand as AsyncCommand)?.RaiseCanExecuteChanged();
                (RemoveCommand as AsyncCommand)?.RaiseCanExecuteChanged();
                (ExportXmlCommand as AsyncCommand)?.RaiseCanExecuteChanged();
                (ExportJsonCommand as AsyncCommand)?.RaiseCanExecuteChanged();
                (CopyPidCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            }
        }

        #endregion

        #region Row-level Commands

        /// <summary>
        /// Command to start the service.
        /// </summary>
        public IAsyncCommand StartCommand { get; }

        /// <summary>
        /// Command to stop the service.
        /// </summary>
        public IAsyncCommand StopCommand { get; }

        /// <summary>
        /// Command to restart the service.
        /// </summary>
        public IAsyncCommand RestartCommand { get; }

        /// <summary>
        /// Command to open the configuration for the service.
        /// </summary>
        public IAsyncCommand ConfigureCommand { get; }

        /// <summary>
        /// Command to install the service.
        /// </summary>
        public IAsyncCommand InstallCommand { get; }

        /// <summary>
        /// Command to uninstall the service.
        /// </summary>
        public IAsyncCommand UninstallCommand { get; }

        /// <summary>
        /// Command to remove the service from the UI grid.
        /// </summary>
        public IAsyncCommand RemoveCommand { get; }

        /// <summary>
        /// Command to export the service definition to XML.
        /// </summary>
        public IAsyncCommand ExportXmlCommand { get; }

        /// <summary>
        /// Command to export the service definition to JSON.
        /// </summary>
        public IAsyncCommand ExportJsonCommand { get; }

        /// <summary>
        /// Command to copy PID to clipboard.
        /// </summary>
        public IAsyncCommand CopyPidCommand { get; }

        #endregion

        #region Command Handlers

        private async Task StartServiceAsync(object? parameter) =>
            await ExecuteSafeAsync(() => _serviceCommands.StartServiceAsync(Service));

        private async Task StopServiceAsync(object? parameter) =>
            await ExecuteSafeAsync(() => _serviceCommands.StopServiceAsync(Service));

        private async Task RestartServiceAsync(object? parameter) =>
            await ExecuteSafeAsync(() => _serviceCommands.RestartServiceAsync(Service));

        private async Task ConfigureServiceAsync(object? parameter) =>
            await ExecuteSafeAsync(() => _serviceCommands.ConfigureServiceAsync(Service));

        private async Task InstallServiceAsync(object? parameter) =>
            await ExecuteSafeAsync(() => _serviceCommands.InstallServiceAsync(Service));

        private async Task UninstallServiceAsync(object? parameter) =>
            await ExecuteSafeAsync(() => _serviceCommands.UninstallServiceAsync(Service));

        private async Task RemoveServiceAsync(object? parameter) =>
            await ExecuteSafeAsync(() => _serviceCommands.RemoveServiceAsync(Service));

        private async Task ExportServiceToXmlAsync(object? parameter) =>
            await ExecuteSafeAsync(() => _serviceCommands.ExportServiceToXmlAsync(Service));

        private async Task ExportServiceToJsonAsync(object? parameter) =>
            await ExecuteSafeAsync(() => _serviceCommands.ExportServiceToJsonAsync(Service));

        private async Task CopyPidAsync(object? parameter) =>
            await ExecuteSafeAsync(() => _serviceCommands.CopyPid(Service));

        #endregion

        #region Helpers

        /// <summary>
        /// Determines if a service command can execute.
        /// </summary>
        /// <param name="parameter">Optional command parameter.</param>
        /// <returns>True if service is valid; otherwise false.</returns>
        private bool CanExecuteServiceCommand(object? parameter)
        {
            return Service != null && !string.IsNullOrWhiteSpace(Service.Name);
        }

        /// <summary>
        /// Executes the given asynchronous action safely and logs any exceptions.
        /// </summary>
        /// <param name="action">The asynchronous action to execute.</param>
        private async Task ExecuteSafeAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                Logger.Error($"Service command failed for {Service?.Name}.", ex);
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// Unsubscribes from Model events to prevent memory leaks.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="ServiceRowViewModel"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources; 
        /// <c>false</c> to release only unmanaged resources.
        /// </param>
        /// <remarks>
        /// This method is part of the standard <see cref="IDisposable"/> pattern. 
        /// It is critical for breaking the strong reference held by the <see cref="Service"/> model 
        /// through the <see cref="Service.PropertyChanged"/> event. Without this unsubscription, 
        /// the ViewModel would remain rooted in memory, leading to a leak.
        /// </remarks>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && Service != null)
                {
                    // CRITICAL: Unsubscribe to release the reference held by the Service model.
                    // This allows the Garbage Collector to reclaim this ViewModel instance.
                    Service.PropertyChanged -= Service_PropertyChanged;
                }

                // Mark as disposed to prevent redundant disposal logic
                _disposed = true;
            }
        }

        #endregion

    }
}

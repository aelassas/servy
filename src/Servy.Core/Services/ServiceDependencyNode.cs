using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Servy.Core.Services
{
    /// <summary>
    /// Represents a node in a Windows service dependency tree.
    /// Implements INotifyPropertyChanged to support live UI updates.
    /// </summary>
    public sealed class ServiceDependencyNode : INotifyPropertyChanged
    {

        #region Fields

        private string? _displayName;
        private bool? _isRunning;

        #endregion

        #region Events

        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the internal service name as registered with the
        /// Windows Service Control Manager.
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// Gets or sets the human-readable display name.
        /// </summary>
        public string DisplayName
        {
            get => _displayName?? ServiceName;
            set => SetProperty(ref _displayName, value);
        }

        /// <summary>
        /// Gets or sets a flag indicating if the service is running.
        /// </summary>
        public bool IsRunning
        {
            get => _isRunning?? false;
            set => SetProperty(ref _isRunning, value);
        }

        /// <summary>
        /// Gets the collection of services that this service
        /// directly depends on.
        /// </summary>
        public ObservableCollection<ServiceDependencyNode> Dependencies { get; } = new ObservableCollection<ServiceDependencyNode>();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="ServiceDependencyNode"/> class.
        /// </summary>
        /// <param name="serviceName">
        /// The internal service name.
        /// </param>
        /// <param name="displayName">
        /// The display name of the service.
        /// </param>
        /// <param name="isRunning">
        /// Indicates if the service is running.
        /// </param>
        public ServiceDependencyNode(string serviceName, string displayName, bool isRunning = false)
        {
            ServiceName = serviceName;
            DisplayName = displayName;
            IsRunning = isRunning;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Standard helper to update field and raise property change event.
        /// </summary>
        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}

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
        private bool? _isExpanded;

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
            get => _displayName ?? ServiceName;
            set => SetProperty(ref _displayName, value);
        }

        /// <summary>
        /// Gets or sets a flag indicating if the service is running.
        /// </summary>
        public bool IsRunning
        {
            get => _isRunning ?? false;
            set => SetProperty(ref _isRunning, value);
        }

        /// <summary>
        /// Gets or sets a flag indicating if the service node is expanded.
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded ?? false;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>
        /// Gets the collection of services that this service
        /// directly depends on.
        /// </summary>
        public ObservableCollection<ServiceDependencyNode> Dependencies { get; } = new ObservableCollection<ServiceDependencyNode>();

        /// <summary>
        /// Gets a value indicating whether this node represents a circular dependency 
        /// that has already appeared higher in the current branch of the tree.
        /// </summary>
        public bool IsCycle { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceDependencyNode"/> class.
        /// </summary>
        /// <param name="serviceName">The internal service name used by the system.</param>
        /// <param name="displayName">The friendly display name of the service.</param>
        /// <param name="isRunning">Indicates whether the service is currently in a running state.</param>
        /// <param name="isCycle">
        /// Set to <see langword="true"/> if this service creates a dependency loop; 
        /// this prevents further recursive discovery of child dependencies.
        /// </param>
        public ServiceDependencyNode(string serviceName, string displayName, bool isRunning = false, bool isCycle = false)
        {
            ServiceName = serviceName;
            DisplayName = displayName;
            IsRunning = isRunning;
            IsCycle = isCycle;
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

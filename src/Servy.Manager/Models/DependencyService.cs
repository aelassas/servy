using Servy.Core.Services;

namespace Servy.Manager.Models
{
    /// <summary>
    /// Represents a Windows service being tracked for console tailing.
    /// </summary>
    public class DependencyService : ServiceItemBase
    {
        /// <summary>
        /// Service dependency tree.
        /// </summary>
        public ServiceDependencyNode Dependencies { get; set; }

    }
}
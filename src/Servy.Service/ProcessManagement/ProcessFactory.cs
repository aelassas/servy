using System.Diagnostics;

namespace Servy.Service
{
    /// <summary>
    /// Concrete factory to create <see cref="IProcessWrapper"/> instances.
    /// </summary>
    public class ProcessFactory : IProcessFactory
    {
        /// <inheritdoc/>
        public IProcessWrapper Create(ProcessStartInfo startInfo)
        {
            return new ProcessWrapper(startInfo);
        }
    }
}

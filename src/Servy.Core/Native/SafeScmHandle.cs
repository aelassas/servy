using System.Diagnostics.CodeAnalysis;

namespace Servy.Core.Native
{
    /// <summary>
    /// Represents a safe wrapper around a Service Control Manager (SCM) database handle.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class SafeScmHandle : SafeCloseServiceHandleBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SafeScmHandle"/> class for P/Invoke marshalling or manual allocation.
        /// </summary>
        public SafeScmHandle() { }
    }
}
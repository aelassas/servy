using System.Diagnostics.CodeAnalysis;

namespace Servy.Core.Native
{
    /// <summary>
    /// Represents a safe wrapper around a Windows Service handle.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class SafeServiceHandle : SafeCloseServiceHandleBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SafeServiceHandle"/> class for P/Invoke marshalling or manual allocation.
        /// </summary>
        public SafeServiceHandle() { }
    }
}
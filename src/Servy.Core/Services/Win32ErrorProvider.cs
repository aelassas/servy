using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Servy.Core.Services
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public class Win32ErrorProvider : IWin32ErrorProvider
    {
        /// <inheritdoc />
        public int GetLastWin32Error() => Marshal.GetLastWin32Error();
    }
}

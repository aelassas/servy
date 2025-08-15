namespace Servy.Core.Services
{
    /// <summary>
    /// Provides access to the last Win32 error code.
    /// </summary>
    public interface IWin32ErrorProvider
    {
        /// <summary>
        /// Gets the last error code set by a Win32 API call.
        /// </summary>
        /// <returns>The last Win32 error code.</returns>
        int GetLastWin32Error();
    }
}

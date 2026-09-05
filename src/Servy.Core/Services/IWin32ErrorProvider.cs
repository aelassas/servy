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
        /// <remarks>
        /// The value is per thread and is valid only until the next call that performs a P/Invoke, so read it
        /// immediately after the call whose failure it explains.
        /// </remarks>
        /// <returns>The last Win32 error code.</returns>
        int GetLastWin32Error();
    }
}

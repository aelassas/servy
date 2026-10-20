namespace Servy.Core.Native
{
    /// <summary>
    /// The Windows Restart Manager calls <see cref="Helpers.HandleHelper"/> makes to find the processes holding a file
    /// open. Each method returns the Win32 error code of the call, 0 on success.
    /// </summary>
    /// <remarks>
    /// Production uses <see cref="RestartManagerApi"/>. Tests substitute a fake so the buffer-growth, error and
    /// session-cleanup paths run without depending on what the operating system happens to report.
    /// </remarks>
    internal interface IRestartManagerApi
    {
        /// <summary>
        /// Starts a Restart Manager session (<c>RmStartSession</c>).
        /// </summary>
        /// <param name="sessionHandle">Receives the handle of the new session.</param>
        /// <returns>The Win32 error code of the call.</returns>
        int StartSession(out uint sessionHandle);

        /// <summary>
        /// Registers one file with the session (<c>RmRegisterResources</c>).
        /// </summary>
        /// <param name="sessionHandle">The session handle.</param>
        /// <param name="filePath">The full path of the file.</param>
        /// <returns>The Win32 error code of the call.</returns>
        int RegisterFile(uint sessionHandle, string filePath);

        /// <summary>
        /// Lists the processes using the registered file (<c>RmGetList</c>).
        /// </summary>
        /// <param name="sessionHandle">The session handle.</param>
        /// <param name="needed">Receives the number of entries the complete list needs.</param>
        /// <param name="count">On input, the length of <paramref name="processes"/>; on output, the number of entries written.</param>
        /// <param name="processes">The buffer that receives the entries, or <see langword="null"/> to ask for the size only.</param>
        /// <returns>The Win32 error code of the call; <see cref="Errors.ERROR_MORE_DATA"/> when the buffer is too small.</returns>
        int GetList(uint sessionHandle, out uint needed, ref uint count, NativeMethods.RM_PROCESS_INFO[]? processes);

        /// <summary>
        /// Ends the session (<c>RmEndSession</c>).
        /// </summary>
        /// <param name="sessionHandle">The session handle.</param>
        /// <returns>The Win32 error code of the call.</returns>
        int EndSession(uint sessionHandle);
    }
}

using Servy.Core.Native;

namespace Servy.Service.Native
{
    /// <summary>
    /// Seam over the process-wide console calls and the Service Control Manager (SCM) calls that
    /// <see cref="Servy.Service.Service"/> would otherwise make as direct P/Invokes, so that its
    /// start-up path, its PRESHUTDOWN registration and its status reporting can be observed from a test.
    /// </summary>
    /// <remarks>
    /// The production implementation is <see cref="ScmNative"/>. It forwards every member unchanged to
    /// <see cref="NativeMethods"/> and <see cref="System.Runtime.InteropServices.Marshal"/>, with the
    /// same arguments, in the same order and on the calling thread, so a service wired to it behaves
    /// exactly as it did when the calls were inline.
    /// </remarks>
    public interface IScmNative
    {
        /// <summary>
        /// Detaches the current process from its console and makes it ignore CTRL+C, by calling
        /// <c>FreeConsole</c> and then <c>SetConsoleCtrlHandler</c> with a null handler and <c>add</c>
        /// set to <see langword="true"/>.
        /// </summary>
        /// <remarks>
        /// Both return values are discarded, exactly as the two inline calls this replaces did.
        /// </remarks>
        void DetachConsole();

        /// <summary>
        /// Reports the specified status to the SCM by calling the Win32 <c>SetServiceStatus</c> API.
        /// </summary>
        /// <param name="handle">The native service status handle to report against.</param>
        /// <param name="status">The status structure to publish.</param>
        /// <returns><see langword="true"/> when the call succeeded; otherwise <see langword="false"/>.</returns>
        bool SetServiceStatus(IntPtr handle, ref NativeMethods.SERVICE_STATUS status);

        /// <summary>
        /// Gets the Win32 error code the last native call on this thread set.
        /// </summary>
        /// <returns>
        /// The value of <see cref="System.Runtime.InteropServices.Marshal.GetLastWin32Error"/>.
        /// </returns>
        int GetLastWin32Error();
    }
}

using Servy.Core.Native;
using System;

namespace Servy.Service.Native
{
    /// <summary>
    /// Seam over the process-wide console calls that
    /// <see cref="Servy.Service.ProcessManagement.ProcessWrapper"/> would otherwise make as direct
    /// P/Invokes when it delivers a CTRL+C to a child's console, so that the delivery sequence and
    /// each of its failure arms can be observed from a test without signalling the test host's own
    /// console group.
    /// </summary>
    /// <remarks>
    /// The production implementation is <see cref="ConsoleCtrlNative"/>. It forwards every member
    /// unchanged to <see cref="NativeMethods"/> and
    /// <see cref="System.Runtime.InteropServices.Marshal"/>, with the same arguments, in the same
    /// order and on the calling thread, so a wrapper left on the default implementation behaves
    /// exactly as it did when the calls were inline.
    /// </remarks>
    internal interface IConsoleCtrlNative
    {
        /// <summary>
        /// Detaches the calling process from its console by calling <c>FreeConsole</c>.
        /// </summary>
        /// <returns><see langword="true"/> when the call succeeded; otherwise <see langword="false"/>.</returns>
        bool FreeConsole();

        /// <summary>
        /// Attaches the calling process to the console of the specified process by calling
        /// <c>AttachConsole</c>.
        /// </summary>
        /// <param name="processId">The identifier of the process whose console to attach to.</param>
        /// <returns><see langword="true"/> when the call succeeded; otherwise <see langword="false"/>.</returns>
        bool AttachConsole(int processId);

        /// <summary>
        /// Adds or removes a console control handler by calling <c>SetConsoleCtrlHandler</c>.
        /// </summary>
        /// <param name="handlerRoutine">
        /// The handler to add or remove; <see cref="IntPtr.Zero"/> selects the process-wide
        /// ignore-CTRL+C flag rather than a routine.
        /// </param>
        /// <param name="add">
        /// <see langword="true"/> to add the handler; <see langword="false"/> to remove it.
        /// </param>
        /// <returns><see langword="true"/> when the call succeeded; otherwise <see langword="false"/>.</returns>
        bool SetConsoleCtrlHandler(IntPtr handlerRoutine, bool add);

        /// <summary>
        /// Sends a console control event to a console process group by calling
        /// <c>GenerateConsoleCtrlEvent</c>.
        /// </summary>
        /// <param name="ctrlEvent">The control event to send.</param>
        /// <param name="processGroupId">
        /// The console process group to signal; <c>0</c> means every process sharing the console.
        /// </param>
        /// <returns><see langword="true"/> when the call succeeded; otherwise <see langword="false"/>.</returns>
        bool GenerateConsoleCtrlEvent(NativeMethods.CtrlEvents ctrlEvent, uint processGroupId);

        /// <summary>
        /// Gets the Win32 error code the last native call on this thread set.
        /// </summary>
        /// <returns>
        /// The value of <see cref="System.Runtime.InteropServices.Marshal.GetLastWin32Error"/>.
        /// </returns>
        int GetLastWin32Error();
    }
}

using Servy.Core.Native;
using System;
using System.Runtime.InteropServices;

namespace Servy.Service.Native
{
    /// <summary>
    /// Production implementation of <see cref="IConsoleCtrlNative"/>.
    /// </summary>
    /// <remarks>
    /// Every member is a one-line forward to the call it replaced in
    /// <see cref="Servy.Service.ProcessManagement.ProcessWrapper"/>: same target, same arguments,
    /// same order, same thread and the same treatment of the return value. The type holds no state,
    /// so an instance can be shared.
    /// </remarks>
    internal sealed class ConsoleCtrlNative : IConsoleCtrlNative
    {
        /// <summary>
        /// Calls <c>FreeConsole</c>.
        /// </summary>
        /// <returns><see langword="true"/> when the call succeeded; otherwise <see langword="false"/>.</returns>
        public bool FreeConsole()
        {
            return NativeMethods.FreeConsole();
        }

        /// <summary>
        /// Calls <c>AttachConsole</c> for the specified process.
        /// </summary>
        /// <param name="processId">The identifier of the process whose console to attach to.</param>
        /// <returns><see langword="true"/> when the call succeeded; otherwise <see langword="false"/>.</returns>
        public bool AttachConsole(int processId)
        {
            return NativeMethods.AttachConsole(processId);
        }

        /// <summary>
        /// Calls <c>SetConsoleCtrlHandler</c>.
        /// </summary>
        /// <param name="handlerRoutine">
        /// The handler to add or remove; <see cref="IntPtr.Zero"/> selects the process-wide
        /// ignore-CTRL+C flag rather than a routine.
        /// </param>
        /// <param name="add">
        /// <see langword="true"/> to add the handler; <see langword="false"/> to remove it.
        /// </param>
        /// <returns><see langword="true"/> when the call succeeded; otherwise <see langword="false"/>.</returns>
        public bool SetConsoleCtrlHandler(IntPtr handlerRoutine, bool add)
        {
            return NativeMethods.SetConsoleCtrlHandler(handlerRoutine, add);
        }

        /// <summary>
        /// Calls <c>GenerateConsoleCtrlEvent</c>.
        /// </summary>
        /// <param name="ctrlEvent">The control event to send.</param>
        /// <param name="processGroupId">
        /// The console process group to signal; <c>0</c> means every process sharing the console.
        /// </param>
        /// <returns><see langword="true"/> when the call succeeded; otherwise <see langword="false"/>.</returns>
        public bool GenerateConsoleCtrlEvent(NativeMethods.CtrlEvents ctrlEvent, uint processGroupId)
        {
            return NativeMethods.GenerateConsoleCtrlEvent(ctrlEvent, processGroupId);
        }

        /// <summary>
        /// Returns <see cref="Marshal.GetLastWin32Error"/>.
        /// </summary>
        /// <returns>The Win32 error code the last native call on this thread set.</returns>
        public int GetLastWin32Error()
        {
            return Marshal.GetLastWin32Error();
        }
    }
}

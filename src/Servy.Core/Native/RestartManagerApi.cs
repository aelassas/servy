using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Servy.Core.Native
{
    /// <summary>
    /// Calls the Windows Restart Manager (<c>rstrtmgr.dll</c>) directly.
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal sealed class RestartManagerApi : IRestartManagerApi
    {
        /// <inheritdoc />
        public int StartSession(out uint sessionHandle)
            => NativeMethods.RmStartSession(out sessionHandle, 0, new StringBuilder(NativeMethods.CCH_RM_SESSION_KEY + 1));

        /// <inheritdoc />
        public int RegisterFile(uint sessionHandle, string filePath)
            => NativeMethods.RmRegisterResources(sessionHandle, 1, new[] { filePath }, 0, null, 0, null);

        /// <inheritdoc />
        public int GetList(uint sessionHandle, out uint needed, ref uint count, NativeMethods.RM_PROCESS_INFO[] processes)
        {
            uint rebootReasons = 0;
            return NativeMethods.RmGetList(sessionHandle, out needed, ref count, processes, ref rebootReasons);
        }

        /// <inheritdoc />
        public int EndSession(uint sessionHandle) => NativeMethods.RmEndSession(sessionHandle);
    }
}

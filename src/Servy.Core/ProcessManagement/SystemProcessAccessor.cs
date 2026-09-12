using Servy.Core.Native;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Core.ProcessManagement
{
    /// <summary>
    /// Production implementation of <see cref="ISystemProcessAccessor"/> delegating to <see cref="Process"/> and <see cref="Toolhelp32Snapshot"/>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SystemProcessAccessor : ISystemProcessAccessor
    {
        /// <inheritdoc />
        public ISystemProcess GetProcessById(int pid)
        {
            return new SystemProcessWrapper(Process.GetProcessById(pid));
        }

        /// <inheritdoc />
        public ISystemProcess GetCurrentProcess()
        {
            return new SystemProcessWrapper(Process.GetCurrentProcess());
        }

        /// <inheritdoc />
        public (Dictionary<int, ProcessInfoNode> Snapshot, Dictionary<int, List<int>> ByParent) BuildSnapshotAndChildMap()
        {
            return Toolhelp32Snapshot.BuildSnapshotAndChildMap();
        }
    }
}

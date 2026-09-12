using Servy.Core.Native;

namespace Servy.Core.ProcessManagement
{
    /// <summary>
    /// Abstraction wrapping process resolution and native snapshot generation.
    /// </summary>
    public interface ISystemProcessAccessor
    {
        /// <summary>
        /// Returns an <see cref="ISystemProcess"/> component associated with the specified process ID.
        /// </summary>
        /// <param name="pid">The process identifier.</param>
        /// <returns>An <see cref="ISystemProcess"/> wrapper for the target process.</returns>
        ISystemProcess GetProcessById(int pid);

        /// <summary>
        /// Gets a new <see cref="ISystemProcess"/> component and associates it with the currently active process.
        /// </summary>
        /// <returns>An <see cref="ISystemProcess"/> wrapper for the current process.</returns>
        ISystemProcess GetCurrentProcess();

        /// <summary>
        /// Takes a native snapshot of all running processes and maps parent-to-child relationships.
        /// </summary>
        /// <returns>A tuple containing the complete snapshot dictionary and the child PID lookup map.</returns>
        (Dictionary<int, ProcessInfoNode> Snapshot, Dictionary<int, List<int>> ByParent) BuildSnapshotAndChildMap();
    }
}

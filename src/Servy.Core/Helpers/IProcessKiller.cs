namespace Servy.Core.Helpers
{
    /// <summary>
    /// Defines the contract for terminating processes, process trees, and releasing file locks.
    /// </summary>
    public interface IProcessKiller
    {
        /// <summary>
        /// Recursively kills all child processes of a specified parent process.
        /// </summary>
        /// <param name="parentPid">The process ID of the parent whose children should be terminated.</param>
        /// <remarks>
        /// This method enumerates processes where <c>ParentProcessId</c> matches 
        /// the given <paramref name="parentPid"/>.
        /// 
        /// It then recursively calls itself to ensure that grandchildren and deeper
        /// descendants are also terminated before finally killing the child itself.
        /// 
        /// Exceptions such as access denied or processes that have already exited are
        /// caught and ignored to allow cleanup to continue without interruption.
        /// </remarks>
        void KillChildren(int parentPid);

        /// <summary>
        /// Kills all processes with the specified name, including their child and parent processes.
        /// </summary>
        /// <param name="processName">The name of the process to kill. Can include or exclude ".exe".</param>
        /// <param name="killParents">Whether to kill parents as well.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        /// <remarks>
        /// This method captures a snapshot of all running processes to ensure consistency 
        /// during the recursive tree walk. It handles the ".exe" extension automatically.
        /// </remarks>
        bool KillProcessTreeAndParents(string processName, bool killParents = true);

        /// <summary>
        /// Kills a specific process by PID, including its entire child tree and (optionally) its ancestors.
        /// </summary>
        /// <param name="pid">The specific Process ID to target.</param>
        /// <param name="killParents">Whether to traverse up the tree and kill parent processes.</param>
        /// <returns>True if the process was found and termination was attempted; otherwise, false.</returns>
        bool KillProcessTreeAndParents(int pid, bool killParents = true);

        /// <summary>
        /// Kills all processes that currently hold a handle to the specified file.
        /// </summary>
        /// <param name="processHelper">An instance of <see cref="IProcessHelper"/> for process operations.</param>
        /// <param name="filePath">Full path to the file.</param>
        /// <returns><c>true</c> if all processes were successfully killed; otherwise <c>false</c>.</returns>
        /// <remarks>
        /// This method requires Sysinternals Handle.exe or Handle64.exe to be available
        /// and assumes its path is in <c>C:\Program Files\Sysinternals\handle64.exe</c> by default.
        /// </remarks>
        bool KillProcessesUsingFile(IProcessHelper processHelper, string filePath);
    }
}
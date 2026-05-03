namespace Servy.Core.Helpers
{
    /// <summary>
    /// Defines the contract for terminating processes, process trees, and releasing file locks.
    /// </summary>
    public interface IProcessKiller
    {
        /// <summary>
        /// Kills all child processes descended from the specified parent process identifier using a native snapshot map.
        /// </summary>
        /// <param name="parentPid">The numerical process identifier of the parent whose descendants should be terminated.</param>
        /// <remarks>
        /// This entry point builds a global native snapshot to ensure that even if intermediate bridge processes have exited, 
        /// their orphaned descendants are still reachable.
        /// 
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
        /// Terminates all process trees originating from executable instances matching the specified name, optionally traversing upward to terminate their parents.
        /// </summary>
        /// <param name="processName">The target process name to resolve and terminate.</param>
        /// <param name="killParents">A boolean flag indicating whether the upward execution chain should also be terminated.</param>
        /// <returns>A boolean value indicating whether the termination sequence executed successfully without encountering critical failures.</returns>
        /// <remarks>
        /// This method captures a snapshot of all running processes to ensure consistency 
        /// during the recursive tree walk. It handles the ".exe" extension automatically.
        /// </remarks>
        bool KillProcessTreeAndParents(string processName, bool killParents = true);

        /// <summary>
        /// Terminates the specific process tree originating from the provided process identifier, optionally traversing upward to terminate its parents.
        /// </summary>
        /// <param name="pid">The target numerical process identifier representing the root of the termination sequence.</param>
        /// <param name="killParents">A boolean flag indicating whether the upward execution chain should also be terminated.</param>
        /// <returns>A boolean value indicating whether the targeted process hierarchy was resolved and successfully terminated.</returns>
        bool KillProcessTreeAndParents(int pid, bool killParents = true);

        /// <summary>
        /// Discovers and violently terminates any process trees holding an active lock on the specified file path using diagnostic tooling.
        /// </summary>
        /// <param name="filePath">The absolute path of the targeted file currently constrained by an active lock.</param>
        /// <returns>A boolean value indicating whether the lock release operation resolved and executed without encountering critical errors.</returns>
        /// <remarks>
        /// This method requires Sysinternals Handle.exe or Handle64.exe to be available
        /// and assumes its path is in <c>C:\Program Files\Sysinternals\handle64.exe</c> by default.
        /// </remarks>
        bool KillProcessesUsingFile(string filePath);
    }
}
namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods for retrieving and formatting process-related information
    /// such as CPU usage, RAM usage, and path validation.
    /// </summary>
    public interface IProcessHelper
    {
        /// <summary>
        /// Performs maintenance on the process cache by removing entries for PIDs that are no longer active.
        /// Should be called by a background timer to prevent memory leaks.
        /// </summary>
        void MaintainCache();

        /// <summary>
        /// Retrieves both CPU and RAM metrics for a process using a single handle.
        /// </summary>
        ProcessMetrics GetProcessMetrics(int pid);

        /// <summary>
        /// Retrieves combined CPU and RAM metrics for an entire process tree.
        /// Performs a single pass over the tree to minimize kernel transitions.
        /// </summary>
        ProcessMetrics GetProcessTreeMetrics(int rootPid);

        /// <summary>
        /// Formats a CPU usage value as a percentage string.
        /// </summary>
        /// <param name="cpuUsage">The CPU usage value.</param>
        /// <returns>
        /// A formatted string with a percent sign.
        /// Examples:
        /// <list type="bullet">
        /// <item><description>0 -> "0%"</description></item>
        /// <item><description>0.03 -> "0%"</description></item>
        /// <item><description>1 -> "1.0%"</description></item>
        /// <item><description>1.04 -> "1.0%"</description></item>
        /// <item><description>1.05 -> "1.1%"</description></item>
        /// <item><description>1.06 -> "1.1%"</description></item>
        /// <item><description>1.1 -> "1.1%"</description></item>
        /// <item><description>1.49 -> "1.4%"</description></item>
        /// <item><description>1.51 -> "1.5%"</description></item>
        /// <item><description>1.57 -> "1.6%"</description></item>
        /// <item><description>1.636 -> "1.6%"</description></item>
        /// </list>
        /// </returns>
        string FormatCpuUsage(double cpuUsage);

        /// <summary>
        /// Formats a RAM usage value in human-readable units.
        /// </summary>
        /// <param name="ramUsage">The RAM usage in bytes.</param>
        /// <returns>
        /// A formatted string with the most appropriate unit:
        /// B, KB, MB, GB, or TB.
        /// Examples:
        /// <list type="bullet">
        /// <item><description>512 -> "512.0 B"</description></item>
        /// <item><description>2048 -> "2.0 KB"</description></item>
        /// <item><description>1048576 -> "1.0 MB"</description></item>
        /// <item><description>1073741824 -> "1.0 GB"</description></item>
        /// </list>
        /// </returns>
        string FormatRamUsage(long ramUsage);

        /// <summary>
        /// Resolves and validates an absolute filesystem path for use by a Windows service.
        /// </summary>
        /// <param name="inputPath">
        /// The input path, which may contain environment variables (e.g. %ProgramFiles%).
        /// Environment variables are expanded using the service account's environment only.
        /// </param>
        /// <returns>
        /// A normalized, absolute path with environment variables expanded.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the path is relative or contains environment variables that could not be expanded.
        /// </exception>
        /// <remarks>
        /// This method is intentionally strict:
        /// <list type="bullet">
        /// <item>Only absolute paths are allowed.</item>
        /// <item>Environment variables must be defined at the system level and visible to the service account.</item>
        /// <item>User-level environment variables are not supported.</item>
        /// </list>
        /// Use <see cref="ValidatePath"/> if you only need a boolean existence check.
        /// </remarks>
        string? ResolvePath(string? inputPath);

        /// <summary>
        /// Validates that a file or directory path exists after resolving environment variables
        /// and normalizing the path.
        /// </summary>
        /// <param name="path">
        /// The path to validate. May contain environment variables.
        /// </param>
        /// <param name="isFile">
        /// True to validate a file path; false to validate a directory path.
        /// </param>
        /// <returns>
        /// True if the path resolves successfully and exists; otherwise false.
        /// </returns>
        /// <remarks>
        /// This method never throws exceptions.
        /// Any failure during resolution (such as unexpanded environment variables,
        /// relative paths, or invalid paths) results in a false return value.
        /// </remarks>
        bool ValidatePath(string? path, bool isFile = true);

    }
}
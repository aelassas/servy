using Servy.Core.Config;
using Servy.Core.EnvironmentVariables;
using Servy.Core.Logging;
using Servy.Core.RegexWrapper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Servy.Service.Helpers
{
    /// <summary>
    /// Helper methods for processes.
    /// </summary>
    public static class ProcessHelper
    {
        #region Static Fields

        /// <summary>
        /// Compiled regex to identify standard environment variable placeholders.
        /// Includes a match timeout to prevent ReDoS attacks.
        /// </summary>
        internal static IRegexWrapper EnvVarRegex = new RegexWrapper(new Regex(
            @"(%[a-zA-Z_][a-zA-Z0-9_]*%)",
            RegexOptions.Compiled,
            AppConfig.InputRegexTimeout)); // 200ms is generous for this pattern

        #endregion

        /// <summary>
        /// Recursively kills the specified process and all of its child processes.
        /// This method is intended for .NET Framework 4.8 where <c>Process.Kill(true)</c>
        /// is not available.
        /// </summary>
        /// <param name="process">The root process to terminate.</param>
        /// <remarks>
        /// Uses WMI (<c>Win32_Process</c>) to enumerate child processes by <c>ParentProcessId</c>.
        /// Children are killed first, followed by the parent process, to avoid leaving orphaned processes.
        /// Any exceptions (e.g., process already exited) are caught and ignored.
        /// </remarks>
        public static void KillProcessTree(Process process)
        {
            try
            {
                if (process == null || process.HasExited)
                    return;

                using (var searcher = new System.Management.ManagementObjectSearcher(
                           $"Select * From Win32_Process Where ParentProcessId={process.Id}"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var childPid = Convert.ToInt32(obj["ProcessId"]);
                        try
                        {
                            using (var childProc = Process.GetProcessById(childPid))
                            {
                                KillProcessTree(childProc); // recursively kill children
                            }
                        }
                        catch
                        {
                            // child may have already exited
                        }
                    }
                }

                // Kill the main process last
                process.Kill();
                process.WaitForExit(30_000); // safety timeout
            }
            catch
            {
                // ignore errors (access denied, already exited, etc.)
            }
        }

        /// <summary>
        /// Expands environment variables and command-line arguments, auditing both for unexpanded placeholders.
        /// </summary>
        /// <param name="vars">The list of environment variables to expand.</param>
        /// <param name="rawArgs">The raw command-line arguments to expand.</param>
        /// <param name="logger">The logger instance for logging messages.</param>
        /// <param name="contextPrefix">An optional prefix for logging (e.g., "Pre-Launch", "Post-Stop").</param>
        /// <returns>A tuple containing the expanded environment dictionary and the expanded arguments string.</returns>
        public static (Dictionary<string, string> env, string expandedArgs) ExpandAndAudit(
            List<EnvironmentVariable> vars, string rawArgs, IServyLogger logger, string contextPrefix = "")
        {
            string prefix = string.IsNullOrWhiteSpace(contextPrefix) ? string.Empty : $"[{contextPrefix}] ";

            // 1. Expand environment variables list
            var expandedEnv = EnvironmentVariableHelper.ExpandEnvironmentVariables(vars);

            // 2. Audit expanded variables for leftover placeholders
            foreach (var kvp in expandedEnv)
            {
                LogUnexpandedPlaceholders(kvp.Value ?? string.Empty, $"{prefix}Environment Variable '{kvp.Key}'", logger);
            }

            // 3. Expand command-line arguments using the expanded environment
            var expandedArgs = EnvironmentVariableHelper.ExpandEnvironmentVariables(rawArgs, expandedEnv);

            // 4. Audit arguments for leftover placeholders
            LogUnexpandedPlaceholders(expandedArgs, $"{prefix}Arguments", logger);

            return (expandedEnv, expandedArgs);
        }

        /// <summary>
        /// Logs a warning for any unexpanded environment variable placeholders found in the given string.
        /// </summary>
        /// <param name="input">The string to inspect.</param>
        /// <param name="context">The descriptive context (e.g., "Arguments").</param>
        /// <param name="logger">The logger instance for logging messages.</param>
        private static void LogUnexpandedPlaceholders(string input, string context, IServyLogger logger)
        {
            if (string.IsNullOrEmpty(input))
                return;

            try
            {
                var matches = EnvVarRegex.Matches(input);

                foreach (Match match in matches)
                {
                    string placeholder = match.Value;
                    logger?.Warn($"Unexpanded environment variable {placeholder} in {context}");
                }
            }
            catch (RegexMatchTimeoutException ex)
            {
                // Log that the check itself timed out to avoid silent failure
                logger?.Error($"Regex timeout while inspecting placeholders in {context}. Input length: {input.Length}", ex);
            }
        }
    }
}

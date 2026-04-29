using Servy.Core.Config;
using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Helper class to find processes holding handles to a file using Sysinternals handle.exe.
    /// </summary>
    public static class HandleHelper
    {
        /// <summary>
        /// Contains information about a process holding a file handle.
        /// </summary>
        public class ProcessHandleInfo
        {
            /// <summary>
            /// Gets or sets the process ID.
            /// </summary>
            public int ProcessId { get; set; }

            /// <summary>
            /// Gets or sets the process name.
            /// </summary>
            public string ProcessName { get; set; }
        }

        /// <summary>
        /// A compiled regular expression used to parse the output of the handle utility.
        /// </summary>
        /// <remarks>
        /// The pattern extracts the process name and process ID (PID) from lines formatted as:
        /// <c>notepad.exe        pid: 1234   type: File     123: C:\Path\To\File.dll</c>
        /// <list type="bullet">
        /// <item>
        /// <description><c>name</c>: Captures the executable name (e.g., "notepad.exe").</description>
        /// </item>
        /// <item>
        /// <description><c>pid</c>: Captures the numerical process identifier (e.g., "1234").</description>
        /// </item>
        /// </list>
        /// </remarks>
        private static readonly Regex HandleOutputRegex = new Regex(
            @"^\s*(?<name>.+?)\s+pid:\s*(?<pid>\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline,
            AppConfig.HandleExeRegexTimeout);

        /// <summary>
        /// Uses handle.exe or handle64.exe to find all processes that have an open handle to the specified file.
        /// </summary>
        /// <param name="processHelper">An instance of <see cref="IProcessHelper"/> to assist with process-related operations.</param>
        /// <param name="handleExePath">Full path to handle.exe or handle64.exe.</param>
        /// <param name="filePath">Full path of the file to check for open handles.</param>
        /// <returns>A list of <see cref="ProcessHandleInfo"/> objects representing the processes holding the file.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="handleExePath"/> or <paramref name="filePath"/> is null or empty.</exception>
        public static List<ProcessHandleInfo> GetProcessesUsingFile(IProcessHelper processHelper, string handleExePath, string filePath)
        {
            if (string.IsNullOrWhiteSpace(handleExePath))
                throw new ArgumentException("handleExePath is null or empty", nameof(handleExePath));
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath is null or empty", nameof(filePath));

            var processes = new List<ProcessHandleInfo>();

            var psi = new ProcessStartInfo
            {
                FileName = handleExePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true, // We redirect this, so we MUST drain it
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = $"{processHelper.EscapeProcessArgument(filePath)} /accepteula"
            };

            using (var process = new Process { StartInfo = psi })
            {
                // Use StringBuilders to capture stdout and stderr in the background to avoid deadlocks
                var outputBuilder = new System.Text.StringBuilder();
                var errorBuilder = new System.Text.StringBuilder();

                process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

                if (!process.Start())
                    throw new InvalidOperationException($"Failed to start process: {handleExePath}");

                // Start asynchronous reads
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(AppConfig.HandleExeTimeoutMs))
                {
                    try { process.Kill(); }
                    catch (InvalidOperationException) { /* Already exited, expected */ }
                    catch (Exception killEx)
                    {
                        Logger.Warn($"Failed to kill handle.exe after timeout (PID {process.Id}): {killEx.Message}");
                    }
                    throw new TimeoutException($"handle.exe timed out. Stderr: {errorBuilder}");
                }

                // Final WaitForExit() with no timeout flushes any in-flight async event handlers
                process.WaitForExit();
                string output = outputBuilder.ToString();

                // Check for specific handle.exe errors (like "No matching handles found")
                if (string.IsNullOrWhiteSpace(output) && errorBuilder.Length > 0)
                {
                    Logger.Warn($"handle.exe produced error output: {errorBuilder}");
                }

                try
                {
                    var matches = HandleOutputRegex.Matches(output);
                    foreach (Match match in matches)
                    {
                        if (match.Success && int.TryParse(match.Groups["pid"].Value, out int pid))
                        {
                            processes.Add(new ProcessHandleInfo
                            {
                                ProcessName = match.Groups["name"].Value.Trim(),
                                ProcessId = pid
                            });
                        }
                    }
                }
                catch (RegexMatchTimeoutException ex)
                {
                    Logger.Error("Regex parsing timed out while processing handle output.", ex);
                }
            }

            return processes;
        }
    }
}

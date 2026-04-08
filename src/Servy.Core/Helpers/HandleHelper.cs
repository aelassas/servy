using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Helper class to find processes holding handles to a file using Sysinternals handle.exe.
    /// </summary>
    [ExcludeFromCodeCoverage]
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
        /// <c>notepad.exe       pid: 1234   type: File    123: C:\Path\To\File.dll</c>
        /// <list type="bullet">
        /// <item>
        /// <description><c>name</c>: Captures the executable name (e.g., "notepad.exe").</description>
        /// </item>
        /// <item>
        /// <description><c>pid</c>: Captures the numerical process identifier (e.g., "1234").</description>
        /// </item>
        /// </list>
        /// </remarks>
        private static readonly Regex HandleOutputRegex = new Regex(@"^\s*(?<name>.+?)\s+pid:\s*(?<pid>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        /// <summary>
        /// Uses handle.exe or handle64.exe to find all processes that have an open handle to the specified file.
        /// </summary>
        /// <param name="handleExePath">Full path to handle.exe or handle64.exe.</param>
        /// <param name="filePath">Full path of the file to check for open handles.</param>
        /// <returns>A list of <see cref="ProcessHandleInfo"/> objects representing the processes holding the file.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="handleExePath"/> or <paramref name="filePath"/> is null or empty.</exception>
        public static List<ProcessHandleInfo> GetProcessesUsingFile(string handleExePath, string filePath)
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
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = $"{ProcessHelper.EscapeProcessArgument(filePath)} /accepteula"
            };

            using (var process = Process.Start(psi))
            {
                if (process == null)
                    throw new InvalidOperationException($"Failed to start process: {handleExePath}");

                string output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(5000))
                {
                    process.Kill();
                    throw new TimeoutException("handle.exe did not respond within 5 seconds");
                }

                // Parse output lines like:
                // notepad.exe       pid: 1234   type: File    123: C:\Path\To\File.dll
                foreach (Match match in HandleOutputRegex.Matches(output))
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

            return processes;
        }

    }
}

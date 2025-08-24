using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
                Arguments = $"\"{filePath}\" /accepteula",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                if (process == null)
                    throw new InvalidOperationException($"Failed to start process: {handleExePath}");

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Parse output lines like:
                // notepad.exe       pid: 1234   type: File    123: C:\Path\To\File.dll
                var regex = new Regex(@"^\s*(?<name>.+?)\s+pid:\s*(?<pid>\d+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);

                foreach (Match match in regex.Matches(output))
                {
                    if (match.Success)
                    {
                        processes.Add(new ProcessHandleInfo
                        {
                            ProcessName = match.Groups["name"].Value.Trim(),
                            ProcessId = int.Parse(match.Groups["pid"].Value)
                        });
                    }
                }
            }

            return processes;
        }
    }
}

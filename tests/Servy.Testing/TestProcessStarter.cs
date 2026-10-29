using System.Diagnostics;
using System.IO;
using System.Text;

namespace Servy.Testing
{
    /// <summary>
    /// Starts child processes for integration tests and waits for a handshake line on their standard output.
    /// </summary>
    public static class TestProcessStarter
    {
        /// <summary>
        /// Gets the path of the Windows PowerShell 5.1 host the integration tests spawn.
        /// </summary>
        public static readonly string PowerShellPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");

        /// <summary>
        /// Starts <paramref name="psi"/> and waits for the first standard-output line that satisfies <paramref name="accept"/>.
        /// </summary>
        /// <remarks>
        /// A start that exits, or that prints nothing acceptable within <paramref name="budget"/>, is killed and retried
        /// up to <paramref name="maxAttempts"/> times. A cold powershell.exe on a loaded CI runner occasionally stalls
        /// before its first output line, and a stalled instance does not recover with more time, so the fix is a fresh
        /// instance rather than a wider budget (see #7128; #6975 had already widened the tree budget from 15 s to 20 s).
        /// Killing the stalled instance is also what unblocks the reader task still sitting in <c>ReadLine</c>.
        /// Every started process is handed to <paramref name="onStarted"/>, so the caller's teardown cleans up whatever
        /// this method leaves behind.
        /// </remarks>
        /// <param name="psi">The start information. <see cref="ProcessStartInfo.RedirectStandardOutput"/> must be set;
        /// when <see cref="ProcessStartInfo.RedirectStandardError"/> is set, stderr is captured and reported on failure.</param>
        /// <param name="accept">Predicate identifying the handshake line among the process's standard-output lines.</param>
        /// <param name="budget">How long one attempt may take to produce an accepted line.</param>
        /// <param name="onStarted">Called with every process started, so the caller can track it for teardown.</param>
        /// <param name="maxAttempts">How many attempts to make before giving up.</param>
        /// <returns>The live process and the accepted handshake line.</returns>
        /// <exception cref="InvalidOperationException"><see cref="Process.Start(ProcessStartInfo)"/> returned no process,
        /// or every attempt failed; in the latter case the message lists each attempt's outcome.</exception>
        public static (Process Process, string Line) StartWithHandshake(
            ProcessStartInfo psi,
            Func<string, bool> accept,
            TimeSpan budget,
            Action<Process> onStarted,
            int maxAttempts = 3)
        {
            var outcomes = new List<string>();

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var process = Process.Start(psi)
                    ?? throw new InvalidOperationException($"Process.Start returned null for '{psi.FileName}'.");
                onStarted(process);

                var errBuilder = new StringBuilder();
                if (psi.RedirectStandardError)
                {
                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            lock (errBuilder)
                            {
                                errBuilder.AppendLine(e.Data);
                            }
                        }
                    };
                    process.BeginErrorReadLine();
                }

                // EOF (a null line) means the process exited without ever printing an acceptable line.
                var readTask = Task.Run(() =>
                {
                    string? line;
                    while ((line = process.StandardOutput.ReadLine()) != null)
                    {
                        if (accept(line))
                        {
                            return line;
                        }
                    }
                    return null;
                });

                var stopwatch = Stopwatch.StartNew();
                bool completed = readTask.Wait(budget);
                stopwatch.Stop();

                if (completed && readTask.Result != null)
                {
                    return (process, readTask.Result);
                }

                // Kill the stalled or dead instance before retrying; this also unblocks the reader above.
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (InvalidOperationException)
                {
                    // The process exited between the check and the kill.
                }
                process.WaitForExit(5000);

                string errOutput;
                lock (errBuilder)
                {
                    errOutput = errBuilder.ToString().Trim();
                }

                string reason = completed
                    ? $"exited without the handshake, exit code {ExitCodeOrUnknown(process)}"
                    : $"no handshake in {stopwatch.Elapsed.TotalSeconds:F1} s";
                outcomes.Add($"attempt {attempt}: {reason}"
                    + (errOutput.Length > 0 ? $", stderr: {errOutput}" : string.Empty));
            }

            throw new InvalidOperationException(
                $"No handshake from '{psi.FileName}' after {maxAttempts} attempts: {string.Join("; ", outcomes)}");
        }

        /// <summary>
        /// Formats a process's exit code, or "unknown" when it is not available.
        /// </summary>
        /// <param name="process">The process to read the exit code from.</param>
        /// <returns>The exit code in hexadecimal, or "unknown" when the process has not exited.</returns>
        private static string ExitCodeOrUnknown(Process process)
        {
            try
            {
                return $"0x{process.ExitCode:X8}";
            }
            catch (InvalidOperationException)
            {
                return "unknown";
            }
        }
    }
}

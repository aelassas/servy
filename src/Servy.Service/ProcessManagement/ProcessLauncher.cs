using Servy.Core.Logging;
using Servy.Service.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Servy.Service.ProcessManagement
{
    /// <summary>
    /// Provides a centralized utility for launching and monitoring external processes within a Windows Service context.
    /// Handles environment variable expansion, runtime-specific encoding fixes, and Service Control Manager (SCM) heartbeats.
    /// </summary>
    public static class ProcessLauncher
    {
        /// <summary>
        /// Orchestrates the initialization and startup of an external process based on the provided options.
        /// </summary>
        /// <param name="options">The configuration parameters for the process launch.</param>
        /// <param name="factory">The factory used to create the process wrapper.</param>
        /// <param name="logger">The logger instance for operational telemetry.</param>
        /// <returns>An initialized and started <see cref="IProcessWrapper"/>.</returns>
        /// <exception cref="TimeoutException">Thrown if a synchronous wait operation exceeds the configured timeout.</exception>
        public static IProcessWrapper Start(
            ProcessLaunchOptions options,
            IProcessFactory factory,
            ILogger logger)
        {
            // 1. Resolve environment variables and arguments
            var expandedEnv = EnvironmentVariableHelper.ExpandEnvironmentVariables(options.EnvironmentVariables);
            var finalArgs = EnvironmentVariableHelper.ExpandEnvironmentVariables(options.Arguments ?? string.Empty, expandedEnv);

            // 2. Configure ProcessStartInfo with service-safe defaults
            var redirectOutput = options.RedirectToWriters && !options.FireAndForget;
            var psi = new ProcessStartInfo
            {
                FileName = options.ExecutablePath,
                Arguments = finalArgs,
                WorkingDirectory = options.WorkingDirectory ?? Path.GetDirectoryName(options.ExecutablePath) ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = redirectOutput && !string.IsNullOrWhiteSpace(options.StdOutPath),
                RedirectStandardError = redirectOutput && !string.IsNullOrWhiteSpace(options.StdErrPath),
            };

            if (redirectOutput)
            {
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;
            }

            // 3. Apply the environment block
            foreach (var envVar in expandedEnv)
            {
                psi.Environment[envVar.Key] = envVar.Value ?? string.Empty;
            }

            // 4. Apply runtime-specific fixes (Python/Java encoding)
            ApplyLanguageFixes(psi);

            // 5. Launch the process
            var process = factory.Create(psi, logger);

            process.Start();

            // 6. Handle execution mode
            if (options.FireAndForget)
            {
                return process;
            }

            var stdoutBuffer = new StringBuilder();
            var stderrBuffer = new StringBuilder();

            if (psi.RedirectStandardOutput)
            {
                process.UnderlyingProcess.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                        stdoutBuffer.AppendLine(e.Data);
                };
            }
            if (psi.RedirectStandardError)
            {
                process.UnderlyingProcess.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                        stderrBuffer.AppendLine(e.Data);
                };
            }

            if (psi.RedirectStandardOutput) process.BeginOutputReadLine();
            if (psi.RedirectStandardError) process.BeginErrorReadLine();

            // Synchronous mode: Wait for exit while pulsing the SCM
            if (options.TimeoutMs > 0 && (options.OnScmHeartbeat?.Target != null || options.OnScmHeartbeat?.Method != null))
            {
                WaitForExitWithHeartbeat(process, options, logger);
            }

            // Ensure all async reads are finished
            process.UnderlyingProcess.WaitForExit();

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false); // UTF-8 without BOM

            // Save logs if paths are set
            if (!string.IsNullOrWhiteSpace(options.StdOutPath))
            {
                File.AppendAllText(options.StdOutPath, stdoutBuffer.ToString(), encoding);
            }
            if (!string.IsNullOrWhiteSpace(options.StdErrPath))
            {
                File.AppendAllText(options.StdErrPath, stderrBuffer.ToString(), encoding);
            }

            return process;
        }

        /// <summary>
        /// Performs a synchronous wait for process exit while periodically updating the Windows SCM to prevent service timeouts.
        /// </summary>
        private static void WaitForExitWithHeartbeat(IProcessWrapper process, ProcessLaunchOptions options, ILogger logger)
        {
            var sw = Stopwatch.StartNew();

            while (!process.WaitForExit(options.WaitChunkMs))
            {
                // Pulse the SCM to indicate the service is still transitioning/active
                options.OnScmHeartbeat?.Invoke(options.ScmAdditionalTimeMs);

                if (sw.ElapsedMilliseconds >= options.TimeoutMs)
                {
                    var errorMsg = $"{options.ExecutablePath} timed out after {options.TimeoutMs}ms. Terminating process tree.";
                    if (options.LogErrorAsWarning)
                    {
                        logger.Warn(errorMsg);
                    }
                    else
                    {
                        logger.Error(errorMsg);
                    }
                    
                    process.Kill(true);

                    throw new TimeoutException($"{options.ExecutablePath} exceeded the maximum allowed timeout of {options.TimeoutMs}ms.");
                }
            }
        }

        /// <summary>
        /// Applies environment variables and command-line flags to ensure consistent UTF-8 I/O behavior for known runtimes.
        /// </summary>
        /// <param name="psi">The start info to modify.</param>
        internal static void ApplyLanguageFixes(ProcessStartInfo psi)
        {
            // Python: Enable UTF-8 mode and disable I/O buffering for real-time log capturing
            if (psi.FileName.IndexOf("python", StringComparison.OrdinalIgnoreCase) >= 0 ||
                psi.Arguments.IndexOf(".py", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                psi.Environment["PYTHONLEGACYWINDOWSSTDIO"] = "0";
                psi.Environment["PYTHONIOENCODING"] = "utf-8";
                psi.Environment["PYTHONUTF8"] = "1";
                psi.Environment["PYTHONUNBUFFERED"] = "1";
            }

            // Java: Ensure file encoding is set to UTF-8 if not explicitly defined by the user
            if ((psi.FileName.IndexOf("java", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 psi.Arguments.IndexOf(".java", StringComparison.OrdinalIgnoreCase) >= 0) &&
                psi.Arguments.IndexOf("-Dfile.encoding", StringComparison.OrdinalIgnoreCase) < 0)
            {
                psi.Arguments = $"-Dfile.encoding=UTF-8 {psi.Arguments}".Trim();
            }
        }
    }
}
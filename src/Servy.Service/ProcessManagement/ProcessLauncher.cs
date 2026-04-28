using Servy.Core.Helpers;
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
                    IServyLogger logger)
        {
            // 1. Resolve environment variables and arguments
            var expandedEnv = EnvironmentVariableHelper.ExpandEnvironmentVariables(options.EnvironmentVariables);
            var finalArgs = EnvironmentVariableHelper.ExpandEnvironmentVariables(options.Arguments ?? string.Empty, expandedEnv);

            // 2. Configure ProcessStartInfo with service-safe defaults
            var redirectOutput = !options.EnableConsoleUI && options.RedirectToWriters && !options.FireAndForget;
            var psi = new ProcessStartInfo
            {
                FileName = options.ExecutablePath,
                Arguments = finalArgs,
                WorkingDirectory = options.WorkingDirectory ?? Path.GetDirectoryName(options.ExecutablePath) ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = !options.EnableConsoleUI,
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

            // 4. Apply runtime-specific fixes
            ApplyLanguageFixes(psi);

            // 5. Launch the process
            var process = factory.Create(psi, logger);

            // ROBUSTNESS: Track ownership. If the method fails before returning, 
            // we must dispose the process to prevent handle leaks.
            bool returnedOwnership = false;

            StreamWriter stdoutWriter = null;
            StreamWriter stderrWriter = null;
            bool pathsMatch = string.Equals(options.StdOutPath, options.StdErrPath, StringComparison.OrdinalIgnoreCase);

            try
            {
                process.Start();

                // 6. Handle execution mode
                if (options.FireAndForget)
                {
                    returnedOwnership = true;
                    return process;
                }

                // Sync objects with strong identity (local to this execution)
                object stdoutLock = new object();
                object stderrLock = new object();
                var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

                // Ensure directories exist for the log files
                Helper.EnsureDirectoryExists(options.StdOutPath);
                if (!pathsMatch) Helper.EnsureDirectoryExists(options.StdErrPath);

                // Setup StdOut Writer
                if (psi.RedirectStandardOutput && !string.IsNullOrWhiteSpace(options.StdOutPath))
                {
                    // ROBUSTNESS: Use the safe StreamWriter pattern to ensure the FileStream 
                    // is disposed if the StreamWriter constructor throws.
                    FileStream stdoutFs = null;
                    try
                    {
                        stdoutFs = new FileStream(options.StdOutPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                        stdoutWriter = new StreamWriter(stdoutFs, encoding) { AutoFlush = true };
                    }
                    catch
                    {
                        stdoutFs?.Dispose();
                        throw;
                    }

                    process.UnderlyingProcess.OutputDataReceived += (_, e) =>
                    {
                        if (e.Data != null)
                        {
                            // Lock on the dedicated sync object, not the resource itself
                            lock (stdoutLock) { stdoutWriter.WriteLine(e.Data); }
                        }
                    };
                }

                // Setup StdErr Writer
                if (psi.RedirectStandardError && !string.IsNullOrWhiteSpace(options.StdErrPath))
                {
                    if (pathsMatch && stdoutWriter != null)
                    {
                        stderrWriter = stdoutWriter;
                        stderrLock = stdoutLock; // Use the same lock if writing to the same file
                    }
                    else
                    {
                        FileStream stderrFs = null;
                        try
                        {
                            stderrFs = new FileStream(options.StdErrPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                            stderrWriter = new StreamWriter(stderrFs, encoding) { AutoFlush = true };
                        }
                        catch
                        {
                            stderrFs?.Dispose();
                            throw;
                        }
                    }

                    process.UnderlyingProcess.ErrorDataReceived += (_, e) =>
                    {
                        if (e.Data != null)
                        {
                            lock (stderrLock) { stderrWriter.WriteLine(e.Data); }
                        }
                    };
                }

                if (psi.RedirectStandardOutput) process.BeginOutputReadLine();
                if (psi.RedirectStandardError) process.BeginErrorReadLine();

                // Synchronous mode: Wait for exit while pulsing the SCM
                if (options.TimeoutMs <= 0)
                {
                    throw new ArgumentException(
                        "Synchronous launch requires TimeoutMs > 0. Set FireAndForget = true for unbounded launches.",
                        nameof(options));
                }

                WaitForExitWithHeartbeat(process, options, logger);

                // Ensure all async reads are finished before disposing streams
                process.UnderlyingProcess.WaitForExit();

                returnedOwnership = true;
                return process;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed during synchronous execution or log flushing for '{options.ExecutablePath}': {ex.Message}", ex);
                throw;
            }
            finally
            {
                // Dispose writers safely to release file locks.
                stdoutWriter?.Dispose();
                if (!pathsMatch)
                {
                    stderrWriter?.Dispose();
                }

                // ROBUSTNESS: If we didn't reach a 'return' statement successfully, 
                // clean up the process wrapper to prevent a handle leak.
                if (!returnedOwnership)
                {
                    process?.Dispose();
                }
            }
        }

        /// <summary>
        /// Performs a synchronous wait for process exit while periodically updating the Windows SCM to prevent service timeouts.
        /// </summary>
        private static void WaitForExitWithHeartbeat(IProcessWrapper process, ProcessLaunchOptions options, IServyLogger logger)
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
        /// Detection is strictly scoped to the executable filename and extension to avoid false positives from directory paths.
        /// </summary>
        /// <param name="psi">The start info to modify.</param>
        internal static void ApplyLanguageFixes(ProcessStartInfo psi)
        {
            if (psi == null || string.IsNullOrEmpty(psi.FileName))
            {
                return;
            }

            string extension = Path.GetExtension(psi.FileName);
            string fileNameOnly = Path.GetFileNameWithoutExtension(psi.FileName);

            // Python Logic: 
            // Matches 'python', 'pythonw', or 'python3.x' patterns, plus .py scripts.
            bool isPython =
                string.Equals(fileNameOnly, "python", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileNameOnly, "pythonw", StringComparison.OrdinalIgnoreCase) ||
                fileNameOnly.StartsWith("python3", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".py", StringComparison.OrdinalIgnoreCase);

            if (isPython)
            {
                psi.Environment["PYTHONLEGACYWINDOWSSTDIO"] = "0";
                psi.Environment["PYTHONIOENCODING"] = "utf-8";
                psi.Environment["PYTHONUTF8"] = "1";
                psi.Environment["PYTHONUNBUFFERED"] = "1";
            }

            // Java Logic: 
            // Matches 'java', 'javaw', or 'javac', plus self-executing .jar archives.
            bool isJava =
                string.Equals(fileNameOnly, "java", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileNameOnly, "javaw", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileNameOnly, "javac", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".jar", StringComparison.OrdinalIgnoreCase);

            if (isJava)
            {
                string currentArgs = psi.Arguments ?? string.Empty;
                // Only prepend if the user hasn't already defined a file encoding
                if (currentArgs.IndexOf("-Dfile.encoding", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    psi.Arguments = $"-Dfile.encoding=UTF-8 {currentArgs}".Trim();
                }
            }
        }
    }
}
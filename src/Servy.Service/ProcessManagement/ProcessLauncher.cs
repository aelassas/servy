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
        /// <remarks>
        /// This implementation implements lazy-loading to prevent zero-byte log file sprawl 
        /// and uses local path captures to satisfy compiler null-safety analysis inside event handlers.
        /// </remarks>
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
            // 0. Precondition Validation
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (string.IsNullOrWhiteSpace(options.ExecutablePath))
            {
                throw new ArgumentException("Executable path must be provided.", nameof(options));
            }

            if (!options.FireAndForget && options.TimeoutMs <= 0)
            {
                throw new ArgumentException(
                    "Synchronous launch requires TimeoutMs > 0. Set FireAndForget = true for unbounded launches.",
                    nameof(options));
            }

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

            if (psi.RedirectStandardOutput)
            {
                psi.StandardOutputEncoding = Encoding.UTF8;
            }

            if (psi.RedirectStandardError)
            {
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
            // we must terminate the process and dispose the wrapper to prevent handle and process leaks.
            bool returnedOwnership = false;

            StreamWriter stdoutWriter = null;
            StreamWriter stderrWriter = null;

            string normalizedOut = Helper.NormalizePath(options.StdOutPath);
            string normalizedErr = Helper.NormalizePath(options.StdErrPath);

            bool pathsMatch =
                normalizedOut != null
                && normalizedErr != null
                && string.Equals(normalizedOut, normalizedErr, StringComparison.OrdinalIgnoreCase);

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
                // If paths match, we must synchronize both streams on the exact same lock
                object stderrLock = pathsMatch ? stdoutLock : new object();
                var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

                // Capture paths into local variables to satisfy null-safety analysis
                // and ensure the closure uses a stable, non-null reference.
                string outPath = options.StdOutPath;
                string errPath = options.StdErrPath;

                // Setup StdOut Writer (Lazy Init)
                if (psi.RedirectStandardOutput && !string.IsNullOrWhiteSpace(outPath))
                {
                    process.UnderlyingProcess.OutputDataReceived += (_, e) =>
                    {
                        if (e.Data != null)
                        {
                            lock (stdoutLock)
                            {
                                if (stdoutWriter == null)
                                {
                                    Helper.EnsureDirectoryExists(outPath);
                                    FileStream stdoutFs = null;
                                    try
                                    {
                                        stdoutFs = new FileStream(outPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                                        stdoutWriter = new StreamWriter(stdoutFs, encoding) { AutoFlush = true };
                                    }
                                    catch
                                    {
                                        stdoutFs?.Dispose();
                                        throw;
                                    }
                                }
                                stdoutWriter.WriteLine(e.Data);
                            }
                        }
                    };
                }

                // Setup StdErr Writer (Lazy Init)
                if (psi.RedirectStandardError && !string.IsNullOrWhiteSpace(errPath))
                {
                    process.UnderlyingProcess.ErrorDataReceived += (_, e) =>
                    {
                        if (e.Data != null)
                        {
                            lock (stderrLock)
                            {
                                if (pathsMatch && !string.IsNullOrWhiteSpace(outPath))
                                {
                                    // Multiplexing into the same file: initialize stdoutWriter if it hasn't been yet
                                    if (stdoutWriter == null)
                                    {
                                        Helper.EnsureDirectoryExists(outPath);
                                        FileStream sharedFs = null;
                                        try
                                        {
                                            sharedFs = new FileStream(outPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                                            stdoutWriter = new StreamWriter(sharedFs, encoding) { AutoFlush = true };
                                        }
                                        catch
                                        {
                                            sharedFs?.Dispose();
                                            throw;
                                        }
                                    }
                                    stdoutWriter.WriteLine(e.Data);
                                }
                                else
                                {
                                    // Independent file
                                    if (stderrWriter == null)
                                    {
                                        Helper.EnsureDirectoryExists(errPath);
                                        FileStream stderrFs = null;
                                        try
                                        {
                                            stderrFs = new FileStream(errPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                                            stderrWriter = new StreamWriter(stderrFs, encoding) { AutoFlush = true };
                                        }
                                        catch
                                        {
                                            stderrFs?.Dispose();
                                            throw;
                                        }
                                    }
                                    stderrWriter.WriteLine(e.Data);
                                }
                            }
                        }
                    };
                }

                if (psi.RedirectStandardOutput) process.BeginOutputReadLine();
                if (psi.RedirectStandardError) process.BeginErrorReadLine();

                // Synchronous mode: Wait for exit while pulsing the SCM
                WaitForExitWithHeartbeat(process, options, logger);

                // Ensure all async reads are finished before disposing streams
                process.UnderlyingProcess.WaitForExit();

                returnedOwnership = true;
                return process;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed during synchronous execution or log flushing for '{options.ExecutablePath}'.", ex);
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

                // ROBUSTNESS: If we didn't successfully return ownership, the process is orphaned.
                // We must kill the process tree before disposing the wrapper to avoid leaking child processes.
                if (!returnedOwnership && process != null)
                {
                    try
                    {
                        // Check if the process actually started and is still running.
                        // process.Start() is the first entry in the try block; if any subsequent logic throws,
                        // we must ensure the child does not remain active and unsupervised.
                        if (!process.HasExited)
                        {
                            process.Kill(true);
                        }
                    }
                    catch (Exception killEx)
                    {
                        // Log but don't rethrow, as we need the original exception to propagate.
                        logger.Warn($"Failed to kill orphaned child after launch failure: {killEx.Message}");
                    }
                    process.Dispose();
                }
            }
        }

        /// <summary>
        /// Performs a synchronous wait for process exit while periodically updating the Windows SCM to prevent service timeouts.
        /// </summary>
        private static void WaitForExitWithHeartbeat(IProcessWrapper process, ProcessLaunchOptions options, IServyLogger logger)
        {
            var sw = Stopwatch.StartNew();
            while (true)
            {
                long remaining = options.TimeoutMs - sw.ElapsedMilliseconds;
                if (remaining <= 0)
                {
                    // Final non-blocking poll
                    if (process.WaitForExit(0)) return;

                    var errorMsg = $"{options.ExecutablePath} timed out after {options.TimeoutMs}ms. Terminating process tree.";
                    if (options.LogErrorAsWarning) logger.Warn(errorMsg); else logger.Error(errorMsg);
                    process.Kill(true);
                    throw new TimeoutException($"{options.ExecutablePath} exceeded the maximum allowed timeout of {options.TimeoutMs}ms.");
                }

                int chunk = (int)Math.Min(options.WaitChunkMs, remaining);
                if (process.WaitForExit(chunk)) return;
                options.OnScmHeartbeat?.Invoke(options.ScmAdditionalTimeMs);
            }
        }

        /// <summary>
        /// Applies environment variables and command-line flags to ensure consistent UTF-8 I/O behavior for known runtimes.
        /// Detection is strictly scoped to the executable filename and extension to avoid false positives from directory paths.
        /// </summary>
        /// <param name="psi">The start info to modify.</param>
        public static void ApplyLanguageFixes(ProcessStartInfo psi)
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
                SetIfMissing(psi, "PYTHONLEGACYWINDOWSSTDIO", "0");
                SetIfMissing(psi, "PYTHONIOENCODING", "utf-8");
                SetIfMissing(psi, "PYTHONUTF8", "1");
                SetIfMissing(psi, "PYTHONUNBUFFERED", "1");
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

        /// <summary>
        /// Sets an environment variable in the specified <see cref="ProcessStartInfo"/> only if 
        /// the key does not already exist in the current environment block.
        /// </summary>
        /// <remarks>
        /// This utility ensures that default runtime settings do not overwrite 
        /// explicit user-defined environment configurations.
        /// </remarks>
        /// <param name="psi">The process start information containing the environment block to modify.</param>
        /// <param name="key">The name of the environment variable key.</param>
        /// <param name="value">The value to assign to the key if missing.</param>
        private static void SetIfMissing(ProcessStartInfo psi, string key, string value)
        {
            // Perform a safe check against the existing environment dictionary
            if (!psi.Environment.ContainsKey(key))
            {
                psi.Environment[key] = value;
            }
        }

    }
}
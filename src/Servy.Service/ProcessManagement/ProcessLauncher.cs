using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Servy.Service.ProcessManagement
{
    /// <summary>
    /// Provides a centralized utility for launching and monitoring external processes within a Windows Service context.
    /// Handles environment variable expansion, runtime-specific encoding fixes, and Service Control Manager (SCM) heartbeats.
    /// </summary>
    public static class ProcessLauncher
    {
        /// <summary>
        /// A compiled regular expression used to detect an existing <c>-Dfile.encoding</c> system property in Java arguments.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The pattern <c>(^|\s)-Dfile\.encoding([=\s]|$)</c> is designed to avoid false positives by ensuring the 
        /// flag is at the start of the string or preceded by whitespace, and followed by an assignment or a delimiter. 
        /// This prevents matching similar substrings inside file paths or JAR names.
        /// </para>
        /// <para>
        /// This regex uses <see cref="RegexOptions.Compiled"/> for performance during process startup and 
        /// <see cref="AppConfig.InputRegexTimeout"/> to prevent potential Denial of Service (DoS) from backtracking 
        /// on malformed user input.
        /// </para>
        /// </remarks>
        private static readonly Regex JavaFileEncodingRegex = new Regex(
            @"(^|\s)-Dfile\.encoding([=\s]|$)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant, // Java property names are case-sensitive
            AppConfig.InputRegexTimeout);

        /// <summary>
        /// Matches canonical Python launcher executable names strictly.
        /// Evaluates patterns such as 'python', 'pythonw', 'python2', 'python3', 'python3.x' 'py', or 'pyw' without capturing arbitrary prefixes.
        /// </summary>
        private static readonly Regex PythonExeRegex = new Regex(
             @"^(python(w|\d+(\.\d+)?)?|pyw?)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            AppConfig.InputRegexTimeout);

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
            var (expandedEnv, finalArgs) = Helpers.ProcessHelper.ExpandAndAudit(options.EnvironmentVariables, options.Arguments ?? string.Empty, logger);

            // 2. Configure ProcessStartInfo with service-safe defaults
            var redirectOutput = !options.EnableConsoleUI && options.RedirectToWriters && !options.FireAndForget;
            var psi = new ProcessStartInfo
            {
                FileName = options.ExecutablePath,
                Arguments = finalArgs,
                WorkingDirectory = options.WorkingDirectory ?? Path.GetDirectoryName(options.ExecutablePath) ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = !options.EnableConsoleUI,
                RedirectStandardOutput = redirectOutput && !string.IsNullOrWhiteSpace(options.StdoutPath),
                RedirectStandardError = redirectOutput && !string.IsNullOrWhiteSpace(options.StderrPath),
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

            // Failure latches to prevent log-spam if file access is denied
            bool stdoutWriterFailed = false;
            bool stderrWriterFailed = false;

            string normalizedOut = Helper.NormalizePath(options.StdoutPath);
            string normalizedErr = Helper.NormalizePath(options.StderrPath);

            bool pathsMatch =
                normalizedOut != null
                && normalizedErr != null
                && string.Equals(normalizedOut, normalizedErr, StringComparison.OrdinalIgnoreCase);

            bool processStarted = false;
            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException(
                        $"Process.Start returned false for '{options.ExecutablePath}' (no process resource started).");
                }
                processStarted = true;

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
                string outPath = options.StdoutPath;
                string errPath = options.StderrPath;

                // Setup StdOut Writer (Lazy Init)
                if (psi.RedirectStandardOutput && !string.IsNullOrWhiteSpace(outPath))
                {
                    process.OutputDataReceived += (_, e) =>
                    {
                        if (e.Data == null) return;

                        try
                        {
                            lock (stdoutLock)
                            {
                                if (stdoutWriterFailed) return;

                                if (stdoutWriter == null)
                                {
                                    FileStream stdoutFs = null;
                                    try
                                    {
                                        Helper.EnsureDirectoryExists(outPath);
                                        stdoutFs = new FileStream(outPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                                        stdoutWriter = new StreamWriter(stdoutFs, encoding) { AutoFlush = true };
                                    }
                                    catch (Exception ex)
                                    {
                                        stdoutFs?.Dispose();
                                        stdoutWriterFailed = true;
                                        logger.Error($"Disabling stdout capture for '{options.ExecutablePath}' after open failure: {ex.Message}", ex);
                                        return;
                                    }
                                }
                                stdoutWriter.WriteLine(e.Data);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log via the supervisor's logger, never propagate out of the handler
                            try { logger.Warn($"Failed to write stdout line for '{options.ExecutablePath}': {ex.Message}"); } catch { /* Fail-silent */ }
                        }
                    };
                }

                // Setup StdErr Writer (Lazy Init)
                if (psi.RedirectStandardError && !string.IsNullOrWhiteSpace(errPath))
                {
                    process.ErrorDataReceived += (_, e) =>
                    {
                        if (e.Data == null) return;

                        try
                        {
                            lock (stderrLock)
                            {
                                if (pathsMatch && !string.IsNullOrWhiteSpace(outPath))
                                {
                                    // Multiplexing into the same file
                                    if (stdoutWriterFailed) return;

                                    if (stdoutWriter == null)
                                    {
                                        FileStream sharedFs = null;
                                        try
                                        {
                                            Helper.EnsureDirectoryExists(outPath);
                                            sharedFs = new FileStream(outPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                                            stdoutWriter = new StreamWriter(sharedFs, encoding) { AutoFlush = true };
                                        }
                                        catch (Exception ex)
                                        {
                                            sharedFs?.Dispose();
                                            stdoutWriterFailed = true;
                                            logger.Error($"Disabling multiplexed stdout/stderr capture for '{options.ExecutablePath}' after open failure: {ex.Message}", ex);
                                            return;
                                        }
                                    }
                                    stdoutWriter.WriteLine(e.Data);
                                }
                                else
                                {
                                    // Independent file
                                    if (stderrWriterFailed) return;

                                    if (stderrWriter == null)
                                    {
                                        FileStream stderrFs = null;
                                        try
                                        {
                                            Helper.EnsureDirectoryExists(errPath);
                                            stderrFs = new FileStream(errPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                                            stderrWriter = new StreamWriter(stderrFs, encoding) { AutoFlush = true };
                                        }
                                        catch (Exception ex)
                                        {
                                            stderrFs?.Dispose();
                                            stderrWriterFailed = true;
                                            logger.Error($"Disabling stderr capture for '{options.ExecutablePath}' after open failure: {ex.Message}", ex);
                                            return;
                                        }
                                    }
                                    stderrWriter.WriteLine(e.Data);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log via the supervisor's logger, never propagate out of the handler
                            try { logger.Warn($"Failed to write stderr line for '{options.ExecutablePath}': {ex.Message}"); } catch { /* Fail-silent */ }
                        }
                    };
                }

                if (psi.RedirectStandardOutput) process.BeginOutputReadLine();
                if (psi.RedirectStandardError) process.BeginErrorReadLine();

                // Synchronous mode: Wait for exit while pulsing the SCM
                WaitForExitWithHeartbeat(process, options, logger);

                // Drain async OutputDataReceived/ErrorDataReceived events with a bounded wait.
                // Process is already exited; this only flushes the event queue.
                try
                {
                    Task.Run(process.WaitForExit).Wait(AppConfig.OutputDrainTimeoutMs);
                }
                catch { /* fail-silent - drain is best-effort */ }

                returnedOwnership = true;
                return process;
            }
            catch (TimeoutException)
            {
                throw; // already logged at the configured severity inside WaitForExitWithHeartbeat
            }
            catch (Exception ex)
            {
                logger.Error($"Failed during synchronous execution or log flushing for '{options.ExecutablePath}'.", ex);
                throw;
            }
            finally
            {
                // Dispose writers safely to release file locks.
                try { stdoutWriter?.Dispose(); }
                catch (Exception ex) { logger.Warn($"Failed to dispose stdout writer: {ex.Message}"); }

                if (!pathsMatch)
                {
                    try { stderrWriter?.Dispose(); }
                    catch (Exception ex) { logger.Warn($"Failed to dispose stderr writer: {ex.Message}"); }
                }

                if (!returnedOwnership && process != null)
                {
                    try
                    {
                        // ROBUSTNESS: If we didn't successfully return ownership, the process is orphaned.
                        // We must kill the process tree before disposing the wrapper to avoid leaking child processes.
                        // Check if the process actually started and is still running.
                        // process.Start() is the first entry in the try block; if any subsequent logic throws,
                        // we must ensure the child does not remain active and unsupervised.
                        if (processStarted && !process.HasExited)
                            process.Kill(true);
                    }
                    catch (Exception killEx)
                    {
                        // Log but don't rethrow, as we need the original exception to propagate.
                        logger.Warn($"Failed to kill orphaned child after launch failure: {killEx.Message}");
                    }
                    process.Dispose();   // always dispose the wrapper we own
                }
            }
        }

        /// <summary>
        /// Performs a synchronous wait for process exit while periodically updating the Windows SCM to prevent service timeouts.
        /// </summary>
        private static void WaitForExitWithHeartbeat(IProcessWrapper process, ProcessLaunchOptions options, IServyLogger logger)
        {
            // Fail fast with a clear contract violation
            if (!options.FireAndForget && options.WaitChunkMs <= 0)
            {
                throw new ArgumentException(
                    "Synchronous launch requires WaitChunkMs > 0.",
                    nameof(options));
            }

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

            string fileNameOnly = Path.GetFileNameWithoutExtension(psi.FileName);

            // Python Logic: 
            // Matches 'python', 'pythonw', 'python2', 'python3', or 'python3.x' patterns.
            bool isPython;
            try { isPython = PythonExeRegex.IsMatch(fileNameOnly); }
            catch (RegexMatchTimeoutException ex)
            {
                Logger.Warn($"ApplyLanguageFixes: Python detection regex timed out on '{fileNameOnly}' ({ex.Message}); assuming not Python.");
                isPython = false;
            }

            if (isPython)
            {
                SetIfMissing(psi, "PYTHONLEGACYWINDOWSSTDIO", "0");
                SetIfMissing(psi, "PYTHONIOENCODING", "utf-8");
                SetIfMissing(psi, "PYTHONUTF8", "1");
                SetIfMissing(psi, "PYTHONUNBUFFERED", "1");
            }

            // Java Logic: 
            // Separate 'javac' (Java Compiler) from 'java'/'javaw' (Java Runtime Engines)
            // to support distinct flag formatting boundaries (-J-D vs -D).
            bool isJavaRuntime =
                string.Equals(fileNameOnly, "java", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileNameOnly, "javaw", StringComparison.OrdinalIgnoreCase);

            bool isJavaCompiler = string.Equals(fileNameOnly, "javac", StringComparison.OrdinalIgnoreCase);

            if (isJavaRuntime || isJavaCompiler)
            {
                string currentArgs = psi.Arguments ?? string.Empty;

                bool hasEncoding;
                try
                {
                    hasEncoding = JavaFileEncodingRegex.IsMatch(currentArgs);
                }
                catch (RegexMatchTimeoutException ex)
                {
                    Logger.Warn($"ApplyLanguageFixes: -Dfile.encoding detection regex timed out on Java arguments ({ex.Message}); assuming not present.");
                    hasEncoding = false;
                }

                if (!hasEncoding)
                {
                    if (isJavaCompiler)
                    {
                        // Prepend with the critical -J flag to prevent javac from rejecting the system property flag option
                        psi.Arguments = $"-J-Dfile.encoding=UTF-8 {currentArgs}".Trim();
                    }
                    else
                    {
                        // Standard bare declaration assignment for java/javaw
                        psi.Arguments = $"-Dfile.encoding=UTF-8 {currentArgs}".Trim();
                    }
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
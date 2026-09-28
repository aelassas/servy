using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Core.Security
{
    /// <summary>
    /// Runs <c>Set-ServyExePermissions.ps1</c> for a service account, so the account gets Modify on
    /// <c>%ProgramData%\Servy</c> while Servy's binaries, configuration files and database stay locked down,
    /// without the user having to run the script by hand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In <c>DEBUG</c> builds the script is found in the repository's <c>setup</c> folder, by walking up from
    /// the application's base directory. In <c>RELEASE</c> builds it is shipped next to the desktop app, the
    /// Manager and the CLI, so it is looked up in the application's base directory.
    /// </para>
    /// <para>
    /// The script is run with Windows PowerShell from the system directory rather than whatever
    /// <c>powershell.exe</c> is first on <c>PATH</c>. The caller must be elevated, as every Servy process that
    /// installs a service already is.
    /// </para>
    /// </remarks>
    public class ServyExePermissionsHardener : IServyExePermissionsHardener
    {
        /// <summary>
        /// The repository folder that holds the script in <c>DEBUG</c> builds.
        /// </summary>
        internal const string SetupFolderName = "setup";

        /// <inheritdoc />
        public async Task<bool> HardenAsync(string targetAccount, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(targetAccount))
            {
                Logger.Warn("Executable permission hardening skipped: no target account was given.");
                return false;
            }

            var account = targetAccount.Trim();
            var scriptPath = ResolveScriptPath();

            if (scriptPath == null)
            {
                Logger.Warn($"{AppConfig.SetServyExePermissionsScriptFileName} was not found, so the permissions of Servy's binaries were not hardened for '{account}'. " +
                    $"Run {AppConfig.SetServyExePermissionsScriptFileName} -TargetAccount \"{account}\" from an elevated PowerShell session.");
                return false;
            }

            var startInfo = BuildStartInfo(GetPowerShellPath(), scriptPath, account);

            ExePermissionsScriptResult result;
            try
            {
                result = await RunScriptAsync(startInfo, AppConfig.SetServyExePermissionsTimeoutMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"Executable permission hardening for '{account}' was cancelled before {AppConfig.SetServyExePermissionsScriptFileName} completed.");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to run {AppConfig.SetServyExePermissionsScriptFileName} for '{account}'.", ex);
                return false;
            }

            return ReportResult(account, result);
        }

        /// <summary>
        /// Locates <c>Set-ServyExePermissions.ps1</c> for the running build.
        /// </summary>
        /// <returns>The full path to the script, or <see langword="null"/> when it cannot be found.</returns>
        protected virtual string? ResolveScriptPath()
        {
#if DEBUG
            return FindScriptPath(AppDomain.CurrentDomain.BaseDirectory, searchSetupFolders: true);
#else
            return FindScriptPath(AppDomain.CurrentDomain.BaseDirectory, searchSetupFolders: false);
#endif
        }

        /// <summary>
        /// Starts the script, captures its output and waits for it to exit.
        /// </summary>
        /// <param name="startInfo">The prepared start information, see <see cref="BuildStartInfo"/>.</param>
        /// <param name="timeoutMs">How long to wait before the script is stopped.</param>
        /// <param name="cancellationToken">A token that stops the script when cancelled.</param>
        /// <returns>The script's exit code (<see langword="null"/> on timeout) and its combined output.</returns>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        /// <exception cref="Win32Exception">Thrown when PowerShell cannot be started.</exception>
        protected virtual Task<ExePermissionsScriptResult> RunScriptAsync(ProcessStartInfo startInfo, int timeoutMs, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                using (var process = new Process { StartInfo = startInfo })
                {
                    var ioLock = new object();
                    var output = new StringBuilder();
                    DataReceivedEventHandler append = (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            lock (ioLock) { output.AppendLine(e.Data); }
                        }
                    };
                    process.OutputDataReceived += append;
                    process.ErrorDataReceived += append;

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    using (cancellationToken.Register(() => TryKill(process)))
                    {
                        if (!process.WaitForExit(timeoutMs))
                        {
                            TryKill(process);
                            process.WaitForExit(AppConfig.HandleExeKillDrainTimeoutMs);
                            lock (ioLock) { return new ExePermissionsScriptResult(null, output.ToString()); }
                        }
                    }

                    // WaitForExit() with no timeout flushes the in-flight asynchronous output handlers
                    process.WaitForExit();
                    cancellationToken.ThrowIfCancellationRequested();

                    lock (ioLock) { return new ExePermissionsScriptResult(process.ExitCode, output.ToString()); }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Finds <c>Set-ServyExePermissions.ps1</c> starting from <paramref name="baseDirectory"/>.
        /// </summary>
        /// <param name="baseDirectory">The application's base directory.</param>
        /// <param name="searchSetupFolders">
        /// <see langword="false"/> to look only in <paramref name="baseDirectory"/> (release layout, where the script
        /// ships next to the executable); <see langword="true"/> to look in the <c>setup</c> folder of
        /// <paramref name="baseDirectory"/> and of each of its parents (repository layout, used by debug builds).
        /// </param>
        /// <returns>The full path to the script, or <see langword="null"/> when it is not found.</returns>
        internal static string? FindScriptPath(string baseDirectory, bool searchSetupFolders)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                return null;

            if (!searchSetupFolders)
            {
                var candidate = Path.Combine(baseDirectory, AppConfig.SetServyExePermissionsScriptFileName);
                return File.Exists(candidate) ? Path.GetFullPath(candidate) : null;
            }

            var directory = new DirectoryInfo(baseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, SetupFolderName, AppConfig.SetServyExePermissionsScriptFileName);
                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            return null;
        }

        /// <summary>
        /// Builds the start information that runs the script non-interactively for <paramref name="targetAccount"/>.
        /// </summary>
        /// <param name="powerShellPath">The full path to <c>powershell.exe</c>.</param>
        /// <param name="scriptPath">The full path to <c>Set-ServyExePermissions.ps1</c>.</param>
        /// <param name="targetAccount">The account passed to <c>-TargetAccount</c>.</param>
        /// <returns>A <see cref="ProcessStartInfo"/> with redirected output and no window.</returns>
        internal static ProcessStartInfo BuildStartInfo(string powerShellPath, string scriptPath, string targetAccount)
        {
            // -File passes the account to the script as a literal value, so a gMSA's trailing '$' is not expanded.
            var arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Helper.Quote(scriptPath) +
                " -TargetAccount " + Helper.Quote(targetAccount);

            return new ProcessStartInfo
            {
                FileName = powerShellPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
        }

        /// <summary>
        /// Logs the script's outcome at the level its exit code calls for.
        /// </summary>
        /// <param name="account">The account the script was run for.</param>
        /// <param name="result">The script's exit code and output.</param>
        /// <returns><see langword="true"/> only for exit code 0.</returns>
        internal static bool ReportResult(string account, ExePermissionsScriptResult result)
        {
            var script = AppConfig.SetServyExePermissionsScriptFileName;
            var output = result.Output.Trim();

            switch (result.ExitCode)
            {
                case 0:
                    Logger.Info($"{script} hardened Servy's binaries, configuration files and database for '{account}'.");
                    Logger.Debug(output);
                    return true;
                case 2:
                    // Missing files are hardened by the next install for an account, once Servy has extracted them.
                    Logger.Warn($"{script} could not harden every file for '{account}' because some are not present yet. Output:{Environment.NewLine}{output}");
                    return false;
                case null:
                    Logger.Error($"{script} did not finish within {AppConfig.SetServyExePermissionsTimeoutMs / AppConfig.MillisecondsPerSecond} seconds for '{account}' and was stopped. Output:{Environment.NewLine}{output}");
                    return false;
                default:
                    Logger.Error($"{script} failed for '{account}' with exit code {result.ExitCode}. Output:{Environment.NewLine}{output}");
                    return false;
            }
        }

        /// <summary>
        /// Gets the path of Windows PowerShell in the system directory.
        /// </summary>
        /// <returns>The full path to <c>powershell.exe</c>.</returns>
        private static string GetPowerShellPath()
            => Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

        /// <summary>
        /// Stops the script if it is still running.
        /// </summary>
        /// <param name="process">The script's PowerShell process.</param>
        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch (InvalidOperationException)
            {
                // Already exited, or never started
            }
            catch (Win32Exception ex)
            {
                Logger.Warn($"Failed to stop {AppConfig.SetServyExePermissionsScriptFileName}: {ex.Message}");
            }
        }
    }
}

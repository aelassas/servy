using Servy.Core.Logging;
using Servy.UI.Localization;
using System.Diagnostics;
using System.Windows;

namespace Servy.UI.Helpers
{
    /// <summary>
    /// Restarts the running application: shuts it down and starts a new instance of the same executable.
    /// </summary>
    /// <remarks>
    /// The new instance is started from the <see cref="Application.Exit"/> event, which is raised after the
    /// windows are closed. The main window terminates every child process of the application when it closes,
    /// so an instance started earlier would be terminated with them.
    /// </remarks>
    public static class AppRestarter
    {
        /// <summary>
        /// Restarts the current application with the command-line arguments it was started with, except the
        /// <c>--lang</c> option: the language to use is the one stored in the settings file.
        /// </summary>
        public static void Restart()
        {
            Restart(Application.Current, GetExecutablePath(), Environment.GetCommandLineArgs().Skip(1), psi => Process.Start(psi));
        }

        /// <summary>
        /// Shuts <paramref name="app"/> down and starts <paramref name="executablePath"/> once it has exited its
        /// windows.
        /// </summary>
        /// <param name="app">The running application; <see langword="null"/> restarts nothing.</param>
        /// <param name="executablePath">The full path of the executable to start.</param>
        /// <param name="args">The command-line arguments of the running instance.</param>
        /// <param name="start">Starts a process from a <see cref="ProcessStartInfo"/>.</param>
        public static void Restart(Application? app, string? executablePath, IEnumerable<string>? args, Func<ProcessStartInfo, Process?> start)
        {
            if (app == null)
            {
                Logger.Warn("Restart requested without a running application; nothing was restarted.");
                return;
            }

            app.Exit += (sender, e) => StartNewInstance(executablePath, args, start);
            app.Shutdown();
        }

        /// <summary>
        /// Starts a new instance of the executable.
        /// </summary>
        /// <param name="executablePath">The full path of the executable to start.</param>
        /// <param name="args">The command-line arguments of the running instance; <c>--lang</c> is dropped.</param>
        /// <param name="start">Starts a process from a <see cref="ProcessStartInfo"/>.</param>
        /// <returns><see langword="true"/> when the new instance was started; a failure is logged, never thrown.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="start"/> is <see langword="null"/>.</exception>
        public static bool StartNewInstance(string? executablePath, IEnumerable<string>? args, Func<ProcessStartInfo, Process?> start)
        {
            if (start == null) throw new ArgumentNullException(nameof(start));

            if (string.IsNullOrWhiteSpace(executablePath))
            {
                Logger.Warn("Restart failed: the path of the running executable is unknown.");
                return false;
            }

            try
            {
                using (start(BuildStartInfo(executablePath, args)))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Restart failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Builds the start information of the new instance.
        /// </summary>
        /// <param name="executablePath">The full path of the executable to start.</param>
        /// <param name="args">The command-line arguments of the running instance; <c>--lang</c> and its value are dropped.</param>
        /// <returns>The start information, with each remaining argument quoted.</returns>
        public static ProcessStartInfo BuildStartInfo(string executablePath, IEnumerable<string>? args)
        {
            var kept = StartupCulture.StripLanguageArg(args).Select(arg => Servy.Core.Helpers.Helper.Quote(arg));

            return new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = string.Join(" ", kept),

                // The application runs elevated; the shell starts the new instance elevated without a prompt
                UseShellExecute = true,
            };
        }

        /// <summary>
        /// Returns the full path of the running executable, or <see langword="null"/> when it cannot be read.
        /// </summary>
        /// <returns>The path of the main module of the current process.</returns>
        public static string? GetExecutablePath()
        {
            try
            {
                using (var current = Process.GetCurrentProcess())
                {
                    return current.MainModule?.FileName;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"The path of the running executable could not be read: {ex.Message}");
                return null;
            }
        }
    }
}

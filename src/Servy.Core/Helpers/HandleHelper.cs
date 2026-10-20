using Servy.Core.Config;
using Servy.Core.Logging;
using Servy.Core.Native;
using System.ComponentModel;
using System.Diagnostics;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Finds the processes holding a file open, through the Windows Restart Manager API.
    /// </summary>
    /// <remarks>
    /// The Restart Manager reports every process that has the file open or mapped (a running executable or a loaded
    /// DLL included), for every user when the caller is elevated. It replaces the Sysinternals Handle executable
    /// (<c>handle64.exe</c> / <c>handle64a.exe</c>) earlier versions shipped and ran, so no external executable is
    /// extracted, verified or started to find a lock.
    /// </remarks>
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
            /// Gets or sets the process name, without the <c>.exe</c> extension.
            /// </summary>
            public string? ProcessName { get; set; }
        }

        /// <summary>
        /// The Restart Manager calls production makes.
        /// </summary>
        private static readonly IRestartManagerApi DefaultApi = new RestartManagerApi();

        /// <summary>
        /// Finds all processes that have the specified file open.
        /// </summary>
        /// <param name="filePath">Full path of the file to check for open handles.</param>
        /// <returns>
        /// One <see cref="ProcessHandleInfo"/> per process holding the file, in the order the Restart Manager reports
        /// them. A process that exited while the list was read, or whose identifier now belongs to another process,
        /// is left out. The list is empty when nothing holds the file or the file does not exist.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="filePath"/> is null, empty or not a valid path.</exception>
        /// <exception cref="Win32Exception">Thrown when a Restart Manager call fails; <see cref="Win32Exception.NativeErrorCode"/> holds its error code.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the list of processes keeps growing for <see cref="AppConfig.RestartManagerGetListMaxAttempts"/> attempts.</exception>
        public static List<ProcessHandleInfo> GetProcessesUsingFile(string filePath)
            => GetProcessesUsingFile(filePath, DefaultApi);

        /// <summary>
        /// Finds all processes that have the specified file open, through the given Restart Manager calls.
        /// </summary>
        /// <param name="filePath">Full path of the file to check for open handles.</param>
        /// <param name="api">The Restart Manager calls to make.</param>
        /// <returns>The processes holding the file; see <see cref="GetProcessesUsingFile(string)"/>.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="filePath"/> is null, empty or not a valid path.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="api"/> is <see langword="null"/>.</exception>
        /// <exception cref="Win32Exception">Thrown when a Restart Manager call fails.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the list of processes keeps growing for <see cref="AppConfig.RestartManagerGetListMaxAttempts"/> attempts.</exception>
        internal static List<ProcessHandleInfo> GetProcessesUsingFile(string filePath, IRestartManagerApi api)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath is null or empty", nameof(filePath));
            if (api == null)
                throw new ArgumentNullException(nameof(api));

            // The Restart Manager matches registered files by full path.
            var fullPath = Path.GetFullPath(filePath);

            int rc = api.StartSession(out uint session);
            if (rc != 0)
                throw new Win32Exception(rc, $"RmStartSession failed with error {rc}.");

            try
            {
                rc = api.RegisterFile(session, fullPath);
                if (rc != 0)
                    throw new Win32Exception(rc, $"RmRegisterResources failed for '{fullPath}' with error {rc}.");

                return ToProcessHandleInfos(GetList(api, session));
            }
            finally
            {
                // Sessions are a limited system-wide resource, so one is ended even when the lookup failed.
                int endRc = api.EndSession(session);
                if (endRc != 0)
                    Logger.Warn($"RmEndSession failed with error {endRc} after looking up the processes using '{fullPath}'.");
            }
        }

        /// <summary>
        /// Reads the complete list of processes using the registered file, growing the buffer when the Restart
        /// Manager reports that the list does not fit.
        /// </summary>
        /// <param name="api">The Restart Manager calls to make.</param>
        /// <param name="session">The session the file is registered with.</param>
        /// <returns>The entries the Restart Manager wrote.</returns>
        /// <exception cref="Win32Exception">Thrown when <c>RmGetList</c> fails with an error other than <see cref="Errors.ERROR_MORE_DATA"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the list keeps growing for <see cref="AppConfig.RestartManagerGetListMaxAttempts"/> attempts.</exception>
        private static NativeMethods.RM_PROCESS_INFO[] GetList(IRestartManagerApi api, uint session)
        {
            NativeMethods.RM_PROCESS_INFO[]? buffer = null;
            uint count = 0;

            for (int attempt = 0; attempt < AppConfig.RestartManagerGetListMaxAttempts; attempt++)
            {
                int rc = api.GetList(session, out uint needed, ref count, buffer);
                if (rc == 0)
                {
                    if (buffer == null || count == 0)
                        return Array.Empty<NativeMethods.RM_PROCESS_INFO>();

                    return buffer.Take((int)Math.Min(count, (uint)buffer.Length)).ToArray();
                }

                if (rc != Errors.ERROR_MORE_DATA)
                    throw new Win32Exception(rc, $"RmGetList failed with error {rc}.");

                // A process opened the file between the call that sized the buffer and this one: size it again.
                buffer = new NativeMethods.RM_PROCESS_INFO[needed];
                count = needed;
            }

            throw new InvalidOperationException(
                $"RmGetList still reported more processes after {AppConfig.RestartManagerGetListMaxAttempts} attempts.");
        }

        /// <summary>
        /// Turns the Restart Manager entries into one <see cref="ProcessHandleInfo"/> per live process, named after
        /// its executable.
        /// </summary>
        /// <param name="entries">The entries the Restart Manager wrote.</param>
        /// <returns>The processes still holding the file.</returns>
        private static List<ProcessHandleInfo> ToProcessHandleInfos(IEnumerable<NativeMethods.RM_PROCESS_INFO> entries)
        {
            var processes = new List<ProcessHandleInfo>();
            var seen = new HashSet<int>();

            foreach (var entry in entries)
            {
                int pid = entry.Process.dwProcessId;
                if (pid <= 0 || !seen.Add(pid))
                    continue;

                var info = Describe(pid, entry.Process.ProcessStartTime);
                if (info != null)
                    processes.Add(info);
            }

            return processes;
        }

        /// <summary>
        /// Names the process the Restart Manager reported, after checking it is still the same process.
        /// </summary>
        /// <param name="pid">The process identifier.</param>
        /// <param name="startTime">The creation time the Restart Manager reported for it.</param>
        /// <returns>
        /// The process, named after its executable (the name the critical-process safelist checks), or
        /// <see langword="null"/> when it has exited or its identifier now belongs to a process started later.
        /// </returns>
        private static ProcessHandleInfo? Describe(int pid, NativeMethods.FILETIME startTime)
        {
            try
            {
                using (var process = Process.GetProcessById(pid))
                {
                    if (!IsSameProcess(process, startTime))
                    {
                        Logger.Debug($"PID {pid} was reused after the Restart Manager listed it as holding the file; skipping it.");
                        return null;
                    }

                    return new ProcessHandleInfo { ProcessId = pid, ProcessName = process.ProcessName };
                }
            }
            catch (ArgumentException)
            {
                // The process exited after the list was read, so it no longer holds the file.
                return null;
            }
            catch (InvalidOperationException)
            {
                // The process exited between being opened and being named.
                return null;
            }
        }

        /// <summary>
        /// Determines whether a process is the one the Restart Manager reported, by its creation time.
        /// </summary>
        /// <param name="process">The process now running under the reported identifier.</param>
        /// <param name="startTime">The creation time the Restart Manager reported.</param>
        /// <returns>
        /// <see langword="false"/> only when both creation times are known and differ; a protected process whose
        /// creation time cannot be read is kept, so the critical-process safelist still sees its name.
        /// </returns>
        private static bool IsSameProcess(Process process, NativeMethods.FILETIME startTime)
        {
            long fileTime = ((long)startTime.dwHighDateTime << 32) | startTime.dwLowDateTime;
            if (fileTime == 0)
                return true;

            DateTime actualUtc;
            try
            {
                actualUtc = process.StartTime.ToUniversalTime();
            }
            catch (Exception ex) when (ex is Win32Exception || ex is NotSupportedException)
            {
                return true;
            }

            // Process.StartTime goes through local time, so allow for its rounding rather than compare ticks.
            return Math.Abs((actualUtc - DateTime.FromFileTimeUtc(fileTime)).TotalSeconds) < 1;
        }
    }
}

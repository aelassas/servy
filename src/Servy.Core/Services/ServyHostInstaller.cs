using Servy.Core.Common;
using Servy.Core.Config;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.Native;
using Servy.Core.ServiceDependencies;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using static Servy.Core.Native.Errors;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Services
{
    /// <inheritdoc />
    public class ServyHostInstaller : IServyHostInstaller
    {
        private readonly IWindowsServiceApi _windowsServiceApi;
        private readonly IWin32ErrorProvider _win32ErrorProvider;
        private readonly IServiceControllerProvider _serviceControllerProvider;
        private readonly TimeSpan _timeout;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServyHostInstaller"/> class.
        /// </summary>
        /// <param name="windowsServiceApi">The native Service Control Manager API.</param>
        /// <param name="win32ErrorProvider">Reads the last Win32 error.</param>
        /// <param name="serviceControllerProvider">Opens service controllers to read the status and wait for a state.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
        public ServyHostInstaller(IWindowsServiceApi windowsServiceApi, IWin32ErrorProvider win32ErrorProvider, IServiceControllerProvider serviceControllerProvider)
            : this(windowsServiceApi, win32ErrorProvider, serviceControllerProvider, TimeSpan.FromSeconds(AppConfig.ServyHostServiceTimeoutSeconds))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServyHostInstaller"/> class with a custom wait timeout.
        /// </summary>
        /// <param name="windowsServiceApi">The native Service Control Manager API.</param>
        /// <param name="win32ErrorProvider">Reads the last Win32 error.</param>
        /// <param name="serviceControllerProvider">Opens service controllers to read the status and wait for a state.</param>
        /// <param name="timeout">How long to wait for the service to start or stop.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
        internal ServyHostInstaller(IWindowsServiceApi windowsServiceApi, IWin32ErrorProvider win32ErrorProvider, IServiceControllerProvider serviceControllerProvider, TimeSpan timeout)
        {
            _windowsServiceApi = windowsServiceApi ?? throw new ArgumentNullException(nameof(windowsServiceApi));
            _win32ErrorProvider = win32ErrorProvider ?? throw new ArgumentNullException(nameof(win32ErrorProvider));
            _serviceControllerProvider = serviceControllerProvider ?? throw new ArgumentNullException(nameof(serviceControllerProvider));
            _timeout = timeout;
        }

        /// <summary>The file names of the Servy host executable in the .NET 10 and the .NET Framework 4.8 build.</summary>
        private static readonly string[] HostExecutableNames = { "Servy.Host.exe", "Servy.Host.Net48.exe" };

        /// <inheritdoc />
        public ServyHostServiceState GetState() => ReadRegistration().State;

        /// <inheritdoc />
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (var sc = _serviceControllerProvider.GetService(AppConfig.ServyHostServiceName))
            {
                try
                {
                    sc.Refresh();
                    if (sc.Status == ServiceControllerStatus.Stopped)
                        return;

                    if (sc.Status != ServiceControllerStatus.StopPending)
                        sc.Stop();
                }
                catch (InvalidOperationException ex) when (!IsInstalled(sc))
                {
                    Logger.Debug($"The '{AppConfig.ServyHostServiceName}' service is not installed; nothing to stop. {ex.Message}");
                    return;
                }

                await WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, cancellationToken);
                Logger.Info($"Stopped the '{AppConfig.ServyHostServiceName}' service.");
            }
        }

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (var sc = _serviceControllerProvider.GetService(AppConfig.ServyHostServiceName))
            {
                sc.Refresh();
                if (sc.Status == ServiceControllerStatus.Running)
                    return;

                if (sc.Status == ServiceControllerStatus.StopPending)
                    await WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, cancellationToken);

                if (sc.Status != ServiceControllerStatus.StartPending)
                    sc.Start();

                await WaitForStatusAsync(sc, ServiceControllerStatus.Running, cancellationToken);
                Logger.Info($"Started the '{AppConfig.ServyHostServiceName}' service.");
            }
        }

        /// <inheritdoc />
        public async Task<OperationResult> EnsureInstalledAndRunningAsync(string hostExePath, IServiceHelper serviceHelper, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(hostExePath))
                throw new ArgumentException("The path of the host executable is required.", nameof(hostExePath));
            if (serviceHelper == null) throw new ArgumentNullException(nameof(serviceHelper));

            cancellationToken.ThrowIfCancellationRequested();

            var stoppedServices = new List<string>();
            try
            {
                if (!File.Exists(hostExePath))
                    return Fail($"'{hostExePath}' does not exist, so the '{AppConfig.ServyHostServiceName}' service cannot be installed.");

                var (state, registeredPath) = ReadRegistration();

                // Never take over a service that is not the Servy host: an earlier version let a user install one under
                // this name (#7294). It is reported and left exactly as it is.
                if (state == ServyHostServiceState.Foreign)
                {
                    return Fail($"A service named '{AppConfig.ServyHostServiceName}' already exists and runs '{registeredPath}', not the Servy host " +
                        $"({AppConfig.ServyHostExe}). Servy leaves it untouched. Rename or uninstall it: Servy needs this name for its host service. " +
                        "If an earlier version of Servy installed it, export it, uninstall it, and import it under another name.");
                }

                if (state == ServyHostServiceState.Unknown)
                {
                    return Fail($"The '{AppConfig.ServyHostServiceName}' service exists, but its executable could not be read, so Servy cannot " +
                        "confirm it is the Servy host and leaves it untouched.");
                }

                // Installed with another executable (a switch between the net10 and the net48 build, or between Debug and
                // Release): stop every Servy service and the host before the service is pointed at this build's executable
                if (state == ServyHostServiceState.ServyHost && !IsSameExecutable(registeredPath!, hostExePath))
                {
                    Logger.Info($"The '{AppConfig.ServyHostServiceName}' service runs '{registeredPath}' instead of '{hostExePath}'; restarting it from the expected path.");

                    stoppedServices = GetRunningServyServices(serviceHelper);
                    if (stoppedServices.Count > 0)
                    {
                        Logger.Info($"Stopping services before moving the '{AppConfig.ServyHostServiceName}' service: {string.Join(", ", stoppedServices)}");
                        await serviceHelper.StopServicesAsync(stoppedServices, cancellationToken);
                    }

                    await StopAsync(cancellationToken);
                }

                EnsureInstalledAndAutomatic(hostExePath);
                await StartAsync(cancellationToken);
                return OperationResult.Success();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to install or start the '{AppConfig.ServyHostServiceName}' service.", ex);
                return OperationResult.Failure($"Failed to install or start the '{AppConfig.ServyHostServiceName}' service: {ex.Message}");
            }
            finally
            {
                if (stoppedServices.Count > 0)
                {
                    try
                    {
                        Logger.Info($"Starting the services stopped to move the '{AppConfig.ServyHostServiceName}' service: {string.Join(", ", stoppedServices)}");
                        await serviceHelper.StartServicesAsync(stoppedServices, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Some services failed to restart after moving the '{AppConfig.ServyHostServiceName}' service.", ex);
                    }
                }
            }
        }

        /// <inheritdoc />
        public Task<int> EnsureServicesDependOnHostAsync(IEnumerable<string> serviceNames, CancellationToken cancellationToken = default)
        {
            if (serviceNames == null) throw new ArgumentNullException(nameof(serviceNames));

            int updated = 0;
            using (var scm = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_CONNECT))
            {
                if (scm == null || scm.IsInvalid)
                {
                    Logger.Error("Failed to open the Service Control Manager to add the Servy host dependency.", new Win32Exception(_win32ErrorProvider.GetLastWin32Error()));
                    return Task.FromResult(0);
                }

                foreach (var name in serviceNames.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (string.Equals(name.Trim(), AppConfig.ServyHostServiceName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        using (var service = _windowsServiceApi.OpenService(scm, name, SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG))
                        {
                            if (service == null || service.IsInvalid)
                            {
                                var err = _win32ErrorProvider.GetLastWin32Error();
                                if (err != ERROR_SERVICE_DOES_NOT_EXIST)
                                    Logger.Warn($"Could not open service '{name}' to add the '{AppConfig.ServyHostServiceName}' dependency. Win32 error: {err}");
                                continue;
                            }

                            // The RAW lpDependencies, never ServiceController.ServicesDependedOn: the BCL expands a
                            // '+Group' load-order entry into the group's current members, so writing that back would
                            // turn "any member of the group" into "every one of them".
                            var dependencies = QueryDependencies(service, name);
                            if (dependencies == null)
                                continue;

                            if (dependencies.Contains(AppConfig.ServyHostServiceName, StringComparer.OrdinalIgnoreCase))
                                continue;

                            var lpDependencies = ServiceDependenciesParser.ParseWithRequired(string.Join(";", dependencies), AppConfig.ServyHostServiceName);
                            if (!_windowsServiceApi.ChangeServiceConfig(service, SERVICE_NO_CHANGE, SERVICE_NO_CHANGE, SERVICE_NO_CHANGE, null, null, IntPtr.Zero, lpDependencies, null, null, null))
                            {
                                Logger.Warn($"Could not add the '{AppConfig.ServyHostServiceName}' dependency to service '{name}'. Win32 error: {_win32ErrorProvider.GetLastWin32Error()}");
                                continue;
                            }
                        }

                        updated++;
                        Logger.Info($"Service '{name}' now depends on the '{AppConfig.ServyHostServiceName}' service.");
                    }
                    catch (InvalidOperationException)
                    {
                        // Not installed in the SCM (a database-only record): nothing to update
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Could not add the '{AppConfig.ServyHostServiceName}' dependency to service '{name}'.", ex);
                    }
                }
            }

            return Task.FromResult(updated);
        }

        /// <summary>
        /// Reads what is registered under the Servy host service name.
        /// </summary>
        /// <returns>The state of the name, and the command line the Service Control Manager runs when it could be read.</returns>
        internal (ServyHostServiceState State, string? CommandLine) ReadRegistration()
        {
            try
            {
                using (var scm = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_CONNECT))
                {
                    if (scm == null || scm.IsInvalid)
                    {
                        Logger.Warn($"Could not open the Service Control Manager to read the '{AppConfig.ServyHostServiceName}' service's executable. Win32 error: {_win32ErrorProvider.GetLastWin32Error()}");
                        return (ServyHostServiceState.Unknown, null);
                    }

                    using (var service = _windowsServiceApi.OpenService(scm, AppConfig.ServyHostServiceName, SERVICE_QUERY_CONFIG))
                    {
                        if (service == null || service.IsInvalid)
                        {
                            var err = _win32ErrorProvider.GetLastWin32Error();
                            if (err == ERROR_SERVICE_DOES_NOT_EXIST)
                                return (ServyHostServiceState.NotInstalled, null);

                            Logger.Warn($"Could not open the '{AppConfig.ServyHostServiceName}' service to read its executable. Win32 error: {err}");
                            return (ServyHostServiceState.Unknown, null);
                        }

                        var commandLine = QueryCommandLine(service);
                        if (string.IsNullOrWhiteSpace(commandLine))
                            return (ServyHostServiceState.Unknown, null);

                        return (IsServyHostExecutable(commandLine!) ? ServyHostServiceState.ServyHost : ServyHostServiceState.Foreign, commandLine);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not read the '{AppConfig.ServyHostServiceName}' service's executable: {ex.Message}");
                return (ServyHostServiceState.Unknown, null);
            }
        }

        /// <summary>
        /// Reads a service's dependency list with the two-pass <c>QueryServiceConfig</c>, exactly as the Service
        /// Control Manager stores it.
        /// </summary>
        /// <param name="service">A handle opened with <c>SERVICE_QUERY_CONFIG</c>.</param>
        /// <param name="name">The service name, for the warning logged when the configuration cannot be read.</param>
        /// <returns>
        /// The raw dependency entries, or <see langword="null"/> when the configuration cannot be read (logged).
        /// An entry prefixed with <c>+</c> is a load-order group, and it is returned as written.
        /// </returns>
        /// <remarks>
        /// This is deliberately not <c>ServiceController.ServicesDependedOn</c>: that property resolves a
        /// <c>+GroupName</c> entry into the services that happen to be in the group, which turns "start after any
        /// member of the group" into "start after every one of them" as soon as the list is written back.
        /// </remarks>
        private List<string>? QueryDependencies(SafeServiceHandle service, string name)
        {
            // Pass 1 reports the size; pass 2 fills the buffer
            _windowsServiceApi.QueryServiceConfig(service, IntPtr.Zero, 0, out int bytesNeeded);
            if (bytesNeeded <= 0)
            {
                Logger.Warn($"Could not read the configuration of service '{name}' to add the '{AppConfig.ServyHostServiceName}' dependency. Win32 error: {_win32ErrorProvider.GetLastWin32Error()}");
                return null;
            }

            IntPtr buffer = Marshal.AllocHGlobal(bytesNeeded);
            try
            {
                if (!_windowsServiceApi.QueryServiceConfig(service, buffer, bytesNeeded, out _))
                {
                    Logger.Warn($"Could not read the configuration of service '{name}' to add the '{AppConfig.ServyHostServiceName}' dependency. Win32 error: {_win32ErrorProvider.GetLastWin32Error()}");
                    return null;
                }

                var config = Marshal.PtrToStructure<QUERY_SERVICE_CONFIG>(buffer);
                return ReadMultiSz(config.lpDependencies);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>
        /// Reads a Windows <c>MULTI_SZ</c> value: null-terminated strings in sequence, closed by an empty one.
        /// </summary>
        /// <param name="multiSz">A pointer to the first string, or <see cref="IntPtr.Zero"/>.</param>
        /// <returns>The strings it holds, empty when the pointer is <see cref="IntPtr.Zero"/> or the value is empty.</returns>
        private static List<string> ReadMultiSz(IntPtr multiSz)
        {
            var values = new List<string>();
            if (multiSz == IntPtr.Zero)
                return values;

            int offset = 0;
            while (true)
            {
                var value = Marshal.PtrToStringUni(IntPtr.Add(multiSz, offset));
                if (string.IsNullOrEmpty(value))
                    break;

                values.Add(value!);
                offset += (value!.Length + 1) * sizeof(char);
            }

            return values;
        }

        /// <summary>
        /// Reads a service's command line with the two-pass <c>QueryServiceConfig</c>.
        /// </summary>
        /// <param name="service">A handle opened with <c>SERVICE_QUERY_CONFIG</c>.</param>
        /// <returns>The command line, or <see langword="null"/> when it cannot be read (logged).</returns>
        private string? QueryCommandLine(SafeServiceHandle service)
        {
            // Pass 1 reports the size; pass 2 fills the buffer
            _windowsServiceApi.QueryServiceConfig(service, IntPtr.Zero, 0, out int bytesNeeded);
            if (bytesNeeded <= 0)
            {
                Logger.Warn($"Could not read the '{AppConfig.ServyHostServiceName}' service's configuration. Win32 error: {_win32ErrorProvider.GetLastWin32Error()}");
                return null;
            }

            IntPtr buffer = Marshal.AllocHGlobal(bytesNeeded);
            try
            {
                if (!_windowsServiceApi.QueryServiceConfig(service, buffer, bytesNeeded, out _))
                {
                    Logger.Warn($"Could not read the '{AppConfig.ServyHostServiceName}' service's configuration. Win32 error: {_win32ErrorProvider.GetLastWin32Error()}");
                    return null;
                }

                var config = Marshal.PtrToStructure<QUERY_SERVICE_CONFIG>(buffer);
                return Marshal.PtrToStringUni(config.lpBinaryPathName);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>
        /// Extracts the executable of a service command line, the way the Service Control Manager and the restarter
        /// (<c>Program.IsServyWrapperImagePath</c>) read it: the quoted part, or the first space-delimited token.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        /// <returns>The executable path, or <see langword="null"/> when a quote is not closed.</returns>
        internal static string? GetExecutable(string commandLine)
        {
            var value = commandLine.Trim();
            if (value.StartsWith("\"", StringComparison.Ordinal))
            {
                int end = value.IndexOf('"', 1);
                return end > 0 ? value.Substring(1, end - 1) : null;
            }

            int space = value.IndexOf(' ');
            return space < 0 ? value : value.Substring(0, space);
        }

        /// <summary>
        /// Determines whether a service command line runs a Servy host executable, of either build.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        /// <returns><see langword="true"/> when its executable's file name is <c>Servy.Host.exe</c> or <c>Servy.Host.Net48.exe</c>.</returns>
        internal static bool IsServyHostExecutable(string commandLine)
        {
            var executable = GetExecutable(commandLine);
            if (executable == null)
                return false;

            string fileName;
            try
            {
                fileName = Path.GetFileName(executable);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return HostExecutableNames.Contains(fileName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether a service command line runs the given executable.
        /// </summary>
        /// <param name="commandLine">The command line the Service Control Manager runs, quoted or not.</param>
        /// <param name="exePath">The expected executable.</param>
        /// <returns><see langword="true"/> when the command line's executable is <paramref name="exePath"/>.</returns>
        internal static bool IsSameExecutable(string commandLine, string exePath)
        {
            try
            {
                var executable = GetExecutable(commandLine);
                return executable != null
                    && string.Equals(Path.GetFullPath(executable), Path.GetFullPath(exePath), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return false;
            }
        }

        /// <inheritdoc />
        public List<string> GetRunningServyServices(IServiceHelper serviceHelper)
        {
            if (serviceHelper == null) throw new ArgumentNullException(nameof(serviceHelper));

            var names = new List<string>(serviceHelper.GetRunningServyServices() ?? new List<string>());
            try
            {
                foreach (var sc in _serviceControllerProvider.GetServices())
                {
                    using (sc)
                    {
                        try
                        {
                            if (sc.Status != ServiceControllerStatus.Running && sc.Status != ServiceControllerStatus.StartPending)
                                continue;

                            if (sc.GetDependencyNames().Contains(AppConfig.ServyHostServiceName, StringComparer.OrdinalIgnoreCase))
                                names.Add(sc.ServiceName);
                        }
                        catch (InvalidOperationException)
                        {
                            // Removed while enumerating
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not list the services that depend on the '{AppConfig.ServyHostServiceName}' service: {ex.Message}");
            }

            return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Creates the service, or reconfigures it: Automatic (not delayed) start, Local System, and the given executable.
        /// </summary>
        /// <param name="hostExePath">The full path of <c>Servy.Host.exe</c>.</param>
        /// <exception cref="Win32Exception">Thrown when the Service Control Manager refuses an operation.</exception>
        private void EnsureInstalledAndAutomatic(string hostExePath)
        {
            string binPath = Helper.Quote(hostExePath);

            using (var scm = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_CONNECT | SC_MANAGER_CREATE_SERVICE))
            {
                if (scm == null || scm.IsInvalid)
                    throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), "Failed to open Service Control Manager.");

                const uint access = SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG | SERVICE_START | (uint)SERVICE_QUERY_STATUS;
                using (var existing = _windowsServiceApi.OpenService(scm, AppConfig.ServyHostServiceName, access))
                {
                    if (existing != null && !existing.IsInvalid)
                    {
                        // Startup type Automatic and the executable this app extracted; the account stays Local System
                        if (!_windowsServiceApi.ChangeServiceConfig(existing, SERVICE_NO_CHANGE, SERVICE_AUTO_START, SERVICE_NO_CHANGE, binPath, null, IntPtr.Zero, null, ServiceAccounts.LocalSystem, null, null))
                            throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), $"Failed to configure the '{AppConfig.ServyHostServiceName}' service.");

                        SetNotDelayed(existing);
                        Logger.Debug($"The '{AppConfig.ServyHostServiceName}' service is installed; its startup type is Automatic.");
                        return;
                    }

                    var openError = _win32ErrorProvider.GetLastWin32Error();
                    if (openError != ERROR_SERVICE_DOES_NOT_EXIST)
                        throw new Win32Exception(openError, $"Failed to open the '{AppConfig.ServyHostServiceName}' service.");
                }

                using (var created = _windowsServiceApi.CreateService(
                    hSCManager: scm,
                    lpServiceName: AppConfig.ServyHostServiceName,
                    lpDisplayName: AppConfig.ServyHostDisplayName,
                    dwDesiredAccess: access,
                    dwServiceType: SERVICE_WIN32_OWN_PROCESS,
                    dwStartType: SERVICE_AUTO_START,
                    dwErrorControl: SERVICE_ERROR_NORMAL,
                    lpBinaryPathName: binPath,
                    lpLoadOrderGroup: null,
                    lpdwTagId: IntPtr.Zero,
                    lpDependencies: null,
                    lpServiceStartName: null, // Local System
                    lpPassword: null))
                {
                    if (created == null || created.IsInvalid)
                        throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), $"Failed to create the '{AppConfig.ServyHostServiceName}' service.");

                    SetDescription(created);
                    Logger.Info($"Installed the '{AppConfig.ServyHostServiceName}' service ({binPath}).");
                }
            }
        }

        /// <summary>
        /// Sets the service's description; a failure is only logged.
        /// </summary>
        /// <param name="service">The service handle.</param>
        private void SetDescription(SafeServiceHandle service)
        {
            var pDescription = Marshal.StringToHGlobalUni(AppConfig.ServyHostDescription);
            try
            {
                var description = new SERVICE_DESCRIPTION { lpDescription = pDescription };
                if (!_windowsServiceApi.ChangeServiceConfig2(service, SERVICE_CONFIG_DESCRIPTION, ref description))
                    Logger.Warn($"Could not set the description of the '{AppConfig.ServyHostServiceName}' service. Win32 error: {_win32ErrorProvider.GetLastWin32Error()}");
            }
            finally
            {
                Marshal.FreeHGlobal(pDescription);
            }
        }

        /// <summary>
        /// Clears the delayed flag, so the startup type is plain Automatic; a failure is only logged.
        /// </summary>
        /// <param name="service">The service handle.</param>
        private void SetNotDelayed(SafeServiceHandle service)
        {
            var info = new SERVICE_DELAYED_AUTO_START_INFO { fDelayedAutostart = false };
            if (!_windowsServiceApi.ChangeServiceConfig2(service, SERVICE_CONFIG_DELAYED_AUTO_START_INFO, ref info))
                Logger.Warn($"Could not clear the delayed start of the '{AppConfig.ServyHostServiceName}' service. Win32 error: {_win32ErrorProvider.GetLastWin32Error()}");
        }

        /// <summary>
        /// Waits for a service to reach a status, polling so the wait can be cancelled.
        /// </summary>
        /// <param name="sc">The service controller.</param>
        /// <param name="status">The status to wait for.</param>
        /// <param name="cancellationToken">A token that stops the wait.</param>
        /// <returns>A task that completes when the service reached the status.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the service did not reach it within the timeout.</exception>
        private async Task WaitForStatusAsync(IServiceControllerWrapper sc, ServiceControllerStatus status, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + _timeout;
            sc.Refresh();
            while (sc.Status != status)
            {
                if (DateTime.UtcNow >= deadline)
                    throw new InvalidOperationException($"The '{AppConfig.ServyHostServiceName}' service did not reach the {status} state within {_timeout.TotalSeconds:0} seconds.");

                await Task.Delay(AppConfig.ScmPollIntervalMs, cancellationToken);
                sc.Refresh();
            }
        }

        /// <summary>
        /// Determines whether a service controller refers to an installed service.
        /// </summary>
        /// <param name="sc">The controller.</param>
        /// <returns><see langword="false"/> when reading its status fails.</returns>
        private static bool IsInstalled(IServiceControllerWrapper sc)
        {
            try
            {
                _ = sc.Status;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Logs and builds a failed result.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <returns>The failed result.</returns>
        private static OperationResult Fail(string message)
        {
            Logger.Error(message);
            return OperationResult.Failure(message);
        }
    }
}

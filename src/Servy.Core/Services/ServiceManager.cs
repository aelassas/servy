using Servy.Core.Common;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using Servy.Core.NamedPipes;
using Servy.Core.Native;
using Servy.Core.Resources;
using Servy.Core.Security;
using Servy.Core.ServiceDependencies;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using static Servy.Core.Native.Errors;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Services
{
    /// <summary>
    /// Provides methods to install, uninstall, start, stop, restart, and update Windows services.
    /// Acts as the central, unified orchestration engine for Windows Service lifecycle operations.
    /// This hub deliberately aggregates low-level Windows Service Control Manager (SCM) interactions,
    /// Win32 P/Invoke boundaries, local system CRUD lifecycle management, parallel topology exploration,
    /// and service data synchronization mechanics.
    /// </summary>
    /// <remarks>
    /// Architectural Intent:
    /// Keeping these domains together puts every SCM mutation and its matching repository write in one method, so a
    /// partial failure between the two is detected and reported in place (see the state-drift errors InstallServiceAsync
    /// raises) instead of being left for the caller to reconcile. It does not make the two stores transactional.
    /// </remarks>
    public class ServiceManager : IServiceManager
    {
        #region Private Delegates

        /// <summary>
        /// Encapsulates a native Win32 or P/Invoke wrapper delegate used for two-pass service configuration queries,
        /// supporting both initial size-probing and memory buffer population calls.
        /// </summary>
        /// <param name="handle">A valid handle to the target Windows service.</param>
        /// <param name="buffer">A pointer to the allocated unmanaged buffer where configuration data is written, or <see cref="IntPtr.Zero"/> during size-probing.</param>
        /// <param name="bufferSize">The allocated size of the unmanaged buffer in bytes.</param>
        /// <param name="bytesNeeded">Receives the required buffer size in bytes if the buffer is insufficient, or the actual number of bytes written on success.</param>
        /// <returns><c>true</c> if the query operation succeeded; otherwise, <c>false</c>.</returns>
        private delegate bool QueryServiceConfigNativeDelegate(
            SafeServiceHandle handle,
            IntPtr buffer,
            int bufferSize,
            out int bytesNeeded);

        #endregion

        #region Private Fields

        private readonly Func<string, IServiceControllerWrapper> _controllerFactory;
        private readonly IServiceControllerProvider _serviceControllerProvider;
        private readonly IWindowsServiceApi _windowsServiceApi;
        private readonly IWin32ErrorProvider _win32ErrorProvider;
        private readonly IServiceRepository _serviceRepository;
        private readonly IServyExePermissionsHardener _exePermissionsHardener;
        private readonly INamedPipesService _namedPipesService;

        #endregion

        #region Internal Test Seams

        /// <summary>
        /// Gets or sets the clock <see cref="WaitForStatusAsync"/> measures its timeout budget against.
        /// Defaults to the real UTC clock; substituted by tests so the timeout arm can be reached without waiting for it.
        /// </summary>
        internal Func<DateTimeOffset> UtcNow { get; set; } = () => DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the delay <see cref="WaitForStatusAsync"/> awaits between Service Control Manager polls.
        /// Defaults to <see cref="Task.Delay(int, CancellationToken)"/>; substituted by tests so the timeout arm can be reached without waiting for it.
        /// </summary>
        internal Func<int, CancellationToken, Task> DelayAsync { get; set; } = Task.Delay;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceManager"/> class.
        /// </summary>
        /// <param name="controllerFactory">A factory function for creating service controller wrappers.</param>
        /// <param name="serviceControllerProvider">A provider to retrieve system service controllers.</param>
        /// <param name="windowsServiceApi">An abstraction over the native Windows Service APIs.</param>
        /// <param name="win32ErrorProvider">A provider to retrieve the last Win32 error code.</param>
        /// <param name="serviceRepository">The repository to store and read service configuration entities.</param>
        /// <param name="exePermissionsHardener">
        /// Hardens Servy's file permissions for the account of a service installed under an account other than
        /// Local System. <see langword="null"/> skips the hardening, which only tests should do.
        /// </param>
        /// <param name="namedPipesService">
        /// Asks the Servy host to rebuild its named pipe's DACL after a service account was added or removed, so the
        /// account of a newly installed service can connect and the account of a removed one no longer can.
        /// <see langword="null"/> skips it, which only tests should do; the host also rebuilds the DACL when it starts.
        /// </param>
        public ServiceManager(
            Func<string, IServiceControllerWrapper> controllerFactory,
            IServiceControllerProvider serviceControllerProvider,
            IWindowsServiceApi windowsServiceApi,
            IWin32ErrorProvider win32ErrorProvider,
            IServiceRepository serviceRepository,
            IServyExePermissionsHardener exePermissionsHardener = null,
            INamedPipesService namedPipesService = null
            )
        {
            _controllerFactory = controllerFactory ?? throw new ArgumentNullException(nameof(controllerFactory));
            _serviceControllerProvider = serviceControllerProvider ?? throw new ArgumentNullException(nameof(serviceControllerProvider));
            _windowsServiceApi = windowsServiceApi ?? throw new ArgumentNullException(nameof(windowsServiceApi));
            _win32ErrorProvider = win32ErrorProvider ?? throw new ArgumentNullException(nameof(win32ErrorProvider));
            _serviceRepository = serviceRepository ?? throw new ArgumentNullException(nameof(serviceRepository));
            _exePermissionsHardener = exePermissionsHardener;
            _namedPipesService = namedPipesService;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Updates the core configuration of an existing Windows service.
        /// </summary>
        /// <param name="scmHandle">A handle to the Service Control Manager database.</param>
        /// <param name="serviceName">The name of the service to configure.</param>
        /// <param name="description">The text description for the service.</param>
        /// <param name="binPath">The fully qualified path to the service binary executable.</param>
        /// <param name="startType">The startup type for the service.</param>
        /// <param name="username">The account under which the service will run.</param>
        /// <param name="password">The password for the account.</param>
        /// <param name="lpDependencies">A double null-terminated string of dependencies.</param>
        /// <param name="displayName">The display name to show in the Services console.</param>
        /// <exception cref="Win32Exception">Thrown when a native API call fails to open or change the service.</exception>
        internal void UpdateServiceConfig(
            SafeScmHandle scmHandle,
            string serviceName,
            string description,
            string binPath,
            ServiceStartType startType,
            string username,
            string password,
            string lpDependencies,
            string displayName
            )
        {
            using (var serviceHandle = _windowsServiceApi.OpenService(
                scmHandle,
                serviceName,
                SERVICE_CHANGE_CONFIG | SERVICE_QUERY_CONFIG))
            {
                // Note: Check serviceHandle for null to accommodate mock objects in unit tests.
                if (serviceHandle == null || serviceHandle.IsInvalid)
                {
                    throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), "Failed to open existing service.");
                }

                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = serviceName;
                }

                bool result = _windowsServiceApi.ChangeServiceConfig(
                    hService: serviceHandle,
                    dwServiceType: SERVICE_WIN32_OWN_PROCESS,
                    dwStartType: ToScmStartType(startType),
                    dwErrorControl: SERVICE_ERROR_NORMAL,
                    lpBinaryPathName: binPath,
                    lpLoadOrderGroup: null,
                    lpdwTagId: IntPtr.Zero,
                    lpDependencies: lpDependencies,
                    lpServiceStartName: username,
                    lpPassword: password,
                    lpDisplayName: displayName
                );

                if (!result)
                {
                    throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), "Failed to update service config.");
                }

                SetServiceDescription(serviceHandle, description);
            }
        }

        /// <summary>
        /// Sets the description text for a Windows service.
        /// </summary>
        /// <param name="serviceHandle">A valid handle to the target Windows service.</param>
        /// <param name="description">
        /// The description to assign. Pass null or an empty string to delete the current description;
        /// an empty string is marshalled as a non-NULL pointer to "" which Win32 treats as "delete description".
        /// </param>
        /// <exception cref="Win32Exception">Thrown if the native configuration change fails.</exception>
        internal void SetServiceDescription(SafeServiceHandle serviceHandle, string description)
        {
            IntPtr pDescription = IntPtr.Zero;
            try
            {
                // Win32: NULL lpDescription = leave unchanged, "" = delete. Normalise null to "" so a caller
                // asking to clear the description actually clears it.
                pDescription = Marshal.StringToHGlobalUni(description?.Trim() ?? string.Empty);

                var desc = new SERVICE_DESCRIPTION
                {
                    lpDescription = pDescription
                };

                if (!_windowsServiceApi.ChangeServiceConfig2(serviceHandle, SERVICE_CONFIG_DESCRIPTION, ref desc))
                {
                    int err = _win32ErrorProvider.GetLastWin32Error();
                    throw new Win32Exception(err, "Failed to set service description.");
                }
            }
            finally
            {
                if (pDescription != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pDescription);
                }
            }
        }

        /// <summary>
        /// Enables or disables the delayed auto-start setting for a Windows service.
        /// </summary>
        /// <param name="serviceHandle">A valid handle to the target Windows service.</param>
        /// <param name="delayedAutostart"><c>true</c> to delay the automatic startup; otherwise <c>false</c>.</param>
        /// <returns><c>true</c> if the change was successful; otherwise, <c>false</c>.</returns>
        private bool SetDelayedAutoStart(SafeServiceHandle serviceHandle, bool delayedAutostart)
        {
            var delayedInfo = new SERVICE_DELAYED_AUTO_START_INFO
            {
                fDelayedAutostart = delayedAutostart,
            };

            return _windowsServiceApi.ChangeServiceConfig2(
                serviceHandle,
                SERVICE_CONFIG_DELAYED_AUTO_START_INFO,
                ref delayedInfo
            );
        }

        /// <summary>
        /// Configures the service to accept pre-shutdown notifications and sets the maximum timeout
        /// the Service Control Manager (SCM) will wait for this service to stop during a system shutdown.
        /// </summary>
        /// <param name="serviceHandle">A valid handle to the target Windows service.</param>
        /// <param name="timeoutMs">The duration in milliseconds the SCM should wait.</param>
        /// <returns><c>true</c> if the pre-shutdown setting was successfully applied; otherwise, <c>false</c>.</returns>
        private bool EnablePreShutdown(SafeServiceHandle serviceHandle, uint timeoutMs)
        {
            var info = new SERVICE_PRE_SHUTDOWN_INFO
            {
                dwPreshutdownTimeout = timeoutMs
            };

            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.AllocHGlobal(Marshal.SizeOf<SERVICE_PRE_SHUTDOWN_INFO>());
                Marshal.StructureToPtr(info, ptr, false);

                return _windowsServiceApi.ChangeServiceConfig2(
                    serviceHandle,
                    SERVICE_CONFIG_PRESHUTDOWN_INFO,
                    ptr
                );
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        /// Polls the Service Control Manager until the target service transitions to the desired state or times out.
        /// </summary>
        /// <param name="sc">The service controller wrapper to monitor.</param>
        /// <param name="desired">The target status to reach.</param>
        /// <param name="timeoutSeconds">Maximum wait duration in seconds.</param>
        /// <param name="cancellationToken">Token to observe while polling.</param>
        /// <returns><c>true</c> if the service reached the desired status before timing out; otherwise, <c>false</c>.</returns>
        private async Task<bool> WaitForStatusAsync(
            IServiceControllerWrapper sc,
            ServiceControllerStatus desired,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            sc.Refresh();
            var deadline = UtcNow().AddSeconds(timeoutSeconds);
            while (sc.Status != desired)
            {
                if (UtcNow() >= deadline) return false;
                await DelayAsync(AppConfig.ScmPollIntervalMs, cancellationToken);
                sc.Refresh();
            }
            return true;
        }

        #endregion

        #region IServiceManager Implementation

        /// <inheritdoc />
        public async Task<OperationResult> InstallServiceAsync(InstallServiceOptions options, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.ServiceName)) throw new ArgumentException("Value is required.", nameof(options));
            if (string.IsNullOrWhiteSpace(options.WrapperExePath)) throw new ArgumentException("Value is required.", nameof(options));
            if (string.IsNullOrWhiteSpace(options.RealExePath)) throw new ArgumentException("Value is required.", nameof(options));

            // The Servy host service owns this name, and every Servy service depends on it
            if (Helper.IsReservedForServyHost(options.ServiceName))
            {
                string reservedError = string.Format(Strings.Msg_ServiceNameReservedForServyHost, options.ServiceName);
                Logger.Error(reservedError);
                return OperationResult.Failure(reservedError);
            }

            // HARDENING: Check database via UNICODE_NOCASE to intercept if this service or a linguistic
            // variation of it already exists before running native SCM queries.
            var existingDbService = await _serviceRepository.GetByNameAsync(options.ServiceName, decrypt: true, cancellationToken);
            bool isUpdateMode = existingDbService != null;

            // Track if we successfully executed an aggressive purge of a casing layout duplicate
            bool legacyDroppedFromDb = false;
            ServiceDto legacyBackupDto = null;

            // If the database has a record under a different casing layout (e.g. 'serviceä' vs 'serviceÄ'),
            // the Windows SCM will treat them as two entirely different entities. We must aggressively drop the old
            // casing layout registration from the OS before proceeding to avoid split-brain or orphaned processes.
            if (isUpdateMode && !string.Equals(existingDbService.Name, options.ServiceName, StringComparison.Ordinal))
            {
                Logger.Info($"Unicode name variance detected during update sequence ('{existingDbService.Name}' -> '{options.ServiceName}'). Executing full uninstallation sequence for the legacy casing variant from SCM and Database.");

                if (IsServiceInstalled(existingDbService.Name, cancellationToken: cancellationToken))
                {
                    try
                    {
                        // To prevent permanent data loss if the subsequent installation steps fail,
                        // we hold onto a deep copy or reference of the DTO before calling Uninstall.
                        legacyBackupDto = existingDbService;

                        var uninstallRes = await UninstallServiceAsync(existingDbService.Name, cancellationToken);
                        if (!uninstallRes.IsSuccess)
                        {
                            string uninstError = $"Failed to unregister legacy casing variant '{legacyBackupDto.Name}' from SCM: {uninstallRes.ErrorMessage}";
                            Logger.Error(uninstError);
                            return OperationResult.Failure(uninstError);
                        }

                        // UninstallServiceAsync succeeded, meaning the DB record for legacyBackupDto.Name is now deleted!
                        legacyDroppedFromDb = true;
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.Info($"Installation cancelled while dropping legacy casing variant '{existingDbService.Name}'.");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // Defense-in-depth: UninstallServiceAsync converts every non-OperationCanceledException
                        // into a Failure result before it can propagate, so nothing currently reaches this arm.
                        // It guards against a future change to that exception contract.
                        string criticalError = $"Unexpected error occurred while trying to drop legacy service casing layout '{existingDbService.Name}'.";
                        Logger.Error(criticalError, ex);
                        return OperationResult.Failure($"{criticalError} Details: {ex.Message}");
                    }
                }
                else
                {
                    try
                    {
                        // OS side already gone - still need to drop the stale, differently-cased DB row
                        // so the subsequent UpsertAsync performs a clean INSERT with the correct casing.
                        await _serviceRepository.DeleteAsync(existingDbService.Name, cancellationToken);
                        legacyDroppedFromDb = true;
                        legacyBackupDto = existingDbService;
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.Info($"Installation cancelled while dropping legacy casing variant '{existingDbService.Name}'.");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        string criticalError = $"Unexpected error occurred while trying to drop stale casing-variant row '{existingDbService.Name}'.";
                        Logger.Error(criticalError, ex);
                        return OperationResult.Failure($"{criticalError} Details: {ex.Message}");
                    }
                }
            }

            string binPath = string.Join(" ",
                Helper.Quote(options.WrapperExePath),
                Helper.Quote(options.ServiceName)
            );

            SafeScmHandle scmHandle = null;
            try
            {
                scmHandle = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_CONNECT | SC_MANAGER_CREATE_SERVICE);
                if (scmHandle == null || scmHandle.IsInvalid)
                {
                    throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), "Failed to open Service Control Manager.");
                }

                string displayName = string.IsNullOrWhiteSpace(options.DisplayName) ? options.ServiceName : options.DisplayName;

                SafeServiceHandle serviceHandle = null;
                try
                {
                    // Every Servy service depends on the Servy host service: the wrapper reads its configuration from it,
                    // so the SCM must start the host first and cannot stop it while a wrapper still runs.
                    string lpDependencies = ServiceDependenciesParser.ParseWithRequired(options.ServiceDependencies, AppConfig.ServyHostServiceName);
                    string lpServiceStartName = string.IsNullOrWhiteSpace(options.Username) ? ServiceAccounts.LocalSystem : options.Username.Trim();

                    // Use IsNullOrEmpty (not IsNullOrWhiteSpace) to preserve non-null
                    // whitespace-only passwords verbatim when configured for standard Windows accounts.
                    // ServiceAccounts.IsGmsa handles internal whitespace normalization for gMSA accounts ($) separately.
                    string lpPassword = string.IsNullOrEmpty(options.Password) ? null : options.Password;

                    bool isBuiltIn = ServiceAccounts.IsBuiltInServiceAccount(lpServiceStartName);
                    var isGmsa = ServiceAccounts.IsGmsa(lpServiceStartName, lpPassword);

                    if (!isBuiltIn && !isGmsa)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _windowsServiceApi.EnsureLogOnAsServiceRight(lpServiceStartName);
                        Logger.Info($"Ensured 'Log on as a service' right for account '{lpServiceStartName}' for service '{options.ServiceName}'.");
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    // Create the service if it does not exist
                    serviceHandle = _windowsServiceApi.CreateService(
                        hSCManager: scmHandle,
                        lpServiceName: options.ServiceName,
                        lpDisplayName: displayName,
                        dwDesiredAccess: SERVICE_START | SERVICE_STOP | SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG | SERVICE_DELETE | SERVICE_READ_CONTROL | SERVICE_WRITE_DAC,
                        dwServiceType: SERVICE_WIN32_OWN_PROCESS,
                        dwStartType: ToScmStartType(options.StartType),
                        dwErrorControl: SERVICE_ERROR_NORMAL,
                        lpBinaryPathName: binPath,
                        lpLoadOrderGroup: null,
                        lpdwTagId: IntPtr.Zero,
                        lpDependencies: lpDependencies,
                        lpServiceStartName: lpServiceStartName,
                        lpPassword: lpPassword
                    );

                    int createServiceError = _win32ErrorProvider.GetLastWin32Error();
                    bool serviceCreated = serviceHandle != null && !serviceHandle.IsInvalid;

                    // ROBUSTNESS: Establish a comprehensive try/catch/finally sequence immediately
                    // following native creation. This guarantees cleanup upon cancellation or async IO exceptions.
                    bool needsRollback = false;

                    try
                    {
                        // Persist service in database
                        var dto = new ServiceDto
                        {
                            Name = options.ServiceName,
                            DisplayName = !string.IsNullOrWhiteSpace(options.DisplayName) ? displayName : null,
                            Description = options.Description,
                            ExecutablePath = options.RealExePath,
                            StartupDirectory = options.StartupDirectory,
                            Parameters = options.RealArgs,
                            StartupType = (int)options.StartType,
                            Priority = (int)options.ProcessPriority,
                            CpuAffinity = options.CpuAffinity,
                            EnableConsoleUI = options.EnableConsoleUI,
                            StdoutPath = options.StdoutPath,
                            StderrPath = options.StderrPath,
                            EnableSizeRotation = options.EnableSizeRotation,
                            RotationSize = (int)(options.RotationSizeInBytes / AppConfig.BytesInMegabyte),
                            EnableDateRotation = options.EnableDateRotation,
                            DateRotationType = (int)options.DateRotationType,
                            MaxRotations = options.MaxRotations,
                            UseLocalTimeForRotation = options.UseLocalTimeForRotation,
                            EnableHealthMonitoring = options.EnableHealthMonitoring,
                            HeartbeatInterval = options.HeartbeatInterval,
                            MaxFailedChecks = options.MaxFailedChecks,
                            RecoveryAction = (int)options.RecoveryAction,
                            RecoveryOnCleanExit = options.RecoveryOnCleanExit,
                            MaxRestartAttempts = options.MaxRestartAttempts,
                            HeartbeatUrl = options.HeartbeatUrl,
                            HeartbeatUrlTimeoutSeconds = options.HeartbeatUrlTimeoutSeconds,
                            EnableHeartbeatUrlFlags = options.EnableHeartbeatUrlFlags,
                            FailureProgramPath = options.FailureProgramPath,
                            FailureProgramStartupDirectory = options.FailureProgramStartupDirectory,
                            FailureProgramParameters = options.FailureProgramExecutableArgs,
                            EnvironmentVariables = options.EnvironmentVariables,
                            ServiceDependencies = options.ServiceDependencies,
                            RunAsLocalSystem = string.IsNullOrWhiteSpace(options.Username),
                            UserAccount = options.Username,
                            Password = options.Password,
                            PreLaunchExecutablePath = options.PreLaunchExePath,
                            PreLaunchStartupDirectory = options.PreLaunchStartupDirectory,
                            PreLaunchParameters = options.PreLaunchArgs,
                            PreLaunchEnvironmentVariables = options.PreLaunchEnvironmentVariables,
                            PreLaunchStdoutPath = options.PreLaunchStdoutPath,
                            PreLaunchStderrPath = options.PreLaunchStderrPath,
                            PreLaunchTimeoutSeconds = options.PreLaunchTimeout,
                            PreLaunchRetryAttempts = options.PreLaunchRetryAttempts,
                            PreLaunchIgnoreFailure = options.PreLaunchIgnoreFailure,
                            PostLaunchExecutablePath = options.PostLaunchExePath,
                            PostLaunchStartupDirectory = options.PostLaunchStartupDirectory,
                            PostLaunchParameters = options.PostLaunchArgs,
                            EnableDebugLogs = options.EnableDebugLogs,
                            StartTimeout = options.StartTimeout,
                            StopTimeout = options.StopTimeout,
                            PreStopExecutablePath = options.PreStopExePath,
                            PreStopStartupDirectory = options.PreStopStartupDirectory,
                            PreStopParameters = options.PreStopArgs,
                            PreStopTimeoutSeconds = options.PreStopTimeout,
                            PreStopLogAsError = options.PreStopLogAsError,
                            PostStopExecutablePath = options.PostStopExePath,
                            PostStopStartupDirectory = options.PostStopStartupDirectory,
                            PostStopParameters = options.PostStopArgs,
                        };

                        cancellationToken.ThrowIfCancellationRequested();
                        var serviceDto = await _serviceRepository.GetByNameAsync(options.ServiceName, decrypt: false, cancellationToken);
                        dto.Pid = serviceDto?.Pid;

                        // The record whose account the revocation below compares against. The casing-variance block
                        // dropped the legacy row, and the lookup above is case-insensitive (UNICODE_NOCASE), so it
                        // cannot see it and serviceDto is null: without the fallback the legacy account would keep
                        // the vault grants. Revoking straight after the drop is not an option because
                        // ExecuteDatabaseRecoveryAsync can restore the row, so it waits for the success paths here.
                        // On the SCM arm of that block UninstallServiceAsync has already revoked, and a second pass
                        // finds no entry left to remove.
                        var formerService = serviceDto ?? (legacyDroppedFromDb ? legacyBackupDto : null);

                        int totalWaitTime = ServiceHelper.CalculateStopTimeout(
                            options.StopTimeout,
                            serviceDto?.PreviousStopTimeout,
                            string.IsNullOrEmpty(options.PreStopExePath) ? 0 : options.PreStopTimeout,
                            floorOverride: AppConfig.ScmStopTimeoutFloorSeconds);

                        uint finalTimeoutMs = (uint)totalWaitTime * AppConfig.MillisecondsPerSecond;

                        if (serviceCreated)
                        {
                            // Grant the service account the necessary rights to control the service
                            _windowsServiceApi.GrantServiceControlRights(serviceHandle, lpServiceStartName);

                            var enablePreShutdownConfigSuccess = EnablePreShutdown(serviceHandle, finalTimeoutMs);

                            if (enablePreShutdownConfigSuccess)
                            {
                                Logger.Info($"Pre-shutdown enabled with timeout of {totalWaitTime} seconds for service '{options.ServiceName}' during installation.");
                            }
                            else
                            {
                                string errorMsg = $"Failed to enable pre-shutdown for service '{options.ServiceName}' during installation. Rolling back creation.";
                                Logger.Error(errorMsg);
                                needsRollback = true;
                                throw new InvalidOperationException(errorMsg);
                            }

                            if (options.StartType == ServiceStartType.AutomaticDelayedStart)
                            {
                                var delayedAutoStartConfigSuccess = SetDelayedAutoStart(serviceHandle, true);

                                if (!delayedAutoStartConfigSuccess)
                                {
                                    string errorMsg = $"Failed to set delayed auto-start for service '{options.ServiceName}' during installation. Rolling back creation.";
                                    Logger.Error(errorMsg);
                                    needsRollback = true;
                                    throw new InvalidOperationException(errorMsg);
                                }
                                else
                                {
                                    Logger.Info($"Delayed auto-start enabled for service '{options.ServiceName}' during installation.");
                                }
                            }
                        }

                        if (!serviceCreated)
                        {
                            var isInstalled = IsServiceInstalled(options.ServiceName, cancellationToken: cancellationToken);
                            if (isInstalled)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                UpdateServiceConfig(
                                    scmHandle: scmHandle,
                                    serviceName: options.ServiceName,
                                    description: options.Description,
                                    binPath: binPath,
                                    startType: options.StartType,
                                    username: lpServiceStartName,
                                    password: lpPassword,
                                    lpDependencies: lpDependencies,
                                    displayName: displayName
                                );

                                // Open the existing service for configuration updates (delayed start and pre-shutdown) and for the
                                // DACL grant below, which needs SERVICE_READ_CONTROL and SERVICE_WRITE_DAC
                                using (var existingServiceHandle = _windowsServiceApi.OpenService(
                                    scmHandle,
                                    options.ServiceName,
                                    SERVICE_CHANGE_CONFIG | SERVICE_READ_CONTROL | SERVICE_WRITE_DAC))
                                {
                                    if (existingServiceHandle == null || existingServiceHandle.IsInvalid)
                                    {
                                        var err = _win32ErrorProvider.GetLastWin32Error();
                                        Logger.Error($"Failed to open service '{options.ServiceName}' for config update. Win32 error: {err}");
                                        throw new Win32Exception(err, $"Failed to open service '{options.ServiceName}' for configuration update. Error code: {err}");
                                    }

                                    // Take back the previous account's control rights before granting the new one's,
                                    // so a service moved to another account leaves nobody behind who can stop it
                                    RevokeServiceControlRightsIfAccountChanged(existingServiceHandle, formerService, lpServiceStartName);

                                    // Grant the service account the necessary rights to control the service
                                    _windowsServiceApi.GrantServiceControlRights(existingServiceHandle, lpServiceStartName);

                                    // 1. Update Pre-shutdown Timeout for existing service
                                    // This ensures that updates to StopTimeout or PreStopTimeout are reflected in the OS SCM.
                                    var preShutdownSuccess = EnablePreShutdown(existingServiceHandle, finalTimeoutMs);
                                    if (preShutdownSuccess)
                                    {
                                        Logger.Info($"Pre-shutdown timeout updated to {totalWaitTime} seconds for existing service '{options.ServiceName}'.");
                                    }
                                    else
                                    {
                                        // UpdateServiceConfig and ChangeServiceConfig2 have already mutated OS state.
                                        // We explicitly reject the state run and notify that manual structural reconciliation is required.
                                        string errorMsg = $"CRITICAL STATE DRIFT: Failed to update pre-shutdown timeout for existing service '{options.ServiceName}'. Core properties were modified but advanced configurations failed. The Servy database remains un-updated. Run re-installation immediately to prevent shutdown data corruption.";
                                        Logger.Error(errorMsg);
                                        throw new InvalidOperationException(errorMsg);
                                    }

                                    // 2. Update Delayed Auto-start
                                    var delayedAutostart = options.StartType == ServiceStartType.AutomaticDelayedStart;
                                    var success = SetDelayedAutoStart(existingServiceHandle, delayedAutostart);

                                    if (success)
                                    {
                                        Logger.Info($"Delayed auto-start {(delayedAutostart ? "enabled" : "disabled")} for existing service '{options.ServiceName}'.");
                                    }
                                    else
                                    {
                                        // UpdateServiceConfig has already committed baseline mutations to the SCM.
                                        // We escalate the diagnostic error to alert operators that the system is now out of sync.
                                        string errorMsg = $"CRITICAL STATE DRIFT: Failed to set delayed auto-start for existing service '{options.ServiceName}'. SCM configuration is now in an inconsistent state and database synchronization was aborted. Please re-run the full installer context to repair.";
                                        Logger.Error(errorMsg);
                                        throw new InvalidOperationException(errorMsg);
                                    }
                                }

                                cancellationToken.ThrowIfCancellationRequested();
                                await _serviceRepository.UpsertAsync(
                                    dto,
                                    preserveExistingRuntimeState: true,
                                    preserveExistingCredentials: false,
                                    cancellationToken);
                                Logger.Info($"Service '{options.ServiceName}' already exists. Updated its configuration.");
                                await HardenExePermissionsAsync(options.ServiceName, lpServiceStartName, cancellationToken);
                                await RevokeExePermissionsIfUnusedAsync(options.ServiceName, formerService, lpServiceStartName, cancellationToken);
                                await GrantPipeAccessAsync(options.ServiceName, lpServiceStartName, cancellationToken);
                                return OperationResult.Success();
                            }

                            string creationErrorMsg = $"Failed to create service '{options.ServiceName}'. Win32 error: {createServiceError}";
                            Logger.Error(creationErrorMsg);
                            throw new Win32Exception(createServiceError, creationErrorMsg);
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        SetServiceDescription(serviceHandle, options.Description);
                        await _serviceRepository.UpsertAsync(
                                                              dto,
                                                              preserveExistingRuntimeState: false,
                                                              preserveExistingCredentials: false,
                                                              cancellationToken); // New service: update runtime state in db (PID, ActiveStdoutPath, ActiveStderrPath)

                        Logger.Info($"Service '{options.ServiceName}' installed successfully.");
                        await HardenExePermissionsAsync(options.ServiceName, lpServiceStartName, cancellationToken);
                        await RevokeExePermissionsIfUnusedAsync(options.ServiceName, formerService, lpServiceStartName, cancellationToken);
                        await GrantPipeAccessAsync(options.ServiceName, lpServiceStartName, cancellationToken);
                        return OperationResult.Success();
                    }
                    catch
                    {
                        needsRollback = true;
                        throw;
                    }
                    finally
                    {
                        // Collapsed rollback handler: cleans up upon explicit Failure returns OR unhandled exceptions
                        if (needsRollback && serviceCreated && serviceHandle != null && !serviceHandle.IsInvalid)
                        {
                            try
                            {
                                if (!_windowsServiceApi.DeleteService(serviceHandle))
                                {
                                    int rollbackErr = _win32ErrorProvider.GetLastWin32Error();
                                    Logger.Error($"Rollback failed: DeleteService returned false for '{options.ServiceName}'. Win32 error: {rollbackErr}. Manual cleanup may be required.");
                                }
                            }
                            catch (Exception delEx)
                            {
                                Logger.Error($"Rollback raised an exception for '{options.ServiceName}'.", delEx);
                            }
                        }
                    }
                }
                finally
                {
                    serviceHandle?.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info($"Installation of '{options.ServiceName}' was cancelled by the user.");
                await ExecuteDatabaseRecoveryAsync(legacyDroppedFromDb, legacyBackupDto);
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error installing service '{options.ServiceName}'.", ex);
                await ExecuteDatabaseRecoveryAsync(legacyDroppedFromDb, legacyBackupDto);
                return OperationResult.Failure($"Error installing service '{options.ServiceName}': {ex.Message}");
            }
            finally
            {
                scmHandle?.Dispose();
            }
        }

        /// <summary>
        /// Hardens Servy's vault for the account a service was just installed under, unless it is Local System,
        /// which already has Full Control and needs no hardening. The account gets the log folder of this service and
        /// of every other service it runs, and nothing else under <c>logs\</c>.
        /// </summary>
        /// <param name="serviceName">The service that was installed; its log folder is granted to the account.</param>
        /// <param name="account">The account the service runs under, as passed to the Service Control Manager.</param>
        /// <param name="cancellationToken">A token that stops the hardening between two files.</param>
        /// <returns>A task that completes when the hardening has finished or failed.</returns>
        /// <remarks>
        /// The service is already installed when this runs, so a failure here is logged and never turned into a
        /// failed (and rolled back) installation.
        /// </remarks>
        private async Task HardenExePermissionsAsync(string serviceName, string account, CancellationToken cancellationToken)
        {
            if (_exePermissionsHardener == null || !ServyExePermissionsHardener.IsHardeningCandidate(account))
                return;

            try
            {
                if (!await _exePermissionsHardener.HardenServiceAsync(serviceName, account, _serviceRepository, cancellationToken))
                {
                    Logger.Warn($"Servy's file permissions were not fully hardened for '{account}' (service '{serviceName}'). " +
                        "See the log above; installing the service again re-applies the hardening.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Hardening Servy's file permissions for '{account}' (service '{serviceName}') failed.", ex);
            }
        }

        /// <inheritdoc />
        public Task RevokeVaultAccessIfUnusedAsync(ServiceDto formerService, CancellationToken cancellationToken = default)
        {
            return RevokeExePermissionsIfUnusedAsync(formerService?.Name ?? string.Empty, formerService, null, cancellationToken);
        }

        /// <summary>
        /// Revokes the access of the account a service ran under before it was uninstalled or moved to another account
        /// to that service's log folder, and its vault access and Servy host named pipe access as well unless a
        /// remaining service still runs under it (#7161).
        /// </summary>
        /// <param name="serviceName">The service that was uninstalled or reconfigured; its log folder is revoked.</param>
        /// <param name="formerService">The service's record as it was before the change; <see langword="null"/> when
        /// there was none, which revokes nothing.</param>
        /// <param name="currentAccount">The account the service runs under now, or <see langword="null"/> after an
        /// uninstall. The same account (compared case-insensitively) revokes nothing.</param>
        /// <param name="cancellationToken">A token that stops the revocation between two items.</param>
        /// <returns>A task that completes when the revocation has finished or failed.</returns>
        /// <remarks>
        /// The uninstall or install has already succeeded when this runs, so a failure here is logged and never turns
        /// it into a failed operation.
        /// </remarks>
        private async Task RevokeExePermissionsIfUnusedAsync(string serviceName, ServiceDto formerService, string currentAccount, CancellationToken cancellationToken)
        {
            if (formerService == null)
                return;

            var formerAccount = ServyExePermissionsHardener.GetServiceAccounts(new[] { formerService }).FirstOrDefault();
            if (formerAccount == null || string.Equals(formerAccount, currentAccount?.Trim(), StringComparison.OrdinalIgnoreCase))
                return;

            if (_exePermissionsHardener != null)
            {
                try
                {
                    if (!await _exePermissionsHardener.RevokeIfUnusedAsync(formerAccount, serviceName, _serviceRepository, cancellationToken))
                    {
                        Logger.Warn($"The vault access of '{formerAccount}' was not fully revoked after service '{serviceName}' stopped using it. " +
                            "See the log above.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Revoking the vault access of '{formerAccount}' (service '{serviceName}') failed.", ex);
                }
            }

            await RefreshPipeAccessAsync(serviceName, formerAccount, granted: false, cancellationToken);
        }

        /// <summary>
        /// Grants the account a service was just installed under access to the Servy host named pipe, unless it is Local
        /// System, which always has it.
        /// </summary>
        /// <param name="serviceName">The service that was installed, for the log.</param>
        /// <param name="account">The account the service runs under, as passed to the Service Control Manager.</param>
        /// <param name="cancellationToken">A token that stops the request.</param>
        /// <returns>A task that completes when the host has rebuilt the DACL, or could not be asked to.</returns>
        /// <remarks>
        /// The pipe's DACL is derived from the accounts of the services in <c>Servy.db</c>, so the account is granted
        /// once its service's row exists; this asks the running host to rebuild the DACL now rather than at its next
        /// start. The service is already installed when this runs, so a failure is logged and never turned into a failed
        /// installation.
        /// </remarks>
        private Task GrantPipeAccessAsync(string serviceName, string account, CancellationToken cancellationToken)
        {
            if (!ServyExePermissionsHardener.IsHardeningCandidate(account))
                return Task.CompletedTask;

            return RefreshPipeAccessAsync(serviceName, account.Trim(), granted: true, cancellationToken);
        }

        /// <summary>
        /// Asks the Servy host to rebuild its named pipe's DACL from the accounts of the services in <c>Servy.db</c>:
        /// an account is granted while at least one service runs under it, and revoked once none does.
        /// </summary>
        /// <param name="serviceName">The service that was installed, reconfigured, uninstalled or removed, for the log.</param>
        /// <param name="account">The account whose access changed, for the log.</param>
        /// <param name="granted">Whether the account was added (<see langword="true"/>) or may have lost its last service.</param>
        /// <param name="cancellationToken">A token that stops the request.</param>
        /// <returns>A task that completes when the host has answered, or could not be reached.</returns>
        private async Task RefreshPipeAccessAsync(string serviceName, string account, bool granted, CancellationToken cancellationToken)
        {
            if (_namedPipesService == null)
                return;

            try
            {
                if (await _namedPipesService.RefreshPipeAccessAsync(cancellationToken))
                {
                    Logger.Info(granted
                        ? $"Granted '{account}' access to the Servy host named pipe (service '{serviceName}')."
                        : $"Refreshed the Servy host named pipe access after service '{serviceName}' stopped using '{account}'; the account keeps it only while another service runs under it.");
                }
                else
                {
                    Logger.Warn($"The Servy host did not refresh its named pipe access after a change to service '{serviceName}' ('{account}'). " +
                        $"It applies the change the next time the '{AppConfig.ServyHostServiceName}' service starts.");
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"Refreshing the Servy host named pipe access for '{account}' (service '{serviceName}') was cancelled.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Refreshing the Servy host named pipe access for '{account}' (service '{serviceName}') failed.", ex);
            }
        }

        /// <summary>
        /// Takes back the service-object control rights of the account a reconfigured service no longer runs under.
        /// </summary>
        /// <param name="serviceHandle">The open handle of the service being reconfigured.</param>
        /// <param name="formerService">The service's record as it was before the change; <see langword="null"/> when
        /// there was none, which revokes nothing.</param>
        /// <param name="currentAccount">The account the service runs under now, or <see cref="ServiceAccounts.LocalSystem"/>
        /// for Local System. The same account (compared case-insensitively) revokes nothing.</param>
        /// <remarks>
        /// The grant this takes back belongs to this one service, so unlike the vault grants it needs no "still used
        /// by another service" check. The reconfiguration has already succeeded when this runs, so a failure here is
        /// logged and never turns it into a failed operation.
        /// </remarks>
        private void RevokeServiceControlRightsIfAccountChanged(SafeServiceHandle serviceHandle, ServiceDto formerService, string currentAccount)
        {
            if (formerService == null)
                return;

            var formerAccount = ServyExePermissionsHardener.GetServiceAccounts(new[] { formerService }).FirstOrDefault();
            if (formerAccount == null || string.Equals(formerAccount, currentAccount?.Trim(), StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                _windowsServiceApi.RevokeServiceControlRights(serviceHandle, formerAccount);
            }
            catch (Exception ex)
            {
                Logger.Error($"Revoking the service control rights of '{formerAccount}' (service '{formerService.Name}') failed.", ex);
            }
        }

        /// <summary>
        /// Orchestrates an isolated database rollback sequence to restore state tracking for a legacy
        /// service variant if the subsequent installation pipeline fails or is canceled.
        /// </summary>
        /// <param name="legacyDroppedFromDb">A flag indicating whether the legacy service record was successfully removed during validation hardening.</param>
        /// <param name="legacyBackupDto">The original service data tracking context captured before structural execution began.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous database recovery operations.</returns>
        /// <remarks>
        /// This routine enforces data tracking atomicity across case-renaming scenarios. It executes under
        /// <see cref="CancellationToken.None"/> to ensure that recovery routines finish processing even
        /// if the primary installation process was canceled via user interaction.
        /// </remarks>
        private async Task ExecuteDatabaseRecoveryAsync(bool legacyDroppedFromDb, ServiceDto legacyBackupDto)
        {
            if (legacyDroppedFromDb && legacyBackupDto != null)
            {
                try
                {
                    Logger.Warn($"Installation pipeline failed after dropping legacy Unicode layout. Restoring database state tracking for '{legacyBackupDto.Name}'.");

                    // Re-hydrate the original DTO back into the system repository
                    await _serviceRepository.UpsertAsync(
                        legacyBackupDto,
                        preserveExistingRuntimeState: false,
                        preserveExistingCredentials: false,
                        CancellationToken.None); // Use None to ensure recovery runs even during user cancellations
                }
                catch (Exception dbRecoveryEx)
                {
                    Logger.Error($"CRITICAL: DB Recovery failed while restoring state tracking for '{legacyBackupDto.Name}'. Database is now out of sync.", dbRecoveryEx);
                }
            }
        }

        /// <inheritdoc />
        public async Task<OperationResult> UninstallServiceAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName)) throw new ArgumentException("serviceName is required.", nameof(serviceName));

            SafeScmHandle scmHandle = null;
            try
            {
                // 1. Initial responsiveness check
                cancellationToken.ThrowIfCancellationRequested();

                scmHandle = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_CONNECT);
                if (scmHandle == null || scmHandle.IsInvalid)
                {
                    return OperationResult.Failure("Failed to open Service Control Manager.");
                }

                // SERVICE_CHANGE_CONFIG to permit the start-type modification.
                uint uninstallRights = SERVICE_STOP | SERVICE_QUERY_STATUS | SERVICE_DELETE | SERVICE_CHANGE_CONFIG;

                using (var serviceHandle = _windowsServiceApi.OpenService(scmHandle, serviceName, uninstallRights))
                {
                    if (serviceHandle == null || serviceHandle.IsInvalid)
                    {
                        int openErr = _win32ErrorProvider.GetLastWin32Error();

                        // Handle the case where the Windows service is already removed from SCM (e.g., via sc delete or failed install),
                        // but leftover database records remain.
                        if (openErr == ERROR_SERVICE_DOES_NOT_EXIST)
                        {
                            var existingDbService = await _serviceRepository.GetByNameAsync(serviceName, decrypt: false, cancellationToken: cancellationToken);
                            if (existingDbService != null)
                            {
                                await _serviceRepository.DeleteAsync(serviceName, cancellationToken);
                                Logger.Info($"Service '{serviceName}' was not found in SCM, but orphan database record was successfully cleaned up.");
                                await RevokeExePermissionsIfUnusedAsync(serviceName, existingDbService, null, cancellationToken);
                                return OperationResult.Success();
                            }
                            return OperationResult.Failure(string.Format(Strings.Msg_ServiceNotFoundDetailed, serviceName));
                        }

                        return OperationResult.Failure($"Failed to open service '{serviceName}' for uninstallation. Win32 Error: {openErr}.");
                    }

                    // Trigger the stop command
                    var status = new SERVICE_STATUS();
                    if (!_windowsServiceApi.ControlService(serviceHandle, SERVICE_CONTROL_STOP, ref status))
                    {
                        int controlErr = _win32ErrorProvider.GetLastWin32Error();
                        if (controlErr == ERROR_SERVICE_NOT_ACTIVE)
                            Logger.Info($"Service '{serviceName}' is already stopped; skipping stop command.");
                        else
                            Logger.Warn($"ControlService(STOP) for '{serviceName}' returned false. Win32 error: {controlErr}. Proceeding to wait loop.");
                    }

                    // 2. The Wait Loop: Now fully cancellable
                    ServiceDto service;
                    using (var sc = _controllerFactory(serviceName))
                    {
                        service = await _serviceRepository.GetByNameAsync(serviceName, decrypt: false, cancellationToken: cancellationToken);
                        int waitTimeout = ServiceHelper.CalculateStopTimeout(
                            service?.StopTimeout,
                            service?.PreviousStopTimeout,
                            ServiceHelper.ResolvePreStopTimeout(service));

                        if (!await WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, waitTimeout, cancellationToken))
                        {
                            var msg = $"Service '{serviceName}' did not reach 'Stopped' within the {waitTimeout}s timeout. Aborting uninstall to avoid SCM 'marked for delete' state.";
                            Logger.Warn(msg);
                            return OperationResult.Failure(msg);
                        }
                    }

                    // ROBUSTNESS: Standardize start type *only after* confirming the service is completely stopped.
                    // This guarantees that if the wait loop times out or throws a cancellation exception above,
                    // the original SCM startup configuration remains unaltered, eliminating the manual-start downgrade trap.
                    bool configSuccess = _windowsServiceApi.ChangeServiceConfig(
                        hService: serviceHandle,
                        dwServiceType: SERVICE_NO_CHANGE,
                        dwStartType: SERVICE_DEMAND_START,
                        dwErrorControl: SERVICE_NO_CHANGE,
                        lpBinaryPathName: null,
                        lpLoadOrderGroup: null,
                        lpdwTagId: IntPtr.Zero,
                        lpDependencies: null,
                        lpServiceStartName: null,
                        lpPassword: null,
                        lpDisplayName: null);

                    if (!configSuccess)
                    {
                        // We log this as a warning rather than a failure, as we can still attempt
                        // the delete command, but it's important for diagnostic visibility.
                        Logger.Warn($"Failed to standardize start type before uninstall for '{serviceName}': Win32 Error {_win32ErrorProvider.GetLastWin32Error()}");
                    }

                    // 3. Final safety check before committing the permanent 'Delete'
                    cancellationToken.ThrowIfCancellationRequested();

                    var res = _windowsServiceApi.DeleteService(serviceHandle);

                    if (res)
                    {
                        // Ensure the repository deletion also honors the token
                        await _serviceRepository.DeleteAsync(serviceName, cancellationToken);

                        Logger.Info($"Service '{serviceName}' uninstalled successfully.");
                        await RevokeExePermissionsIfUnusedAsync(serviceName, service, null, cancellationToken);
                        return OperationResult.Success();
                    }
                    else
                    {
                        string errorMsg = $"Failed to uninstall service '{serviceName}'. Win32 Error {_win32ErrorProvider.GetLastWin32Error()}";
                        Logger.Error(errorMsg);
                        return OperationResult.Failure(errorMsg);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info($"Uninstallation of '{serviceName}' was cancelled by the user.");
                throw; // Re-throw to let the ViewModel handle the cancellation UI state
            }
            catch (Exception ex)
            {
                Logger.Error($"Error uninstalling service '{serviceName}'.", ex);
                return OperationResult.Failure($"Error uninstalling service '{serviceName}': {ex.Message}");
            }
            finally
            {
                scmHandle?.Dispose();
            }
        }

        /// <inheritdoc />
        public async Task<OperationResult> StartServiceAsync(string serviceName, bool logSuccessfulStart = true, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("service name cannot be null or whitespace.", nameof(serviceName));

            int timeout = 0;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var service = await _serviceRepository.GetByNameAsync(serviceName, decrypt: false, cancellationToken);
                if (service == null) return OperationResult.Failure($"Service '{serviceName}' was not found in the repository.");

                using (var sc = _controllerFactory(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                        return OperationResult.Success();

                    timeout = ServiceHelper.CalculateStartTimeout(
                        service.StartTimeout,
                        ServiceHelper.ResolvePreLaunchTimeout(service),
                        service.PreLaunchRetryAttempts ?? 0);

                    Logger.Info($"Attempting to start service '{serviceName}' with a timeout of {timeout} seconds.");
                    sc.Start();

                    if (!await WaitForStatusAsync(sc, ServiceControllerStatus.Running, timeout, cancellationToken))
                    {
                        string msg = $"Service '{serviceName}' did not reach 'Running' status within the {timeout}s timeout. It may still be initializing.";
                        Logger.Warn(msg);
                        return OperationResult.Failure(msg);
                    }

                    if (logSuccessfulStart)
                    {
                        Logger.Info($"Service '{serviceName}' started successfully.");
                    }

                    return OperationResult.Success();
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info($"Start of '{serviceName}' was cancelled by the user.");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to start service '{serviceName}'.", ex);
                return OperationResult.Failure($"Failed to start service '{serviceName}'. Reason: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public async Task<OperationResult> StopServiceAsync(string serviceName, bool logSuccessfulStop = true, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("service name cannot be null or whitespace.", nameof(serviceName));

            int timeout = 0;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var service = await _serviceRepository.GetByNameAsync(serviceName, decrypt: false, cancellationToken);

                if (service == null) return OperationResult.Failure($"Service '{serviceName}' was not found in the repository.");

                using (var sc = _controllerFactory(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped)
                        return OperationResult.Success();

                    timeout = ServiceHelper.CalculateStopTimeout(
                        service.StopTimeout,
                        service.PreviousStopTimeout,
                        ServiceHelper.ResolvePreStopTimeout(service));

                    Logger.Info($"Attempting to stop service '{serviceName}' with a timeout of {timeout} seconds.");
                    sc.Stop();

                    if (!await WaitForStatusAsync(sc, ServiceControllerStatus.Stopped, timeout, cancellationToken))
                    {
                        string msg = $"Service '{serviceName}' did not stop within {timeout} seconds. A forceful termination may be required.";
                        Logger.Warn(msg);
                        return OperationResult.Failure(msg);
                    }

                    if (logSuccessfulStop)
                    {
                        Logger.Info($"Service '{serviceName}' stopped successfully.");
                    }

                    return OperationResult.Success();
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info($"Stop of '{serviceName}' was cancelled by the user.");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to stop service '{serviceName}'.", ex);
                return OperationResult.Failure($"Failed to stop service '{serviceName}'. Reason: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public async Task<OperationResult> RestartServiceAsync(string serviceName, bool logSuccessfulRestart = true, CancellationToken cancellationToken = default)
        {
            var stopResult = await StopServiceAsync(serviceName, logSuccessfulStop: logSuccessfulRestart, cancellationToken);
            if (!stopResult.IsSuccess)
            {
                return OperationResult.Failure($"Failed to restart service '{serviceName}': {stopResult.ErrorMessage}");
            }

            var startResult = await StartServiceAsync(serviceName, logSuccessfulStart: logSuccessfulRestart, cancellationToken);

            if (startResult.IsSuccess)
            {
                if (logSuccessfulRestart)
                    Logger.Info($"Service '{serviceName}' restarted successfully.");
            }
            else
            {
                Logger.Error($"Failed to restart service '{serviceName}': {startResult.ErrorMessage}");
                return OperationResult.Failure($"Failed to restart service '{serviceName}': {startResult.ErrorMessage}");
            }

            return startResult;
        }

        /// <inheritdoc />
        public ServiceControllerStatus? GetServiceStatus(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or whitespace.", nameof(serviceName));

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using (var sc = _controllerFactory(serviceName))
                {
                    return sc.Status;
                }
            }
            catch (InvalidOperationException ex)
            {
                // Catching InvalidOperationException handles cases where the service does not exist
                // or was uninstalled mid-flight, safely satisfying the nullable fallback contract.
                Logger.Debug($"Service '{serviceName}' was not found or was removed during status retrieval: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"Unexpected error retrieving status for service '{serviceName}'.", ex);
                return null;
            }
        }

        /// <inheritdoc />
        public bool IsServiceInstalled(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or whitespace.", nameof(serviceName));

            cancellationToken.ThrowIfCancellationRequested();

            return _windowsServiceApi.GetServices()
                            .Any(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc />
        public ServiceStartType GetServiceStartupType(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or whitespace.", nameof(serviceName));

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Use ServiceController to grab the base StartType natively
                using (var sc = _controllerFactory(serviceName))
                {
                    var startupType = MapStartupType(sc);          // one switch, one Win32Exception policy

                    // If automatic, drill down with P/Invoke to check for Delayed Auto-Start
                    if (startupType == ServiceStartType.Automatic)
                    {
                        SafeScmHandle scmHandle = null;
                        try
                        {
                            scmHandle = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_CONNECT);
                            if (scmHandle != null && !scmHandle.IsInvalid)
                            {
                                using (var svcHandle = _windowsServiceApi.OpenService(scmHandle, serviceName, SERVICE_QUERY_CONFIG))
                                {
                                    if (svcHandle == null || svcHandle.IsInvalid)
                                    {
                                        Logger.Debug($"Could not open '{serviceName}' to check delayed auto-start (Win32 {_win32ErrorProvider.GetLastWin32Error()}); reporting Automatic.");
                                    }
                                    else if (IsDelayedStart(svcHandle))
                                    {
                                        startupType = ServiceStartType.AutomaticDelayedStart;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Best-effort refinement only: keep the start type MapStartupType already resolved.
                            Logger.Debug($"Delayed auto-start probe failed for '{serviceName}'; reporting {startupType}.", ex);
                        }
                        finally
                        {
                            if (scmHandle != null)
                            {
                                scmHandle.Dispose();
                            }
                        }
                    }

                    return startupType;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting service startup type for '{serviceName}'.", ex);
                return ServiceStartType.Unknown;
            }
        }

        /// <inheritdoc />
        public List<ServiceInfo> GetAllServices(CancellationToken cancellationToken = default)
        {
            var results = new ConcurrentBag<ServiceInfo>();

            // Materialize the list so we can guarantee deterministic disposal of all items
            var services = _serviceControllerProvider.GetServices().ToList();

            try
            {
                SafeScmHandle scmHandle = null;
                try
                {
                    scmHandle = _windowsServiceApi.OpenSCManager(null, null, SC_MANAGER_ENUMERATE_SERVICE);
                    if (scmHandle == null || scmHandle.IsInvalid)
                    {
                        throw new Win32Exception(_win32ErrorProvider.GetLastWin32Error(), "Failed to open Service Control Manager.");
                    }

                    Parallel.ForEach(services, new ParallelOptions
                    {
                        CancellationToken = cancellationToken,
                        MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, AppConfig.MaxParallelScmQueries),
                    },
                    service =>
                    {
                        // Wrapper disposal is owned by the outer finally so cancelled iterations are still cleaned up.

                        // Check before any work so cancellation surfaces as OperationCanceledException, not a partial result set.
                        cancellationToken.ThrowIfCancellationRequested();

                        ServiceInfo info = new ServiceInfo
                        {
                            Name = service.ServiceName,
                            Status = MapStatus(service.Status),
                            StartupType = MapStartupType(service),
                            LogOnAs = string.Empty,
                            Description = string.Empty,
                        };

                        // Per-service timeout enforcement
                        // We use a local CancellationTokenSource to enforce the per-call timeout
                        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                        {
                            try
                            {
                                // Set the timeout inside the guarded block
                                cts.CancelAfter(AppConfig.PopulateNativeDetailsTimeoutMs);

                                PopulateNativeDetails(scmHandle, info, cts.Token);
                            }
                            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                            {
                                info.Description = Strings.Msg_DetailsUnavailableTimedOut;
                                Logger.Warn($"Native SCM query timed out for service: {info.Name}");
                            }
                            catch (Exception ex) when (!(ex is OperationCanceledException))
                            {
                                Logger.Debug($"Native details collection faulted for {info.Name}: {ex.Message}");
                                info.Description = string.Format(Strings.Msg_DetailsUnavailableFaulted, ex.GetType().Name);
                            }
                        }

                        results.Add(info);
                    });

                    return results.OrderBy(s => s.Name).ToList();
                }
                finally
                {
                    // Native SCM queries are synchronous; nothing outlives the loop, so the handle can be disposed here.
                    scmHandle?.Dispose();
                }
            }
            finally
            {
                // Guarantee disposal of all service controller wrappers,
                // including those left unprocessed due to Parallel loop cancellation.
                foreach (var service in services)
                {
                    service?.Dispose();
                }
            }
        }

        /// <inheritdoc />
        public ServiceDependencyNode GetDependencies(string serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Service name cannot be null or whitespace.", nameof(serviceName));

            cancellationToken.ThrowIfCancellationRequested();

            if (!IsServiceInstalled(serviceName, cancellationToken))
            {
                return null; // legitimate: not installed
            }

            using (var sc = _controllerFactory(serviceName))
            {
                return sc.GetDependencies(cancellationToken); // let exceptions propagate; UI can show "Failed to query: ..."
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Populates additional service details using native Windows APIs.
        /// </summary>
        /// <param name="scmHandle">An active handle to the Service Control Manager.</param>
        /// <param name="info">The service information object to populate.</param>
        /// <param name="cancellationToken">A cancellation token (carries the per-service native-query timeout); checked before each discrete native call.</param>
        private void PopulateNativeDetails(SafeScmHandle scmHandle, ServiceInfo info, CancellationToken cancellationToken)
        {
            // 1. Pre-flight check
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(info.Name))
                throw new ArgumentException("Service name is empty!");

            // 2. Open the service handle
            using (var svcHandle = _windowsServiceApi.OpenService(scmHandle, info.Name, SERVICE_QUERY_CONFIG))
            {
                if (svcHandle == null || svcHandle.IsInvalid)
                {
                    int err = _win32ErrorProvider.GetLastWin32Error();
                    Logger.Debug($"Could not open '{info.Name}' for native details (Win32 {err}); leaving details unset.");
                    info.Description = Strings.Msg_DetailsUnavailableAccessDenied;
                    return;
                }

                // 3. Check token before each discrete native query.
                // If the per-service native-query timeout (AppConfig.PopulateNativeDetailsTimeoutMs)
                // or user cancellation hits during GetServiceUser, we skip the subsequent
                // calls to keep the loop moving.

                cancellationToken.ThrowIfCancellationRequested();
                info.LogOnAs = GetServiceUser(svcHandle) ?? ServiceAccounts.LocalSystem;  // confirmed null = LocalSystem (Win32 default)

                cancellationToken.ThrowIfCancellationRequested();
                info.Description = GetServiceDescription(svcHandle) ?? string.Empty;

                cancellationToken.ThrowIfCancellationRequested();
                if (info.StartupType == ServiceStartType.Automatic && IsDelayedStart(svcHandle))
                {
                    info.StartupType = ServiceStartType.AutomaticDelayedStart;
                }
            }
        }

        /// <summary>
        /// Executes a two-pass Win32 service configuration query using a size-probe pattern,
        /// allocating native memory dynamically before marshalling the target result string.
        /// </summary>
        /// <param name="svcHandle">A valid handle to the target Windows service.</param>
        /// <param name="queryConfig">The native P/Invoke method or wrapper delegate executing the Win32 query call.</param>
        /// <param name="extractString">Delegate that marshals the native structure from unmanaged memory and extracts the target string pointer.</param>
        /// <param name="probeContext">Description of the configuration target used for diagnostic logging.</param>
        /// <returns>The extracted string or <c>null</c> if no configuration value is set.</returns>
        /// <exception cref="Win32Exception">Thrown when the Win32 subsystem encounters an infrastructural or security impediment.</exception>
        private string QueryServiceConfigString(
            SafeServiceHandle svcHandle,
            QueryServiceConfigNativeDelegate queryConfig,
            Func<IntPtr, string> extractString,
            string probeContext)
        {
            // Invoke Pass 1: Size-Probe using an intentional null destination pointer
            queryConfig(svcHandle, IntPtr.Zero, 0, out int bytesNeeded);

            // Intercept the native error state immediately before any subsequent C# evaluations occur
            int errorCode = _win32ErrorProvider.GetLastWin32Error();

            // A successful size probe always fails with ERROR_INSUFFICIENT_BUFFER and reports a positive
            // size. Anything else is a real failure. A service with no value set still returns a struct
            // here; the null comes from extractString on the null string pointer inside it.
            if (bytesNeeded <= 0)
            {
                Logger.Warn($"QueryServiceConfig size probe failed for {probeContext}. Win32 Error Code: {errorCode}");
                throw new Win32Exception(errorCode);
            }

            IntPtr ptr = Marshal.AllocHGlobal(bytesNeeded);
            try
            {
                if (queryConfig(svcHandle, ptr, bytesNeeded, out _))
                {
                    return extractString(ptr);
                }

                int callErrorCode = _win32ErrorProvider.GetLastWin32Error();
                throw new Win32Exception(callErrorCode);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        /// <summary>
        /// Retrieves the account name under which the service runs.
        /// </summary>
        /// <param name="svcHandle">A valid handle to the target Windows service.</param>
        /// <returns>The account string, or <c>null</c> if no account is configured. A failed query throws instead.</returns>
        /// <exception cref="Win32Exception">Thrown when the Win32 subsystem encounters an infrastructural or security impediment.</exception>
        private string GetServiceUser(SafeServiceHandle svcHandle)
        {
            return QueryServiceConfigString(
                svcHandle,
                (SafeServiceHandle handle, IntPtr buffer, int bufferSize, out int bytesNeeded) =>
                    _windowsServiceApi.QueryServiceConfig(handle, buffer, bufferSize, out bytesNeeded),
                ptr =>
                {
                    var config = Marshal.PtrToStructure<QUERY_SERVICE_CONFIG>(ptr);
                    return Marshal.PtrToStringAuto(config.lpServiceStartName);
                },
                "account configuration");
        }

        /// <summary>
        /// Maps the standard .NET Framework <see cref="ServiceControllerStatus"/> to internal enum format.
        /// </summary>
        /// <param name="nativeStatus">The system status to map.</param>
        /// <returns>An internal <see cref="Enums.ServiceStatus"/> representation.</returns>
        private static ServiceStatus MapStatus(ServiceControllerStatus nativeStatus)
        {
            switch (nativeStatus)
            {
                case ServiceControllerStatus.Running: return Enums.ServiceStatus.Running;
                case ServiceControllerStatus.Stopped: return Enums.ServiceStatus.Stopped;
                case ServiceControllerStatus.Paused: return Enums.ServiceStatus.Paused;
                case ServiceControllerStatus.StartPending: return Enums.ServiceStatus.StartPending;
                case ServiceControllerStatus.StopPending: return Enums.ServiceStatus.StopPending;
                case ServiceControllerStatus.PausePending: return Enums.ServiceStatus.PausePending;
                case ServiceControllerStatus.ContinuePending: return Enums.ServiceStatus.ContinuePending;
                default: return Enums.ServiceStatus.None;
            }
        }

        /// <summary>
        /// Gets the startup type mapping while accounting for protected service API limitations.
        /// </summary>
        /// <param name="service">The service controller wrapper to query.</param>
        /// <returns>The identified <see cref="ServiceStartType"/>.</returns>
        private static ServiceStartType MapStartupType(IServiceControllerWrapper service)
        {
            try
            {
                switch (service.StartType)
                {
                    case ServiceStartMode.Automatic: return ServiceStartType.Automatic;
                    case ServiceStartMode.Manual: return ServiceStartType.Manual;
                    case ServiceStartMode.Disabled: return ServiceStartType.Disabled;
                    default: return ServiceStartType.Unknown;
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
            {
                // Expected for protected services the process cannot open. Debug level keeps a
                // machine-wide refresh from writing one error line per protected service.
                Logger.Debug($"Access denied or Win32 error reading StartType for '{service.ServiceName}'. Falling back to Unknown.", ex);
                return ServiceStartType.Unknown;
            }
            catch (Exception ex)
            {
                // Catch-all for unexpected failures (e.g. NullReferenceException). ObjectDisposedException
                // derives from InvalidOperationException, so the filtered catch above takes it, not this one.
                Logger.Error($"Unexpected error mapping startup type for '{service.ServiceName}'.", ex);
                return ServiceStartType.Unknown;
            }
        }

        /// <summary>
        /// Retrieves the optional description associated with the service.
        /// </summary>
        /// <param name="svcHandle">A valid handle to the target Windows service.</param>
        /// <returns>The service description string or <c>null</c> if none exists.</returns>
        /// <exception cref="Win32Exception">Thrown when the Win32 subsystem encounters an infrastructural or security impediment.</exception>
        private string GetServiceDescription(SafeServiceHandle svcHandle)
        {
            return QueryServiceConfigString(
                svcHandle,
                (SafeServiceHandle handle, IntPtr buffer, int bufferSize, out int bytesNeeded) =>
                    _windowsServiceApi.QueryServiceConfig2(handle, SERVICE_CONFIG_DESCRIPTION, buffer, bufferSize, out bytesNeeded),
                ptr =>
                {
                    var descStruct = Marshal.PtrToStructure<SERVICE_DESCRIPTION>(ptr);
                    return Marshal.PtrToStringAuto(descStruct.lpDescription);
                },
                "description");
        }

        /// <summary>
        /// Checks if the service is configured for a delayed automatic start.
        /// </summary>
        /// <param name="svcHandle">A valid handle to the target Windows service.</param>
        /// <returns><c>true</c> if it has delayed start configured; otherwise, <c>false</c>.</returns>
        private bool IsDelayedStart(SafeServiceHandle svcHandle)
        {
            var info = new SERVICE_DELAYED_AUTO_START_INFO();
            int structSize = Marshal.SizeOf<SERVICE_DELAYED_AUTO_START_INFO>();

            return _windowsServiceApi.QueryServiceConfig2(
                svcHandle,
                SERVICE_CONFIG_DELAYED_AUTO_START_INFO,
                ref info,
                structSize,
                out _) && info.fDelayedAutostart;
        }

        /// <summary>
        /// Maps a <see cref="ServiceStartType"/> to its corresponding Windows Service Control Manager (SCM)
        /// constant value for the <c>CreateService</c> and <c>ChangeServiceConfig</c> APIs.
        /// </summary>
        /// <remarks>
        /// The SCM API expects specific Win32 start type constants (1=System, 2=Automatic, 3=Manual, 4=Disabled).
        /// Two members in <see cref="ServiceStartType"/> do not have direct Win32 SCM start type anchors:
        /// <list type="bullet">
        ///   <item>
        ///     <see cref="ServiceStartType.Unknown"/> (0): Internal default/uninitialized sentinel. In the Win32 API,
        ///     0 represents <c>SERVICE_BOOT_START</c> (reserved for boot-driver loading). Passing 0 for a Win32 service
        ///     fails with <c>ERROR_INVALID_PARAMETER</c>. This method throws an exception if invoked with <c>Unknown</c>.
        ///   </item>
        ///   <item>
        ///     <see cref="ServiceStartType.AutomaticDelayedStart"/> (5): Managed as a separate configuration flag after creation.
        ///     This helper coerces <see cref="ServiceStartType.AutomaticDelayedStart"/> to <see cref="ServiceStartType.Automatic"/>
        ///     for the base SCM creation call.
        ///   </item>
        /// </list>
        /// </remarks>
        /// <param name="t">The <see cref="ServiceStartType"/> requested by the configuration.</param>
        /// <returns>The unsigned integer representation compatible with the Windows API.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="t"/> is <see cref="ServiceStartType.Unknown"/> or an undefined value.</exception>
        private static uint ToScmStartType(ServiceStartType t)
        {
            switch (t)
            {
                case ServiceStartType.AutomaticDelayedStart:
                case ServiceStartType.Automatic:
                    return SERVICE_AUTO_START;
                case ServiceStartType.Manual:
                    return SERVICE_DEMAND_START;
                case ServiceStartType.Disabled:
                    return SERVICE_DISABLED;

                case ServiceStartType.Unknown:
                    throw new ArgumentOutOfRangeException(
                        nameof(t),
                        t,
                        "Service start type could not be determined or is uninitialized (0 maps to Win32 SERVICE_BOOT_START, which is invalid for user-mode services).");

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(t),
                        t,
                        $"Undefined service start type value: {(int)t}.");
            }
        }

        #endregion
    }
}

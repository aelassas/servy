using Dapper;
using Newtonsoft.Json;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Core.Security;
using Servy.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Repository for managing <see cref="ServiceDto"/> entities in the database.
    /// Handles secure password encryption and decryption.
    /// </summary>
    public class ServiceRepository : IServiceRepository
    {
        private readonly IDapperExecutor _dapper;
        private readonly ISecureData _secureData;
        private readonly IXmlServiceSerializer _xmlServiceSerializer;
        private readonly IJsonServiceSerializer _jsonServiceSerializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceRepository"/> class.
        /// </summary>
        /// <param name="dapper">
        /// The Dapper executor used to perform database operations. Must not be null.
        /// </param>
        /// <param name="secureData">
        /// The service responsible for securely encrypting and decrypting passwords. Must not be null.
        /// </param>
        /// <param name="xmlServiceSerializer">
        /// The service responsible for serializing XML. Must not be null.
        /// </param>
        /// <param name="jsonServiceSerializer">
        /// The service responsible for serializing JSON. Must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dapper"/> or <paramref name="secureData"/> or <paramref name="xmlServiceSerializer"/> is null.
        /// </exception>
        public ServiceRepository(
            IDapperExecutor dapper,
            ISecureData secureData,
            IXmlServiceSerializer xmlServiceSerializer,
            IJsonServiceSerializer jsonServiceSerializer
            )
        {
            _dapper = dapper ?? throw new ArgumentNullException(nameof(dapper));
            _secureData = secureData ?? throw new ArgumentNullException(nameof(secureData));
            _xmlServiceSerializer = xmlServiceSerializer ?? throw new ArgumentNullException(nameof(xmlServiceSerializer));
            _jsonServiceSerializer = jsonServiceSerializer ?? throw new ArgumentNullException(nameof(jsonServiceSerializer));
        }

        #region DTO methods

        /// <inheritdoc />
        public virtual async Task<int> AddAsync(ServiceDto service, CancellationToken cancellationToken = default)
        {
            // 1. Create the encrypted clone for the DB operation
            var encryptedService = CreateEncryptedClone(service);

            var sql = @"
                INSERT INTO Services (
                    Name, Description, ExecutablePath, StartupDirectory, Parameters, 
                    StartupType, Priority, StdoutPath, StderrPath, EnableRotation, RotationSize, 
                    EnableHealthMonitoring, HeartbeatInterval, MaxFailedChecks, RecoveryAction, MaxRestartAttempts, 
                    EnvironmentVariables, ServiceDependencies, RunAsLocalSystem, UserAccount, Password, 
                    PreLaunchExecutablePath, PreLaunchStartupDirectory, PreLaunchParameters, PreLaunchEnvironmentVariables, 
                    PreLaunchStdoutPath, PreLaunchStderrPath, PreLaunchTimeoutSeconds, PreLaunchRetryAttempts, PreLaunchIgnoreFailure,
                    FailureProgramPath, FailureProgramStartupDirectory, FailureProgramParameters,
                    PostLaunchExecutablePath, PostLaunchStartupDirectory, PostLaunchParameters, Pid, EnableDebugLogs, DisplayName, MaxRotations,
                    EnableDateRotation, DateRotationType, StartTimeout, StopTimeout,
                    PreStopExecutablePath, PreStopStartupDirectory, PreStopParameters, PreStopTimeoutSeconds, PreStopLogAsError,
                    PostStopExecutablePath, PostStopStartupDirectory, PostStopParameters, UseLocalTimeForRotation
                ) VALUES (
                    @Name, @Description, @ExecutablePath, @StartupDirectory, @Parameters, 
                    @StartupType, @Priority, @StdoutPath, @StderrPath, @EnableRotation, @RotationSize, 
                    @EnableHealthMonitoring, @HeartbeatInterval, @MaxFailedChecks, @RecoveryAction, @MaxRestartAttempts, 
                    @EnvironmentVariables, @ServiceDependencies, @RunAsLocalSystem, @UserAccount, @Password, 
                    @PreLaunchExecutablePath, @PreLaunchStartupDirectory, @PreLaunchParameters, @PreLaunchEnvironmentVariables, 
                    @PreLaunchStdoutPath, @PreLaunchStderrPath, @PreLaunchTimeoutSeconds, @PreLaunchRetryAttempts, @PreLaunchIgnoreFailure,
                    @FailureProgramPath, @FailureProgramStartupDirectory, @FailureProgramParameters,
                    @PostLaunchExecutablePath, @PostLaunchStartupDirectory, @PostLaunchParameters, @Pid, @EnableDebugLogs, @DisplayName, @MaxRotations,
                    @EnableDateRotation, @DateRotationType, @StartTimeout, @StopTimeout,
                    @PreStopExecutablePath, @PreStopStartupDirectory, @PreStopParameters, @PreStopTimeoutSeconds, @PreStopLogAsError,
                    @PostStopExecutablePath, @PostStopStartupDirectory, @PostStopParameters, @UseLocalTimeForRotation
                );
                SELECT last_insert_rowid();";

            // 2. Capture the generated ID from the database
            var id = await _dapper.ExecuteScalarAsync<int>(sql, encryptedService);

            // 3. Sync the ID back to the original DTO so the caller (and the test) sees it
            service.Id = id;

            return id;
        }

        /// <inheritdoc />
        public virtual async Task<int> UpdateAsync(ServiceDto service, CancellationToken cancellationToken = default)
        {
            // Create a shallow copy to avoid mutating the caller's object
            var encryptedService = CreateEncryptedClone(service);

            var sql = @"
                UPDATE Services SET
                    Name = @Name,
                    Description = @Description,
                    ExecutablePath = @ExecutablePath,
                    StartupDirectory = @StartupDirectory,
                    Parameters = @Parameters,
                    StartupType = @StartupType,
                    Priority = @Priority,
                    StdoutPath = @StdoutPath,
                    StderrPath = @StderrPath,
                    EnableRotation = @EnableRotation,
                    RotationSize = @RotationSize,
                    EnableHealthMonitoring = @EnableHealthMonitoring,
                    HeartbeatInterval = @HeartbeatInterval,
                    MaxFailedChecks = @MaxFailedChecks,
                    RecoveryAction = @RecoveryAction,
                    MaxRestartAttempts = @MaxRestartAttempts,
                    EnvironmentVariables = @EnvironmentVariables,
                    ServiceDependencies = @ServiceDependencies,
                    RunAsLocalSystem = @RunAsLocalSystem,
                    UserAccount = @UserAccount,
                    Password = @Password,
                    PreLaunchExecutablePath = @PreLaunchExecutablePath,
                    PreLaunchStartupDirectory = @PreLaunchStartupDirectory,
                    PreLaunchParameters = @PreLaunchParameters,
                    PreLaunchEnvironmentVariables = @PreLaunchEnvironmentVariables,
                    PreLaunchStdoutPath = @PreLaunchStdoutPath,
                    PreLaunchStderrPath = @PreLaunchStderrPath,
                    PreLaunchTimeoutSeconds = @PreLaunchTimeoutSeconds,
                    PreLaunchRetryAttempts = @PreLaunchRetryAttempts,
                    PreLaunchIgnoreFailure = @PreLaunchIgnoreFailure,                     
                    FailureProgramPath = @FailureProgramPath,
                    FailureProgramStartupDirectory = @FailureProgramStartupDirectory,
                    FailureProgramParameters = @FailureProgramParameters,
                    PostLaunchExecutablePath = @PostLaunchExecutablePath,
                    PostLaunchStartupDirectory = @PostLaunchStartupDirectory,
                    PostLaunchParameters = @PostLaunchParameters,
                    Pid = @Pid,
                    EnableDebugLogs = @EnableDebugLogs,
                    DisplayName = @DisplayName,
                    MaxRotations = @MaxRotations,
                    EnableDateRotation = @EnableDateRotation,
                    DateRotationType = @DateRotationType,
                    StartTimeout = @StartTimeout,
                    StopTimeout = @StopTimeout,
                    PreviousStopTimeout = COALESCE(@PreviousStopTimeout, PreviousStopTimeout),
                    ActiveStdoutPath = @ActiveStdoutPath,
                    ActiveStderrPath = @ActiveStderrPath,
                    PreStopExecutablePath = @PreStopExecutablePath,
                    PreStopStartupDirectory = @PreStopStartupDirectory,
                    PreStopParameters = @PreStopParameters,
                    PreStopTimeoutSeconds = @PreStopTimeoutSeconds,
                    PreStopLogAsError = @PreStopLogAsError,
                    PostStopExecutablePath = @PostStopExecutablePath,
                    PostStopStartupDirectory = @PostStopStartupDirectory,
                    PostStopParameters = @PostStopParameters,
                    UseLocalTimeForRotation = @UseLocalTimeForRotation
                WHERE Id = @Id;";

            return await _dapper.ExecuteAsync(sql, encryptedService);
        }

        /// <inheritdoc />
        public virtual int Update(ServiceDto service)
        {
            // Create a shallow copy to avoid mutating the caller's object
            var encryptedService = CreateEncryptedClone(service);

            var sql = @"
                UPDATE Services SET
                    Name = @Name,
                    Description = @Description,
                    ExecutablePath = @ExecutablePath,
                    StartupDirectory = @StartupDirectory,
                    Parameters = @Parameters,
                    StartupType = @StartupType,
                    Priority = @Priority,
                    StdoutPath = @StdoutPath,
                    StderrPath = @StderrPath,
                    EnableRotation = @EnableRotation,
                    RotationSize = @RotationSize,
                    EnableHealthMonitoring = @EnableHealthMonitoring,
                    HeartbeatInterval = @HeartbeatInterval,
                    MaxFailedChecks = @MaxFailedChecks,
                    RecoveryAction = @RecoveryAction,
                    MaxRestartAttempts = @MaxRestartAttempts,
                    EnvironmentVariables = @EnvironmentVariables,
                    ServiceDependencies = @ServiceDependencies,
                    RunAsLocalSystem = @RunAsLocalSystem,
                    UserAccount = @UserAccount,
                    Password = @Password,
                    PreLaunchExecutablePath = @PreLaunchExecutablePath,
                    PreLaunchStartupDirectory = @PreLaunchStartupDirectory,
                    PreLaunchParameters = @PreLaunchParameters,
                    PreLaunchEnvironmentVariables = @PreLaunchEnvironmentVariables,
                    PreLaunchStdoutPath = @PreLaunchStdoutPath,
                    PreLaunchStderrPath = @PreLaunchStderrPath,
                    PreLaunchTimeoutSeconds = @PreLaunchTimeoutSeconds,
                    PreLaunchRetryAttempts = @PreLaunchRetryAttempts,
                    PreLaunchIgnoreFailure = @PreLaunchIgnoreFailure,                     
                    FailureProgramPath = @FailureProgramPath,
                    FailureProgramStartupDirectory = @FailureProgramStartupDirectory,
                    FailureProgramParameters = @FailureProgramParameters,
                    PostLaunchExecutablePath = @PostLaunchExecutablePath,
                    PostLaunchStartupDirectory = @PostLaunchStartupDirectory,
                    PostLaunchParameters = @PostLaunchParameters,
                    Pid = @Pid,
                    EnableDebugLogs = @EnableDebugLogs,
                    DisplayName = @DisplayName,
                    MaxRotations = @MaxRotations,
                    EnableDateRotation = @EnableDateRotation,
                    DateRotationType = @DateRotationType,
                    StartTimeout = @StartTimeout,
                    StopTimeout = @StopTimeout,
                    PreviousStopTimeout = COALESCE(@PreviousStopTimeout, PreviousStopTimeout),
                    ActiveStdoutPath = @ActiveStdoutPath,
                    ActiveStderrPath = @ActiveStderrPath,
                    PreStopExecutablePath = @PreStopExecutablePath,
                    PreStopStartupDirectory = @PreStopStartupDirectory,
                    PreStopParameters = @PreStopParameters,
                    PreStopTimeoutSeconds = @PreStopTimeoutSeconds,
                    PreStopLogAsError = @PreStopLogAsError,
                    PostStopExecutablePath = @PostStopExecutablePath,
                    PostStopStartupDirectory = @PostStopStartupDirectory,
                    PostStopParameters = @PostStopParameters,
                    UseLocalTimeForRotation = @UseLocalTimeForRotation
                WHERE Id = @Id;";

            return _dapper.Execute(sql, encryptedService);
        }

        /// <summary>
        /// Creates a shallow clone of the ServiceDto and encrypts sensitive fields.
        /// This prevents double-encryption and unintended mutation of the input object.
        /// </summary>
        private ServiceDto CreateEncryptedClone(ServiceDto source)
        {
            var clone = (ServiceDto)source.Clone();

            clone.Parameters = EncryptIfPresent(clone.Parameters);
            clone.FailureProgramParameters = EncryptIfPresent(clone.FailureProgramParameters);
            clone.PreLaunchParameters = EncryptIfPresent(clone.PreLaunchParameters);
            clone.PostLaunchParameters = EncryptIfPresent(clone.PostLaunchParameters);
            clone.Password = EncryptIfPresent(clone.Password);
            clone.EnvironmentVariables = EncryptIfPresent(clone.EnvironmentVariables);
            clone.PreLaunchEnvironmentVariables = EncryptIfPresent(clone.PreLaunchEnvironmentVariables);
            clone.PreStopParameters = EncryptIfPresent(clone.PreStopParameters);
            clone.PostStopParameters = EncryptIfPresent(clone.PostStopParameters);

            return clone;
        }

        /// <inheritdoc />
        public virtual async Task<int> UpsertAsync(ServiceDto service, CancellationToken cancellationToken = default)
        {
            // 1. Create the encrypted clone to prevent side-effects on the UI/Caller object
            var encryptedService = CreateEncryptedClone(service);

            // 2. The conflict target must match the functional index definition: LOWER(Name)
            var sql = @"
                INSERT INTO Services (
                    Name, Description, ExecutablePath, StartupDirectory, Parameters, 
                    StartupType, Priority, StdoutPath, StderrPath, EnableRotation, RotationSize, 
                    EnableHealthMonitoring, HeartbeatInterval, MaxFailedChecks, RecoveryAction, MaxRestartAttempts, 
                    EnvironmentVariables, ServiceDependencies, RunAsLocalSystem, UserAccount, Password, 
                    PreLaunchExecutablePath, PreLaunchStartupDirectory, PreLaunchParameters, PreLaunchEnvironmentVariables, 
                    PreLaunchStdoutPath, PreLaunchStderrPath, PreLaunchTimeoutSeconds, PreLaunchRetryAttempts, PreLaunchIgnoreFailure,
                    FailureProgramPath, FailureProgramStartupDirectory, FailureProgramParameters,
                    PostLaunchExecutablePath, PostLaunchStartupDirectory, PostLaunchParameters, Pid, EnableDebugLogs, DisplayName, MaxRotations,
                    EnableDateRotation, DateRotationType, StartTimeout, StopTimeout,
                    PreStopExecutablePath, PreStopStartupDirectory, PreStopParameters, PreStopTimeoutSeconds, PreStopLogAsError,
                    PostStopExecutablePath, PostStopStartupDirectory, PostStopParameters, UseLocalTimeForRotation
                ) VALUES (
                    @Name, @Description, @ExecutablePath, @StartupDirectory, @Parameters, 
                    @StartupType, @Priority, @StdoutPath, @StderrPath, @EnableRotation, @RotationSize, 
                    @EnableHealthMonitoring, @HeartbeatInterval, @MaxFailedChecks, @RecoveryAction, @MaxRestartAttempts, 
                    @EnvironmentVariables, @ServiceDependencies, @RunAsLocalSystem, @UserAccount, @Password, 
                    @PreLaunchExecutablePath, @PreLaunchStartupDirectory, @PreLaunchParameters, @PreLaunchEnvironmentVariables, 
                    @PreLaunchStdoutPath, @PreLaunchStderrPath, @PreLaunchTimeoutSeconds, @PreLaunchRetryAttempts, @PreLaunchIgnoreFailure,
                    @FailureProgramPath, @FailureProgramStartupDirectory, @FailureProgramParameters,
                    @PostLaunchExecutablePath, @PostLaunchStartupDirectory, @PostLaunchParameters, @Pid, @EnableDebugLogs, @DisplayName, @MaxRotations,
                    @EnableDateRotation, @DateRotationType, @StartTimeout, @StopTimeout,
                    @PreStopExecutablePath, @PreStopStartupDirectory, @PreStopParameters, @PreStopTimeoutSeconds, @PreStopLogAsError,
                    @PostStopExecutablePath, @PostStopStartupDirectory, @PostStopParameters, @UseLocalTimeForRotation
                )
                ON CONFLICT(LOWER(Name)) DO UPDATE SET
                    Description = excluded.Description,
                    ExecutablePath = excluded.ExecutablePath,
                    StartupDirectory = excluded.StartupDirectory,
                    Parameters = excluded.Parameters,
                    StartupType = excluded.StartupType,
                    Priority = excluded.Priority,
                    StdoutPath = excluded.StdoutPath,
                    StderrPath = excluded.StderrPath,
                    EnableRotation = excluded.EnableRotation,
                    RotationSize = excluded.RotationSize,
                    EnableHealthMonitoring = excluded.EnableHealthMonitoring,
                    HeartbeatInterval = excluded.HeartbeatInterval,
                    MaxFailedChecks = excluded.MaxFailedChecks,
                    RecoveryAction = excluded.RecoveryAction,
                    MaxRestartAttempts = excluded.MaxRestartAttempts,
                    EnvironmentVariables = excluded.EnvironmentVariables,
                    ServiceDependencies = excluded.ServiceDependencies,
                    RunAsLocalSystem = excluded.RunAsLocalSystem,
                    UserAccount = excluded.UserAccount,
                    Password = excluded.Password,
                    PreLaunchExecutablePath = excluded.PreLaunchExecutablePath,
                    PreLaunchStartupDirectory = excluded.PreLaunchStartupDirectory,
                    PreLaunchParameters = excluded.PreLaunchParameters,
                    PreLaunchEnvironmentVariables = excluded.PreLaunchEnvironmentVariables,
                    PreLaunchStdoutPath = excluded.PreLaunchStdoutPath,
                    PreLaunchStderrPath = excluded.PreLaunchStderrPath,
                    PreLaunchTimeoutSeconds = excluded.PreLaunchTimeoutSeconds,
                    PreLaunchRetryAttempts = excluded.PreLaunchRetryAttempts,
                    PreLaunchIgnoreFailure = excluded.PreLaunchIgnoreFailure,
                    FailureProgramPath = excluded.FailureProgramPath,
                    FailureProgramStartupDirectory = excluded.FailureProgramStartupDirectory,
                    FailureProgramParameters = excluded.FailureProgramParameters,
                    PostLaunchExecutablePath = excluded.PostLaunchExecutablePath,
                    PostLaunchStartupDirectory = excluded.PostLaunchStartupDirectory,
                    PostLaunchParameters = excluded.PostLaunchParameters,
                    EnableDebugLogs = excluded.EnableDebugLogs,
                    DisplayName = excluded.DisplayName,
                    MaxRotations = excluded.MaxRotations,
                    EnableDateRotation = excluded.EnableDateRotation,
                    DateRotationType = excluded.DateRotationType,
                    StartTimeout = excluded.StartTimeout,
                    StopTimeout = excluded.StopTimeout,
                    PreStopExecutablePath = excluded.PreStopExecutablePath,
                    PreStopStartupDirectory = excluded.PreStopStartupDirectory,
                    PreStopParameters = excluded.PreStopParameters,
                    PreStopTimeoutSeconds = excluded.PreStopTimeoutSeconds,
                    PreStopLogAsError = excluded.PreStopLogAsError,
                    PostStopExecutablePath = excluded.PostStopExecutablePath,
                    PostStopStartupDirectory = excluded.PostStopStartupDirectory,
                    PostStopParameters = excluded.PostStopParameters,
                    UseLocalTimeForRotation = excluded.UseLocalTimeForRotation;

                SELECT id FROM Services WHERE LOWER(Name) = LOWER(@Name);";

            var id = await _dapper.ExecuteScalarAsync<int>(sql, encryptedService);

            // 3. Update the original DTO's ID to keep it in sync with the DB
            service.Id = id;

            return id;
        }

        /// <inheritdoc />
        public virtual async Task<int> UpsertBatchAsync(IEnumerable<ServiceDto> services, CancellationToken cancellationToken = default)
        {
            if (services == null || !services.Any()) return 0;

            // Securely clone and encrypt all items in memory before passing to Dapper
            var encryptedServices = services.Select(CreateEncryptedClone).ToList();

            // The SQL uses an exhaustive DO UPDATE SET to keep the local DB synchronized with all service metadata
            var sql = @"
                INSERT INTO Services (
                    Name, Description, ExecutablePath, StartupDirectory, Parameters, 
                    StartupType, Priority, StdoutPath, StderrPath, EnableRotation, RotationSize, 
                    EnableHealthMonitoring, HeartbeatInterval, MaxFailedChecks, RecoveryAction, MaxRestartAttempts, 
                    EnvironmentVariables, ServiceDependencies, RunAsLocalSystem, UserAccount, Password, 
                    PreLaunchExecutablePath, PreLaunchStartupDirectory, PreLaunchParameters, PreLaunchEnvironmentVariables, 
                    PreLaunchStdoutPath, PreLaunchStderrPath, PreLaunchTimeoutSeconds, PreLaunchRetryAttempts, PreLaunchIgnoreFailure,
                    FailureProgramPath, FailureProgramStartupDirectory, FailureProgramParameters,
                    PostLaunchExecutablePath, PostLaunchStartupDirectory, PostLaunchParameters, Pid, EnableDebugLogs, DisplayName, MaxRotations,
                    EnableDateRotation, DateRotationType, StartTimeout, StopTimeout,
                    PreStopExecutablePath, PreStopStartupDirectory, PreStopParameters, PreStopTimeoutSeconds, PreStopLogAsError,
                    PostStopExecutablePath, PostStopStartupDirectory, PostStopParameters, UseLocalTimeForRotation
                ) VALUES (
                    @Name, @Description, @ExecutablePath, @StartupDirectory, @Parameters, 
                    @StartupType, @Priority, @StdoutPath, @StderrPath, @EnableRotation, @RotationSize, 
                    @EnableHealthMonitoring, @HeartbeatInterval, @MaxFailedChecks, @RecoveryAction, @MaxRestartAttempts, 
                    @EnvironmentVariables, @ServiceDependencies, @RunAsLocalSystem, @UserAccount, @Password, 
                    @PreLaunchExecutablePath, @PreLaunchStartupDirectory, @PreLaunchParameters, @PreLaunchEnvironmentVariables, 
                    @PreLaunchStdoutPath, @PreLaunchStderrPath, @PreLaunchTimeoutSeconds, @PreLaunchRetryAttempts, @PreLaunchIgnoreFailure,
                    @FailureProgramPath, @FailureProgramStartupDirectory, @FailureProgramParameters,
                    @PostLaunchExecutablePath, @PostLaunchStartupDirectory, @PostLaunchParameters, @Pid, @EnableDebugLogs, @DisplayName, @MaxRotations,
                    @EnableDateRotation, @DateRotationType, @StartTimeout, @StopTimeout,
                    @PreStopExecutablePath, @PreStopStartupDirectory, @PreStopParameters, @PreStopTimeoutSeconds, @PreStopLogAsError,
                    @PostStopExecutablePath, @PostStopStartupDirectory, @PostStopParameters, @UseLocalTimeForRotation
                )
                ON CONFLICT(LOWER(Name)) DO UPDATE SET
                    Description = excluded.Description,
                    ExecutablePath = excluded.ExecutablePath,
                    StartupDirectory = excluded.StartupDirectory,
                    Parameters = excluded.Parameters,
                    StartupType = excluded.StartupType,
                    Priority = excluded.Priority,
                    StdoutPath = excluded.StdoutPath,
                    StderrPath = excluded.StderrPath,
                    EnableRotation = excluded.EnableRotation,
                    RotationSize = excluded.RotationSize,
                    EnableHealthMonitoring = excluded.EnableHealthMonitoring,
                    HeartbeatInterval = excluded.HeartbeatInterval,
                    MaxFailedChecks = excluded.MaxFailedChecks,
                    RecoveryAction = excluded.RecoveryAction,
                    MaxRestartAttempts = excluded.MaxRestartAttempts,
                    EnvironmentVariables = excluded.EnvironmentVariables,
                    ServiceDependencies = excluded.ServiceDependencies,
                    RunAsLocalSystem = excluded.RunAsLocalSystem,
                    UserAccount = excluded.UserAccount,
                    Password = excluded.Password,
                    PreLaunchExecutablePath = excluded.PreLaunchExecutablePath,
                    PreLaunchStartupDirectory = excluded.PreLaunchStartupDirectory,
                    PreLaunchParameters = excluded.PreLaunchParameters,
                    PreLaunchEnvironmentVariables = excluded.PreLaunchEnvironmentVariables,
                    PreLaunchStdoutPath = excluded.PreLaunchStdoutPath,
                    PreLaunchStderrPath = excluded.PreLaunchStderrPath,
                    PreLaunchTimeoutSeconds = excluded.PreLaunchTimeoutSeconds,
                    PreLaunchRetryAttempts = excluded.PreLaunchRetryAttempts,
                    PreLaunchIgnoreFailure = excluded.PreLaunchIgnoreFailure,
                    FailureProgramPath = excluded.FailureProgramPath,
                    FailureProgramStartupDirectory = excluded.FailureProgramStartupDirectory,
                    FailureProgramParameters = excluded.FailureProgramParameters,
                    PostLaunchExecutablePath = excluded.PostLaunchExecutablePath,
                    PostLaunchStartupDirectory = excluded.PostLaunchStartupDirectory,
                    PostLaunchParameters = excluded.PostLaunchParameters,
                    Pid = excluded.Pid,
                    EnableDebugLogs = excluded.EnableDebugLogs,
                    DisplayName = excluded.DisplayName,
                    MaxRotations = excluded.MaxRotations,
                    EnableDateRotation = excluded.EnableDateRotation,
                    DateRotationType = excluded.DateRotationType,
                    StartTimeout = excluded.StartTimeout,
                    StopTimeout = excluded.StopTimeout,
                    PreStopExecutablePath = excluded.PreStopExecutablePath,
                    PreStopStartupDirectory = excluded.PreStopStartupDirectory,
                    PreStopParameters = excluded.PreStopParameters,
                    PreStopTimeoutSeconds = excluded.PreStopTimeoutSeconds,
                    PreStopLogAsError = excluded.PreStopLogAsError,
                    PostStopExecutablePath = excluded.PostStopExecutablePath,
                    PostStopStartupDirectory = excluded.PostStopStartupDirectory,
                    PostStopParameters = excluded.PostStopParameters,
                    UseLocalTimeForRotation = excluded.UseLocalTimeForRotation;";

            // Standard Dapper collection execution. Returning affected row count.
            return await _dapper.ExecuteAsync(sql, encryptedServices);
        }

        /// <inheritdoc />
        public virtual async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var sql = "DELETE FROM Services WHERE Id = @Id;";

            return await _dapper.ExecuteAsync(sql, new { Id = id });
        }

        /// <inheritdoc />
        public virtual async Task<int> DeleteAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;

            var sql = "DELETE FROM Services WHERE LOWER(Name) = LOWER(@Name);";

            return await _dapper.ExecuteAsync(sql, new { Name = name.Trim() });
        }

        /// <inheritdoc />
        public virtual async Task<ServiceDto> GetByIdAsync(int id, bool decrypt = true, CancellationToken cancellationToken = default)
        {
            var sql = "SELECT * FROM Services WHERE Id = @Id;";
            var cmd = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
            var dto = await _dapper.QuerySingleOrDefaultAsync<ServiceDto>(cmd);

            if (decrypt)
            {
                DecryptDto(dto);
            }

            return dto;
        }

        /// <inheritdoc />
        public virtual async Task<ServiceDto> GetByNameAsync(string name, bool decrypt = true, CancellationToken cancellationToken = default)
        {
            var sql = "SELECT * FROM Services WHERE LOWER(Name) = LOWER(@Name);";
            var cmd = new CommandDefinition(sql, new { Name = name.Trim() }, cancellationToken: cancellationToken);
            var dto = await _dapper.QuerySingleOrDefaultAsync<ServiceDto>(cmd);

            if (decrypt)
            {
                DecryptDto(dto);
            }

            return dto;
        }

        /// <inheritdoc />
        public virtual ServiceDto GetByName(string name, bool decrypt = true)
        {
            const string sql = "SELECT * FROM Services WHERE LOWER(Name) = LOWER(@Name);";

            // Using synchronous QuerySingleOrDefault
            var dto = _dapper.QuerySingleOrDefault<ServiceDto>(sql, new { Name = name.Trim() });

            if (dto != null && decrypt)
            {
                DecryptDto(dto);
            }

            return dto;
        }

        /// <inheritdoc />
        public async Task<int?> GetServicePidAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            // Using Dapper for a fast, scalar query
            const string sql = "SELECT Pid FROM Services WHERE Name = @Name LIMIT 1;";

            return await _dapper.QueryFirstOrDefaultAsync<int?>(sql, new { Name = serviceName });
        }

        /// <inheritdoc />
        public async Task<ServiceConsoleStateDto> GetServiceConsoleStateAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT Pid, ActiveStdoutPath, ActiveStderrPath 
                FROM Services 
                WHERE Name = @Name 
                LIMIT 1;";

            // This bypasses full DTO mapping and hits the SQLite page cache instantly
            return await _dapper.QueryFirstOrDefaultAsync<ServiceConsoleStateDto>(
                sql,
                new { Name = serviceName }
            );
        }

        /// <inheritdoc />
        public virtual async Task<IEnumerable<ServiceDto>> GetAllAsync(bool decrypt = true, CancellationToken cancellationToken = default)
        {
            var sql = "SELECT * FROM Services ORDER BY LOWER(Name) COLLATE NOCASE ASC;";
            var cmd = new CommandDefinition(sql, cancellationToken: cancellationToken);
            var list = await _dapper.QueryAsync<ServiceDto>(cmd);

            if (decrypt)
            {
                foreach (var dto in list)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DecryptDto(dto);
                }
            }

            return list;
        }

        /// <inheritdoc />
        public virtual async Task<IEnumerable<ServiceDto>> SearchAsync(string keyword, bool decrypt = true, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return await GetAllAsync(decrypt, cancellationToken);
            }

            // Standardize: Remove LOWER() from SQL to utilize indexes more efficiently
            // SQLite's LIKE is case-insensitive by default for ASCII
            var sql = @"
                SELECT * FROM Services 
                WHERE Name LIKE @Pattern ESCAPE '\' 
                   OR Description LIKE @Pattern ESCAPE '\' 
                ORDER BY Name COLLATE NOCASE ASC;";

            // Use Span-friendly or allocation-reduced escaping if possible
            // Standardizing on OrdinalIgnoreCase logic for the keyword preparation
            var escapedKeyword = keyword.Trim()
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");

            var pattern = $"%{escapedKeyword}%";

            var cmd = new CommandDefinition(sql, new { Pattern = pattern }, cancellationToken: cancellationToken);
            var list = (await _dapper.QueryAsync<ServiceDto>(cmd)).ToList();

            if (decrypt)
            {
                foreach (var dto in list)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DecryptDto(dto);
                }
            }

            return list;
        }

        /// <summary>
        /// Decrypts sensitive fields of a <see cref="ServiceDto"/> in-place.
        /// </summary>
        /// <param name="dto">The DTO to decrypt. If null, the method returns immediately.</param>
        private void DecryptDto(ServiceDto dto)
        {
            if (dto == null) return;

            dto.Parameters = DecryptIfPresent(dto.Parameters);
            dto.FailureProgramParameters = DecryptIfPresent(dto.FailureProgramParameters);
            dto.PreLaunchParameters = DecryptIfPresent(dto.PreLaunchParameters);
            dto.PostLaunchParameters = DecryptIfPresent(dto.PostLaunchParameters);
            dto.Password = DecryptIfPresent(dto.Password);
            dto.EnvironmentVariables = DecryptIfPresent(dto.EnvironmentVariables);
            dto.PreLaunchEnvironmentVariables = DecryptIfPresent(dto.PreLaunchEnvironmentVariables);
            dto.PreStopParameters = DecryptIfPresent(dto.PreStopParameters);
            dto.PostStopParameters = DecryptIfPresent(dto.PostStopParameters);
        }

        /// <inheritdoc />
        public virtual async Task<string> ExportXmlAsync(string name, CancellationToken cancellationToken = default)
        {
            var service = await GetByNameAsync(name);
            if (service == null)
                return string.Empty;

            var serializer = new XmlSerializer(typeof(ServiceDto));
            using (var stringWriter = new StringWriter())
            {
                serializer.Serialize(stringWriter, service);
                return stringWriter.ToString();
            }
        }

        /// <inheritdoc />
        public virtual async Task<bool> ImportXmlAsync(string xml, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(xml))
                return false;

            try
            {
                var service = _xmlServiceSerializer.Deserialize(xml);
                if (service == null)
                    return false;

                await UpsertAsync(service);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <inheritdoc />
        public virtual async Task<string> ExportJsonAsync(string name, CancellationToken cancellationToken = default)
        {
            var service = await GetByNameAsync(name);
            if (service == null)
                return string.Empty;

            return JsonConvert.SerializeObject(service, Formatting.Indented);
        }

        /// <inheritdoc />
        public virtual async Task<bool> ImportJsonAsync(string json, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                var service = _jsonServiceSerializer.Deserialize(json);
                if (service == null) return false;

                await UpsertAsync(service);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Encrypts a string if it is not null or white space.
        /// </summary>
        private string EncryptIfPresent(string value)
            => string.IsNullOrWhiteSpace(value) ? value : _secureData.Encrypt(value);

        /// <summary>
        /// Decrypts a string if it is not null or white space.
        /// </summary>
        private string DecryptIfPresent(string value)
            => string.IsNullOrWhiteSpace(value) ? value : _secureData.Decrypt(value);

        #endregion
    }
}

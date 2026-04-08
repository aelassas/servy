using Dapper;
using Newtonsoft.Json;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Domain;
using Servy.Core.DTOs;
using Servy.Core.Enums;
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
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dapper"/> or <paramref name="secureData"/> or <paramref name="xmlServiceSerializer"/> is null.
        /// </exception>
        public ServiceRepository(IDapperExecutor dapper, ISecureData secureData, IXmlServiceSerializer xmlServiceSerializer)
        {
            _dapper = dapper ?? throw new ArgumentNullException(nameof(dapper));
            _secureData = secureData ?? throw new ArgumentNullException(nameof(secureData));
            _xmlServiceSerializer = xmlServiceSerializer ?? throw new ArgumentNullException(nameof(xmlServiceSerializer));
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

            var sql = @"
        SELECT *
        FROM Services
        WHERE LOWER(Name) LIKE @Pattern ESCAPE '\'
            OR LOWER(Description) LIKE @Pattern ESCAPE '\'
        ORDER BY LOWER(Name) COLLATE NOCASE ASC;";

            var escapedKeyword = keyword.Trim().ToLower()
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");

            var pattern = $"%{escapedKeyword}%";
            var cmd = new CommandDefinition(sql, new { Pattern = pattern }, cancellationToken: cancellationToken);
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
                var service = JsonConvert.DeserializeObject<ServiceDto>(json, JsonSecurity.UntrustedDataSettings);
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

        #region Domain Methods

        /// <inheritdoc />
        public async Task<int> AddDomainServiceAsync(Service service, CancellationToken cancellationToken = default) => await AddAsync(MapToDto(service), cancellationToken);

        /// <inheritdoc />
        public async Task<int> UpdateDomainServiceAsync(Service service, CancellationToken cancellationToken = default) => await UpdateAsync(MapToDto(service), cancellationToken);

        /// <inheritdoc />
        public async Task<int> UpsertDomainServiceAsync(Service service, CancellationToken cancellationToken = default) => await UpsertAsync(MapToDto(service), cancellationToken);

        /// <inheritdoc />
        public async Task<int> DeleteDomainServiceAsync(int id, CancellationToken cancellationToken = default) => await DeleteAsync(id, cancellationToken);

        /// <inheritdoc />
        public async Task<int> DeleteDomainServiceAsync(string name, CancellationToken cancellationToken = default) => await DeleteAsync(name, cancellationToken);

        /// <inheritdoc />
        public async Task<Service> GetDomainServiceByIdAsync(IServiceManager serviceManager, int id, bool decrypt = true, CancellationToken cancellationToken = default)
        {
            var dto = await GetByIdAsync(id, decrypt, cancellationToken);
            return dto != null ? MapToDomain(serviceManager, dto) : null;
        }

        /// <inheritdoc />
        public async Task<Service> GetDomainServiceByNameAsync(IServiceManager serviceManager, string name, bool decrypt = true, CancellationToken cancellationToken = default)
        {
            var dto = await GetByNameAsync(name, decrypt, cancellationToken);
            return dto != null ? MapToDomain(serviceManager, dto) : null;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Service>> GetAllDomainServicesAsync(IServiceManager serviceManager, bool decrypt = true, CancellationToken cancellationToken = default)
        {
            var dtos = await GetAllAsync(decrypt, cancellationToken);
            return dtos.Select(dto => MapToDomain(serviceManager, dto));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Service>> SearchDomainServicesAsync(IServiceManager serviceManager, string keyword, bool decrypt = true, CancellationToken cancellationToken = default)
        {
            var dtos = await SearchAsync(keyword, decrypt, cancellationToken);
            return dtos.Select(dto => MapToDomain(serviceManager, dto));
        }

        /// <inheritdoc />
        public async Task<string> ExportDomainServiceXMLAsync(string name, CancellationToken cancellationToken = default) => await ExportXmlAsync(name, cancellationToken);

        /// <inheritdoc />
        public async Task<bool> ImportDomainServiceXMLAsync(string xml, CancellationToken cancellationToken = default) => await ImportXmlAsync(xml, cancellationToken);

        /// <inheritdoc />
        public async Task<string> ExportDomainServiceJSONAsync(string name, CancellationToken cancellationToken = default) => await ExportJsonAsync(name, cancellationToken);

        /// <inheritdoc />
        public async Task<bool> ImportDomainServiceJSONAsync(string json, CancellationToken cancellationToken = default) => await ImportJsonAsync(json, cancellationToken);

        #endregion

        #region Mapping helpers

        /// <summary>
        /// Maps a <see cref="ServiceDto"/> object to a domain <see cref="Service"/> instance.
        /// </summary>
        /// <param name="serviceManager">The <see cref="IServiceManager"/> used to manage the service.</param>
        /// <param name="dto">The data transfer object representing the service.</param>
        /// <returns>A new <see cref="Service"/> instance populated from the DTO.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="dto"/> is <c>null</c>.</exception>
        private Service MapToDomain(IServiceManager serviceManager, ServiceDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            return new Service(serviceManager)
            {
                Name = dto.Name,
                Description = dto.Description,
                ExecutablePath = dto.ExecutablePath,
                StartupDirectory = dto.StartupDirectory,
                Parameters = dto.Parameters,
                StartupType = dto.StartupType.HasValue ? (ServiceStartType)dto.StartupType.Value : ServiceStartType.Manual,
                Priority = dto.Priority.HasValue ? (ProcessPriority)dto.Priority.Value : ProcessPriority.Normal,
                StdoutPath = dto.StdoutPath,
                StderrPath = dto.StderrPath,
                EnableRotation = dto.EnableRotation ?? false,
                RotationSize = dto.RotationSize ?? AppConfig.DefaultRotationSize,
                EnableDateRotation = dto.EnableDateRotation ?? false,
                DateRotationType = dto.DateRotationType.HasValue ? (DateRotationType)dto.DateRotationType.Value : DateRotationType.Daily,
                MaxRotations = dto.MaxRotations ?? AppConfig.DefaultMaxRotations,
                UseLocalTimeForRotation = dto.UseLocalTimeForRotation ?? AppConfig.DefaultUseLocalTimeForRotation,
                EnableHealthMonitoring = dto.EnableHealthMonitoring ?? false,
                HeartbeatInterval = dto.HeartbeatInterval ?? 30,
                MaxFailedChecks = dto.MaxFailedChecks ?? 3,
                RecoveryAction = dto.RecoveryAction.HasValue ? (RecoveryAction)dto.RecoveryAction.Value : RecoveryAction.None,
                MaxRestartAttempts = dto.MaxRestartAttempts ?? 3,
                FailureProgramPath = dto.FailureProgramPath,
                FailureProgramStartupDirectory = dto.FailureProgramStartupDirectory,
                FailureProgramParameters = dto.FailureProgramParameters,
                EnvironmentVariables = dto.EnvironmentVariables,
                ServiceDependencies = dto.ServiceDependencies,
                RunAsLocalSystem = dto.RunAsLocalSystem ?? true,
                UserAccount = dto.UserAccount,
                Password = dto.Password,
                PreLaunchExecutablePath = dto.PreLaunchExecutablePath,
                PreLaunchStartupDirectory = dto.PreLaunchStartupDirectory,
                PreLaunchParameters = dto.PreLaunchParameters,
                PreLaunchEnvironmentVariables = dto.PreLaunchEnvironmentVariables,
                PreLaunchStdoutPath = dto.PreLaunchStdoutPath,
                PreLaunchStderrPath = dto.PreLaunchStderrPath,
                PreLaunchTimeoutSeconds = dto.PreLaunchTimeoutSeconds ?? 30,
                PreLaunchRetryAttempts = dto.PreLaunchRetryAttempts ?? 0,
                PreLaunchIgnoreFailure = dto.PreLaunchIgnoreFailure ?? false,

                PostLaunchExecutablePath = dto.PostLaunchExecutablePath,
                PostLaunchStartupDirectory = dto.PostLaunchStartupDirectory,
                PostLaunchParameters = dto.PostLaunchParameters,

                EnableDebugLogs = dto.EnableDebugLogs ?? false,

                DisplayName = dto.DisplayName,

                StartTimeout = dto.StartTimeout ?? AppConfig.DefaultStartTimeout,
                StopTimeout = dto.StopTimeout ?? AppConfig.DefaultStopTimeout,

                Pid = dto.Pid,
                ActiveStdoutPath = dto.ActiveStdoutPath,
                ActiveStderrPath = dto.ActiveStderrPath,

                PreStopExecutablePath = dto.PreStopExecutablePath,
                PreStopStartupDirectory = dto.PreStopStartupDirectory,
                PreStopParameters = dto.PreStopParameters,
                PreStopTimeoutSeconds = dto.PreStopTimeoutSeconds ?? AppConfig.DefaultPreStopTimeoutSeconds,
                PreStopLogAsError = dto.PreStopLogAsError ?? false,

                PostStopExecutablePath = dto.PostStopExecutablePath,
                PostStopStartupDirectory = dto.PostStopStartupDirectory,
                PostStopParameters = dto.PostStopParameters,
            };
        }

        /// <summary>
        /// Maps a domain <see cref="Service"/> instance to a <see cref="ServiceDto"/> object for persistence.
        /// </summary>
        /// <param name="domain">The domain service to map.</param>
        /// <returns>A <see cref="ServiceDto"/> populated from the domain service.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="domain"/> is <c>null</c>.</exception>
        private ServiceDto MapToDto(Service domain)
        {
            if (domain == null) throw new ArgumentNullException(nameof(domain));

            return new ServiceDto
            {
                Name = domain.Name,
                Description = domain.Description,
                ExecutablePath = domain.ExecutablePath,
                StartupDirectory = domain.StartupDirectory,
                Parameters = domain.Parameters,
                StartupType = (int)domain.StartupType,
                Priority = (int)domain.Priority,
                StdoutPath = domain.StdoutPath,
                StderrPath = domain.StderrPath,
                EnableRotation = domain.EnableRotation,
                RotationSize = domain.RotationSize,
                EnableDateRotation = domain.EnableDateRotation,
                DateRotationType = (int)domain.DateRotationType,
                MaxRotations = domain.MaxRotations,
                EnableHealthMonitoring = domain.EnableHealthMonitoring,
                UseLocalTimeForRotation = domain.UseLocalTimeForRotation,
                HeartbeatInterval = domain.HeartbeatInterval,
                MaxFailedChecks = domain.MaxFailedChecks,
                RecoveryAction = (int)domain.RecoveryAction,
                MaxRestartAttempts = domain.MaxRestartAttempts,
                FailureProgramPath = domain.FailureProgramPath,
                FailureProgramStartupDirectory = domain.FailureProgramStartupDirectory,
                FailureProgramParameters = domain.FailureProgramParameters,
                EnvironmentVariables = domain.EnvironmentVariables,
                ServiceDependencies = domain.ServiceDependencies,
                RunAsLocalSystem = domain.RunAsLocalSystem,
                UserAccount = domain.UserAccount,
                Password = domain.Password,
                PreLaunchExecutablePath = domain.PreLaunchExecutablePath,
                PreLaunchStartupDirectory = domain.PreLaunchStartupDirectory,
                PreLaunchParameters = domain.PreLaunchParameters,
                PreLaunchEnvironmentVariables = domain.PreLaunchEnvironmentVariables,
                PreLaunchStdoutPath = domain.PreLaunchStdoutPath,
                PreLaunchStderrPath = domain.PreLaunchStderrPath,
                PreLaunchTimeoutSeconds = domain.PreLaunchTimeoutSeconds,
                PreLaunchRetryAttempts = domain.PreLaunchRetryAttempts,
                PreLaunchIgnoreFailure = domain.PreLaunchIgnoreFailure,

                PostLaunchExecutablePath = domain.PostLaunchExecutablePath,
                PostLaunchStartupDirectory = domain.PostLaunchStartupDirectory,
                PostLaunchParameters = domain.PostLaunchParameters,

                EnableDebugLogs = domain.EnableDebugLogs,

                DisplayName = domain.DisplayName,

                StartTimeout = domain.StartTimeout,
                StopTimeout = domain.StopTimeout,

                PreStopExecutablePath = domain.PreStopExecutablePath,
                PreStopStartupDirectory = domain.PreStopStartupDirectory,
                PreStopParameters = domain.PreStopParameters,
                PreStopTimeoutSeconds = domain.PreStopTimeoutSeconds,
                PreStopLogAsError = domain.PreStopLogAsError,

                PostStopExecutablePath = domain.PostStopExecutablePath,
                PostStopStartupDirectory = domain.PostStopStartupDirectory,
                PostStopParameters = domain.PostStopParameters,
            };
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

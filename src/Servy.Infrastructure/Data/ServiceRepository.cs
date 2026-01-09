using Dapper;
using Newtonsoft.Json;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Domain;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
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
        private readonly ISecurePassword _securePassword;
        private readonly IXmlServiceSerializer _xmlServiceSerializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceRepository"/> class.
        /// </summary>
        /// <param name="dapper">
        /// The Dapper executor used to perform database operations. Must not be null.
        /// </param>
        /// <param name="securePassword">
        /// The service responsible for securely encrypting and decrypting passwords. Must not be null.
        /// </param>
        /// <param name="xmlServiceSerializer">
        /// The service responsible for serializing XML. Must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="dapper"/> or <paramref name="securePassword"/> or <paramref name="xmlServiceSerializer"/> is null.
        /// </exception>
        public ServiceRepository(IDapperExecutor dapper, ISecurePassword securePassword, IXmlServiceSerializer xmlServiceSerializer)
        {
            _dapper = dapper ?? throw new ArgumentNullException(nameof(dapper));
            _securePassword = securePassword ?? throw new ArgumentNullException(nameof(securePassword));
            _xmlServiceSerializer = xmlServiceSerializer ?? throw new ArgumentNullException(nameof(xmlServiceSerializer));
        }

        #region DTO methods

        /// <inheritdoc />
        public virtual async Task<int> AddAsync(ServiceDto service, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(service.Parameters))
                service.Parameters = _securePassword.Encrypt(service.Parameters);

            if (!string.IsNullOrWhiteSpace(service.FailureProgramParameters))
                service.FailureProgramParameters = _securePassword.Encrypt(service.FailureProgramParameters);

            if (!string.IsNullOrWhiteSpace(service.PreLaunchParameters))
                service.PreLaunchParameters = _securePassword.Encrypt(service.PreLaunchParameters);

            if (!string.IsNullOrWhiteSpace(service.PostLaunchParameters))
                service.PostLaunchParameters = _securePassword.Encrypt(service.PostLaunchParameters);

            if (!string.IsNullOrWhiteSpace(service.Password))
                service.Password = _securePassword.Encrypt(service.Password);

            if (!string.IsNullOrWhiteSpace(service.EnvironmentVariables))
                service.EnvironmentVariables = _securePassword.Encrypt(service.EnvironmentVariables);

            if (!string.IsNullOrWhiteSpace(service.PreLaunchEnvironmentVariables))
                service.PreLaunchEnvironmentVariables = _securePassword.Encrypt(service.PreLaunchEnvironmentVariables);

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
                    EnableDateRotation, DateRotationType, StartTimeout, StopTimeout
                ) VALUES (
                    @Name, @Description, @ExecutablePath, @StartupDirectory, @Parameters, 
                    @StartupType, @Priority, @StdoutPath, @StderrPath, @EnableRotation, @RotationSize, 
                    @EnableHealthMonitoring, @HeartbeatInterval, @MaxFailedChecks, @RecoveryAction, @MaxRestartAttempts, 
                    @EnvironmentVariables, @ServiceDependencies, @RunAsLocalSystem, @UserAccount, @Password, 
                    @PreLaunchExecutablePath, @PreLaunchStartupDirectory, @PreLaunchParameters, @PreLaunchEnvironmentVariables, 
                    @PreLaunchStdoutPath, @PreLaunchStderrPath, @PreLaunchTimeoutSeconds, @PreLaunchRetryAttempts, @PreLaunchIgnoreFailure,
                    @FailureProgramPath, @FailureProgramStartupDirectory, @FailureProgramParameters,
                    @PostLaunchExecutablePath, @PostLaunchStartupDirectory, @PostLaunchParameters, @Pid, @EnableDebugLogs, @DisplayName, @MaxRotations,
                    @EnableDateRotation, @DateRotationType, @StartTimeout, @StopTimeout
                );
                SELECT last_insert_rowid();";

            return await _dapper.ExecuteScalarAsync<int>(sql, service);
        }

        /// <inheritdoc />
        public virtual async Task<int> UpdateAsync(ServiceDto service, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(service.Parameters))
                service.Parameters = _securePassword.Encrypt(service.Parameters);

            if (!string.IsNullOrWhiteSpace(service.FailureProgramParameters))
                service.FailureProgramParameters = _securePassword.Encrypt(service.FailureProgramParameters);

            if (!string.IsNullOrWhiteSpace(service.PreLaunchParameters))
                service.PreLaunchParameters = _securePassword.Encrypt(service.PreLaunchParameters);

            if (!string.IsNullOrWhiteSpace(service.PostLaunchParameters))
                service.PostLaunchParameters = _securePassword.Encrypt(service.PostLaunchParameters);

            if (!string.IsNullOrWhiteSpace(service.Password))
                service.Password = _securePassword.Encrypt(service.Password);

            if (!string.IsNullOrWhiteSpace(service.EnvironmentVariables))
                service.EnvironmentVariables = _securePassword.Encrypt(service.EnvironmentVariables);

            if (!string.IsNullOrWhiteSpace(service.PreLaunchEnvironmentVariables))
                service.PreLaunchEnvironmentVariables = _securePassword.Encrypt(service.PreLaunchEnvironmentVariables);

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
                    StopTimeout = @StopTimeout

                WHERE Id = @Id;";

            return await _dapper.ExecuteAsync(sql, service);
        }


        /// <inheritdoc />
        public virtual async Task<int> UpsertAsync(ServiceDto service, CancellationToken cancellationToken = default)
        {
            var serviceDto = await GetByNameAsync(service.Name.Trim());
            if (serviceDto != null)
            {
                service.Id = serviceDto.Id;
                service.Pid = serviceDto.Pid;
                return await UpdateAsync(service);
            }

            return await AddAsync(service);
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
        public virtual async Task<ServiceDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var sql = "SELECT * FROM Services WHERE Id = @Id;";

            var cmd = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
            var dto = await _dapper.QuerySingleOrDefaultAsync<ServiceDto>(cmd);

            if (dto != null && !string.IsNullOrEmpty(dto.Parameters))
                dto.Parameters = _securePassword.Decrypt(dto.Parameters);

            if (dto != null && !string.IsNullOrEmpty(dto.FailureProgramParameters))
                dto.FailureProgramParameters = _securePassword.Decrypt(dto.FailureProgramParameters);

            if (dto != null && !string.IsNullOrEmpty(dto.PreLaunchParameters))
                dto.PreLaunchParameters = _securePassword.Decrypt(dto.PreLaunchParameters);

            if (dto != null && !string.IsNullOrEmpty(dto.PostLaunchParameters))
                dto.PostLaunchParameters = _securePassword.Decrypt(dto.PostLaunchParameters);

            if (dto != null && !string.IsNullOrEmpty(dto.Password))
                dto.Password = _securePassword.Decrypt(dto.Password);

            if (dto != null && !string.IsNullOrEmpty(dto.EnvironmentVariables))
                dto.EnvironmentVariables = _securePassword.Decrypt(dto.EnvironmentVariables);

            if (dto != null && !string.IsNullOrEmpty(dto.PreLaunchEnvironmentVariables))
                dto.PreLaunchEnvironmentVariables = _securePassword.Decrypt(dto.PreLaunchEnvironmentVariables);

            return dto;
        }

        /// <inheritdoc />
        public virtual async Task<ServiceDto> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            var sql = "SELECT * FROM Services WHERE LOWER(Name) = LOWER(@Name);";

            var cmd = new CommandDefinition(sql, new { Name = name.Trim() }, cancellationToken: cancellationToken);
            var dto = await _dapper.QuerySingleOrDefaultAsync<ServiceDto>(cmd);

            if (dto != null && !string.IsNullOrEmpty(dto.Parameters))
                dto.Parameters = _securePassword.Decrypt(dto.Parameters);

            if (dto != null && !string.IsNullOrEmpty(dto.FailureProgramParameters))
                dto.FailureProgramParameters = _securePassword.Decrypt(dto.FailureProgramParameters);

            if (dto != null && !string.IsNullOrEmpty(dto.PreLaunchParameters))
                dto.PreLaunchParameters = _securePassword.Decrypt(dto.PreLaunchParameters);

            if (dto != null && !string.IsNullOrEmpty(dto.PostLaunchParameters))
                dto.PostLaunchParameters = _securePassword.Decrypt(dto.PostLaunchParameters);

            if (dto != null && !string.IsNullOrEmpty(dto.Password))
                dto.Password = _securePassword.Decrypt(dto.Password);

            if (dto != null && !string.IsNullOrEmpty(dto.EnvironmentVariables))
                dto.EnvironmentVariables = _securePassword.Decrypt(dto.EnvironmentVariables);

            if (dto != null && !string.IsNullOrEmpty(dto.PreLaunchEnvironmentVariables))
                dto.PreLaunchEnvironmentVariables = _securePassword.Decrypt(dto.PreLaunchEnvironmentVariables);

            return dto;
        }

        /// <inheritdoc />
        public virtual async Task<IEnumerable<ServiceDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var sql = "SELECT * FROM Services;";

            var cmd = new CommandDefinition(sql, cancellationToken: cancellationToken);
            var list = await _dapper.QueryAsync<ServiceDto>(cmd);


            foreach (var dto in list)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(dto.Parameters))
                    dto.Parameters = _securePassword.Decrypt(dto.Parameters);

                if (!string.IsNullOrEmpty(dto.FailureProgramParameters))
                    dto.FailureProgramParameters = _securePassword.Decrypt(dto.FailureProgramParameters);

                if (!string.IsNullOrEmpty(dto.PreLaunchParameters))
                    dto.PreLaunchParameters = _securePassword.Decrypt(dto.PreLaunchParameters);

                if (!string.IsNullOrEmpty(dto.PostLaunchParameters))
                    dto.PostLaunchParameters = _securePassword.Decrypt(dto.PostLaunchParameters);

                if (!string.IsNullOrEmpty(dto.Password))
                    dto.Password = _securePassword.Decrypt(dto.Password);

                if (!string.IsNullOrEmpty(dto.EnvironmentVariables))
                    dto.EnvironmentVariables = _securePassword.Decrypt(dto.EnvironmentVariables);

                if (!string.IsNullOrEmpty(dto.PreLaunchEnvironmentVariables))
                    dto.PreLaunchEnvironmentVariables = _securePassword.Decrypt(dto.PreLaunchEnvironmentVariables);
            }

            return list;
        }

        /// <inheritdoc />
        public virtual async Task<IEnumerable<ServiceDto>> Search(string keyword, CancellationToken cancellationToken = default)
        {
            var sql = @"
                SELECT *
                FROM Services
                WHERE LOWER(Name) LIKE @Pattern ESCAPE '\'
                    OR LOWER(Description) LIKE @Pattern ESCAPE '\'
                ORDER BY LOWER(Name) COLLATE NOCASE ASC;";

            var escapedKeyword = keyword?.Trim().ToLower()
                .Replace(@"\", @"\\")  // escape backslashes first
                .Replace("%", @"\%")
                .Replace("_", @"\_");

            var pattern = $"%{escapedKeyword}%";

            var cmd = new CommandDefinition(sql, new { Pattern = pattern }, cancellationToken: cancellationToken);
            var list = await _dapper.QueryAsync<ServiceDto>(cmd);

            foreach (var dto in list)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(dto.Parameters))
                    dto.Parameters = _securePassword.Decrypt(dto.Parameters);

                if (!string.IsNullOrEmpty(dto.FailureProgramParameters))
                    dto.FailureProgramParameters = _securePassword.Decrypt(dto.FailureProgramParameters);

                if (!string.IsNullOrEmpty(dto.PreLaunchParameters))
                    dto.PreLaunchParameters = _securePassword.Decrypt(dto.PreLaunchParameters);

                if (!string.IsNullOrEmpty(dto.PostLaunchParameters))
                    dto.PostLaunchParameters = _securePassword.Decrypt(dto.PostLaunchParameters);

                if (!string.IsNullOrEmpty(dto.Password))
                    dto.Password = _securePassword.Decrypt(dto.Password);

                if (!string.IsNullOrEmpty(dto.EnvironmentVariables))
                    dto.EnvironmentVariables = _securePassword.Decrypt(dto.EnvironmentVariables);

                if (!string.IsNullOrEmpty(dto.PreLaunchEnvironmentVariables))
                    dto.PreLaunchEnvironmentVariables = _securePassword.Decrypt(dto.PreLaunchEnvironmentVariables);
            }

            return list;
        }

        /// <inheritdoc />
        public virtual async Task<string> ExportXML(string name, CancellationToken cancellationToken = default)
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
        public virtual async Task<bool> ImportXML(string xml, CancellationToken cancellationToken = default)
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
        public virtual async Task<string> ExportJSON(string name, CancellationToken cancellationToken = default)
        {
            var service = await GetByNameAsync(name);
            if (service == null)
                return string.Empty;

            return JsonConvert.SerializeObject(service, Formatting.Indented);
        }

        /// <inheritdoc />
        public virtual async Task<bool> ImportJSON(string json, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                var service = JsonConvert.DeserializeObject<ServiceDto>(json);
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
        public async Task<Service> GetDomainServiceByIdAsync(IServiceManager serviceManager, int id, CancellationToken cancellationToken = default)
        {
            var dto = await GetByIdAsync(id, cancellationToken);
            return dto != null ? MapToDomain(serviceManager, dto) : null;
        }

        /// <inheritdoc />
        public async Task<Service> GetDomainServiceByNameAsync(IServiceManager serviceManager, string name, CancellationToken cancellationToken = default)
        {
            var dto = await GetByNameAsync(name, cancellationToken);
            return dto != null ? MapToDomain(serviceManager, dto) : null;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Service>> GetAllDomainServicesAsync(IServiceManager serviceManager, CancellationToken cancellationToken = default)
        {
            var dtos = await GetAllAsync(cancellationToken);
            return dtos.Select(dto => MapToDomain(serviceManager, dto));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Service>> SearchDomainServicesAsync(IServiceManager serviceManager, string keyword, CancellationToken cancellationToken = default)
        {
            var dtos = await Search(keyword, cancellationToken);
            return dtos.Select(dto => MapToDomain(serviceManager, dto));
        }

        /// <inheritdoc />
        public async Task<string> ExportDomainServiceXMLAsync(string name, CancellationToken cancellationToken = default) => await ExportXML(name, cancellationToken);

        /// <inheritdoc />
        public async Task<bool> ImportDomainServiceXMLAsync(string xml, CancellationToken cancellationToken = default) => await ImportXML(xml, cancellationToken);

        /// <inheritdoc />
        public async Task<string> ExportDomainServiceJSONAsync(string name, CancellationToken cancellationToken = default) => await ExportJSON(name, cancellationToken);

        /// <inheritdoc />
        public async Task<bool> ImportDomainServiceJSONAsync(string json, CancellationToken cancellationToken = default) => await ImportJSON(json, cancellationToken);

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
            };
        }

        #endregion

    }
}

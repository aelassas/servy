using Newtonsoft.Json;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
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

        /// <inheritdoc />
        public async Task<int> AddAsync(ServiceDto service)
        {
            if (!string.IsNullOrWhiteSpace(service.Password))
                service.Password = _securePassword.Encrypt(service.Password);

            var sql = @"
                INSERT INTO Services (
                    Name, Description, ExecutablePath, StartupDirectory, Parameters, 
                    StartupType, Priority, StdoutPath, StderrPath, EnableRotation, RotationSize, 
                    EnableHealthMonitoring, HeartbeatInterval, MaxFailedChecks, RecoveryAction, MaxRestartAttempts, 
                    EnvironmentVariables, ServiceDependencies, RunAsLocalSystem, UserAccount, Password, 
                    PreLaunchExecutablePath, PreLaunchStartupDirectory, PreLaunchParameters, PreLaunchEnvironmentVariables, 
                    PreLaunchStdoutPath, PreLaunchStderrPath, PreLaunchTimeoutSeconds, PreLaunchRetryAttempts, PreLaunchIgnoreFailure
                ) VALUES (
                    @Name, @Description, @ExecutablePath, @StartupDirectory, @Parameters, 
                    @StartupType, @Priority, @StdoutPath, @StderrPath, @EnableRotation, @RotationSize, 
                    @EnableHealthMonitoring, @HeartbeatInterval, @MaxFailedChecks, @RecoveryAction, @MaxRestartAttempts, 
                    @EnvironmentVariables, @ServiceDependencies, @RunAsLocalSystem, @UserAccount, @Password, 
                    @PreLaunchExecutablePath, @PreLaunchStartupDirectory, @PreLaunchParameters, @PreLaunchEnvironmentVariables, 
                    @PreLaunchStdoutPath, @PreLaunchStderrPath, @PreLaunchTimeoutSeconds, @PreLaunchRetryAttempts, @PreLaunchIgnoreFailure
                );
                SELECT last_insert_rowid();";


            return await _dapper.ExecuteScalarAsync<int>(sql, service);
        }

        /// <inheritdoc />
        public async Task<int> UpdateAsync(ServiceDto service)
        {
            if (!string.IsNullOrWhiteSpace(service.Password))
                service.Password = _securePassword.Encrypt(service.Password);

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
                    PreLaunchIgnoreFailure = @PreLaunchIgnoreFailure
                WHERE Id = @Id;";

            
            return await _dapper.ExecuteAsync(sql, service);
        }

        /// <inheritdoc />
        public async Task<int> UpsertAsync(ServiceDto service)
        {
            var exists = await _dapper.QuerySingleOrDefaultAsync<int>(
                "SELECT Id FROM Services WHERE LOWER(Name) = LOWER(@Name);",
                new { Name = service.Name.Trim() });

            if (exists > 0)
            {
                service.Id = exists;
                return await UpdateAsync(service);
            }

            return await AddAsync(service);
        }

        /// <inheritdoc />
        public async Task<int> DeleteAsync(int id)
        {
            var sql = "DELETE FROM Services WHERE Id = @Id;";
            
            return await _dapper.ExecuteAsync(sql, new { Id = id });
        }

        /// <inheritdoc />
        public async Task<int> DeleteAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;

            var sql = "DELETE FROM Services WHERE LOWER(Name) = LOWER(@Name);";
            
            return await _dapper.ExecuteAsync(sql, new { Name = name.Trim() });
        }

        /// <inheritdoc />
        public async Task<ServiceDto?> GetByIdAsync(int id)
        {
            var sql = "SELECT * FROM Services WHERE Id = @Id;";
            

            var dto = await _dapper.QuerySingleOrDefaultAsync<ServiceDto>(sql, new { Id = id });
            if (dto != null && !string.IsNullOrEmpty(dto.Password))
                dto.Password = _securePassword.Decrypt(dto.Password);

            return dto;
        }

        /// <inheritdoc />
        public async Task<ServiceDto?> GetByNameAsync(string name)
        {
            var sql = "SELECT * FROM Services WHERE LOWER(Name) = LOWER(@Name);";
            

            var dto = await _dapper.QuerySingleOrDefaultAsync<ServiceDto>(sql, new { Name = name.Trim() });
            if (dto != null && !string.IsNullOrEmpty(dto.Password))
                dto.Password = _securePassword.Decrypt(dto.Password);

            return dto;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ServiceDto>> GetAllAsync()
        {
            var sql = "SELECT * FROM Services;";
            

            var list = await _dapper.QueryAsync<ServiceDto>(sql);
            foreach (var dto in list)
            {
                if (!string.IsNullOrEmpty(dto.Password))
                    dto.Password = _securePassword.Decrypt(dto.Password);
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ServiceDto>> Search(string keyword)
        {
            var sql = @"
                SELECT *
                FROM Services
                WHERE LOWER(Name) LIKE @Pattern
                   OR LOWER(Description) LIKE @Pattern;";

            var pattern = $"%{keyword?.Trim().ToLower()}%";

            

            var list = await _dapper.QueryAsync<ServiceDto>(sql, new { Pattern = pattern });
            foreach (var dto in list)
            {
                if (!string.IsNullOrEmpty(dto.Password))
                    dto.Password = _securePassword.Decrypt(dto.Password);
            }

            return list;
        }

        /// <inheritdoc />
        public async Task<string> ExportXML(string name)
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
        public async Task<bool> ImportXML(string xml)
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
        public async Task<string> ExportJSON(string name)
        {
            var service = await GetByNameAsync(name);
            if (service == null)
                return string.Empty;

            return JsonConvert.SerializeObject(service, Newtonsoft.Json.Formatting.Indented);
        }

        /// <inheritdoc />
        public async Task<bool> ImportJSON(string json)
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
    }
}

using Dapper;
using Newtonsoft.Json;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using System.Xml.Serialization;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Repository for managing <see cref="ServiceDto"/> entities in the database.
    /// Handles secure password encryption, decryption, and centralizes SQL schemas to prevent divergence.
    /// </summary>
    public class ServiceRepository : IServiceRepository
    {
        private static readonly XmlSerializer ServiceDtoSerializer = new XmlSerializer(typeof(ServiceDto));

        private readonly IDapperExecutor _dapper;
        private readonly ISecureData _secureData;
        private readonly IXmlServiceSerializer _xmlServiceSerializer;
        private readonly IJsonServiceSerializer _jsonServiceSerializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceRepository"/> class.
        /// </summary>
        /// <param name="dapper">The Dapper executor used to perform database operations. Must not be null.</param>
        /// <param name="secureData">The service responsible for securely encrypting and decrypting passwords. Must not be null.</param>
        /// <param name="xmlServiceSerializer">The service responsible for serializing XML. Must not be null.</param>
        /// <param name="jsonServiceSerializer">The service responsible for serializing JSON. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
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
            var encryptedService = CreateEncryptedClone(service);

            var sql = $@"
                INSERT INTO Services ({SqlConstants.InsertColumns}) 
                VALUES ({SqlConstants.InsertValues});
                SELECT last_insert_rowid();";

            var id = await _dapper.ExecuteScalarAsync<int>(sql, encryptedService, cancellationToken);
            service.Id = id;

            return id;
        }

        /// <inheritdoc />
        public virtual async Task<int> UpdateAsync(ServiceDto service, CancellationToken cancellationToken = default)
        {
            var encryptedService = CreateEncryptedClone(service);

            var sql = $@"
                UPDATE Services SET
                {SqlConstants.UpdateSet}
                WHERE Id = @Id;";

            return await _dapper.ExecuteAsync(sql, encryptedService, cancellationToken);
        }

        /// <inheritdoc />
        public virtual int Update(ServiceDto service)
        {
            var encryptedService = CreateEncryptedClone(service);

            var sql = $@"
                UPDATE Services SET
                {SqlConstants.UpdateSet}
                WHERE Id = @Id;";

            return _dapper.Execute(sql, encryptedService);
        }

        /// <inheritdoc />
        public virtual async Task<int> UpsertAsync(ServiceDto service, CancellationToken cancellationToken = default)
        {
            var encryptedService = CreateEncryptedClone(service);

            var sql = $@"
                INSERT INTO Services ({SqlConstants.InsertColumns}) 
                VALUES ({SqlConstants.InsertValues})
                ON CONFLICT(LOWER(Name)) DO UPDATE SET
                {SqlConstants.UpsertSet};
                SELECT id FROM Services WHERE LOWER(Name) = LOWER(@Name);";

            var id = await _dapper.ExecuteScalarAsync<int>(sql, encryptedService, cancellationToken);
            service.Id = id;

            return id;
        }

        /// <inheritdoc />
        public virtual async Task<int> UpsertBatchAsync(IEnumerable<ServiceDto> services, CancellationToken cancellationToken = default)
        {
            if (services == null || !services.Any()) return 0;

            // 1. Create encrypted clones for database storage
            var encryptedServices = services.Select(CreateEncryptedClone).ToList();

            var sql = $@"
                INSERT INTO Services ({SqlConstants.InsertColumns}) 
                VALUES ({SqlConstants.InsertValues})
                ON CONFLICT(LOWER(Name)) DO UPDATE SET
                {SqlConstants.UpsertSet};";

            // 2. Execute the batch upsert
            var affectedRows = await _dapper.ExecuteAsync(sql, encryptedServices, cancellationToken);

            // 3. Sync IDs back to the original DTOs
            // SQLite has a default limit of 999 parameters. For larger batches, 
            // we process the ID sync in chunks to avoid 'Too many SQL variables' errors.
            var serviceList = services.ToList();
            const int chunkSize = 900;

            for (int i = 0; i < serviceList.Count; i += chunkSize)
            {
                var currentChunk = serviceList.Skip(i).Take(chunkSize).ToList();

                // Normalize the input names to match the functional index requirement
                var lowerNames = currentChunk.Select(s => s.Name?.ToLowerInvariant()).ToList();

                // Fetch the generated IDs using LOWER(Name) to ensure index usage and case-insensitivity
                var idMap = (await _dapper.QueryAsync<(int Id, string Name)>(
                    "SELECT Id, Name FROM Services WHERE LOWER(Name) IN @names",
                    new { names = lowerNames }, cancellationToken))
                    .ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);

                // Update the original DTO references
                foreach (var service in currentChunk)
                {
                    // OrdinalIgnoreCase here handles the mapping between the 
                    // user's input (MyService) and the DB's stored casing (myservice).
                    if (idMap.TryGetValue(service.Name, out var id))
                    {
                        service.Id = id;
                    }
                }
            }

            return affectedRows;
        }

        /// <inheritdoc />
        public virtual async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var sql = "DELETE FROM Services WHERE Id = @Id;";
            return await _dapper.ExecuteAsync(sql, new { Id = id }, cancellationToken);
        }

        /// <inheritdoc />
        public virtual async Task<int> DeleteAsync(string? name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;

            var sql = "DELETE FROM Services WHERE LOWER(Name) = LOWER(@Name);";
            return await _dapper.ExecuteAsync(sql, new { Name = name.Trim() }, cancellationToken);
        }

        /// <inheritdoc />
        public virtual async Task<ServiceDto?> GetByIdAsync(int id, bool decrypt = true, CancellationToken cancellationToken = default)
        {
            var sql = "SELECT * FROM Services WHERE Id = @Id;";
            var cmd = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
            var dto = await _dapper.QuerySingleOrDefaultAsync<ServiceDto>(cmd);

            if (decrypt) DecryptDto(dto);
            return dto;
        }

        /// <inheritdoc />
        public virtual async Task<ServiceDto?> GetByNameAsync(string? name, bool decrypt = true, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var sql = "SELECT * FROM Services WHERE LOWER(Name) = LOWER(@Name);";
            var cmd = new CommandDefinition(sql, new { Name = name.Trim() }, cancellationToken: cancellationToken);
            var dto = await _dapper.QuerySingleOrDefaultAsync<ServiceDto>(cmd);

            if (decrypt) DecryptDto(dto);
            return dto;
        }

        /// <inheritdoc />
        public virtual ServiceDto? GetByName(string? name, bool decrypt = true)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            const string sql = "SELECT * FROM Services WHERE LOWER(Name) = LOWER(@Name);";
            var dto = _dapper.QuerySingleOrDefault<ServiceDto>(sql, new { Name = name.Trim() });

            if (dto != null && decrypt) DecryptDto(dto);
            return dto;
        }

        /// <inheritdoc />
        public async Task<int?> GetServicePidAsync(string? serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName)) return null;
            const string sql = "SELECT Pid FROM Services WHERE LOWER(Name) = LOWER(@Name) LIMIT 1;";
            return await _dapper.QueryFirstOrDefaultAsync<int?>(sql, new { Name = serviceName.Trim() }, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<ServiceConsoleStateDto?> GetServiceConsoleStateAsync(string? serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName)) return null;
            const string sql = @"
                SELECT Pid, ActiveStdoutPath, ActiveStderrPath 
                FROM Services 
                WHERE LOWER(Name) = LOWER(@Name)
                LIMIT 1;";

            return await _dapper.QueryFirstOrDefaultAsync<ServiceConsoleStateDto>(sql, new { Name = serviceName }, cancellationToken);
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
                SELECT * FROM Services 
                WHERE LOWER(Name)        LIKE LOWER(@Pattern) ESCAPE '\' 
                   OR LOWER(Description) LIKE LOWER(@Pattern) ESCAPE '\' 
                ORDER BY Name COLLATE NOCASE ASC;";

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

        /// <inheritdoc />
        public virtual async Task<string> ExportXmlAsync(string? name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var service = await GetByNameAsync(name, decrypt: true, cancellationToken: cancellationToken);
            if (service == null) return string.Empty;

            using (var stringWriter = new StringWriter())
            {
                ServiceDtoSerializer.Serialize(stringWriter, service);
                return stringWriter.ToString();
            }
        }

        /// <inheritdoc />
        public virtual async Task<bool> ImportXmlAsync(string xml, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(xml)) return false;

            try
            {
                var service = _xmlServiceSerializer.Deserialize(xml);
                if (service == null) return false;

                await UpsertAsync(service, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to import service from XML.", ex);
                return false;
            }
        }

        /// <inheritdoc />
        public virtual async Task<string> ExportJsonAsync(string? name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var service = await GetByNameAsync(name, decrypt: true, cancellationToken: cancellationToken);
            if (service == null) return string.Empty;

            return JsonConvert.SerializeObject(service, Formatting.Indented);
        }

        /// <inheritdoc />
        public virtual async Task<bool> ImportJsonAsync(string json, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                var service = _jsonServiceSerializer.Deserialize(json);
                if (service == null) return false;

                await UpsertAsync(service, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to import service from JSON.", ex);
                return false;
            }
        }

        #endregion

        #region Private Helpers

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

        /// <summary>
        /// Decrypts sensitive fields of a <see cref="ServiceDto"/> in-place.
        /// </summary>
        /// <param name="dto">The DTO to decrypt. If null, the method returns immediately.</param>
        private void DecryptDto(ServiceDto? dto)
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

        /// <summary>
        /// Encrypts a string if it is not null or white space.
        /// </summary>
        private string? EncryptIfPresent(string? value)
            => string.IsNullOrWhiteSpace(value) ? value : _secureData.Encrypt(value);

        /// <summary>
        /// Decrypts a string if it is not null or white space.
        /// </summary>
        private string? DecryptIfPresent(string? value)
            => string.IsNullOrWhiteSpace(value) ? value : _secureData.Decrypt(value);

        #endregion
    }
}
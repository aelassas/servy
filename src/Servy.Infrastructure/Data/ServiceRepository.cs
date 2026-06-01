using Dapper;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Repository for managing <see cref="ServiceDto"/> entities in the database.
    /// Handles secure password encryption, decryption, and centralizes SQL schemas to prevent divergence.
    /// </summary>
    public class ServiceRepository : IServiceRepository
    {
        private readonly IDapperExecutor _dapper;
        private readonly ISecureData _secureData;
        private readonly IXmlServiceSerializer _xmlServiceSerializer;
        private readonly IJsonServiceSerializer _jsonServiceSerializer;

        /// <summary>
        /// A centralized registry of sensitive fields that require encryption at rest.
        /// Iterating this array prevents divergence between encryption and decryption paths.
        /// </summary>
        private static readonly (Func<ServiceDto, string?> Get, Action<ServiceDto, string?> Set, string Name)[] SensitiveFields =
        {
            (d => d.Parameters,                    (d, v) => d.Parameters = v,                    nameof(ServiceDto.Parameters)),
            (d => d.FailureProgramParameters,      (d, v) => d.FailureProgramParameters = v,      nameof(ServiceDto.FailureProgramParameters)),
            (d => d.PreLaunchParameters,           (d, v) => d.PreLaunchParameters = v,           nameof(ServiceDto.PreLaunchParameters)),
            (d => d.PostLaunchParameters,          (d, v) => d.PostLaunchParameters = v,          nameof(ServiceDto.PostLaunchParameters)),
            (d => d.Password,                      (d, v) => d.Password = v,                      nameof(ServiceDto.Password)),
            (d => d.EnvironmentVariables,          (d, v) => d.EnvironmentVariables = v,          nameof(ServiceDto.EnvironmentVariables)),
            (d => d.PreLaunchEnvironmentVariables, (d, v) => d.PreLaunchEnvironmentVariables = v, nameof(ServiceDto.PreLaunchEnvironmentVariables)),
            (d => d.PreStopParameters,             (d, v) => d.PreStopParameters = v,             nameof(ServiceDto.PreStopParameters)),
            (d => d.PostStopParameters,            (d, v) => d.PostStopParameters = v,            nameof(ServiceDto.PostStopParameters)),
        };

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

            var id = await _dapper.ExecuteScalarAsync<int>(sql, encryptedService, cancellationToken: cancellationToken);
            service.Id = id;

            return id;
        }

        /// <inheritdoc />
        public virtual async Task<int> UpdateAsync(ServiceDto service, bool preserveExistingRuntimeState, bool preserveExistingCredentials, CancellationToken cancellationToken = default)
        {
            var encryptedService = CreateEncryptedClone(service);

            await PatchRuntimeStateAsync(
                incoming: encryptedService,
                preserveExistingRuntimeState: preserveExistingRuntimeState,
                preserveExistingCredentials: preserveExistingCredentials,
                cancellationToken: cancellationToken);

            var sql = $@"
                UPDATE Services SET
                {SqlConstants.UpdateSet}
                WHERE Id = @Id;";

            return await _dapper.ExecuteAsync(sql, encryptedService, cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public virtual int Update(ServiceDto service, bool preserveExistingRuntimeState, bool preserveExistingCredentials)
        {
            var encryptedService = CreateEncryptedClone(service);

            PatchRuntimeState(
                incoming: encryptedService,
                preserveExistingRuntimeState: preserveExistingRuntimeState,
                preserveExistingCredentials: preserveExistingCredentials
                );

            var sql = $@"
                UPDATE Services SET
                {SqlConstants.UpdateSet}
                WHERE Id = @Id;";

            return _dapper.Execute(sql, encryptedService);
        }

        /// <inheritdoc />
        public virtual async Task<int> UpsertAsync(ServiceDto service, bool preserveExistingRuntimeState, bool preserveExistingCredentials, CancellationToken cancellationToken = default)
        {
            var encryptedService = CreateEncryptedClone(service);

            await PatchRuntimeStateAsync(
                incoming: encryptedService,
                preserveExistingRuntimeState: preserveExistingRuntimeState,
                preserveExistingCredentials: preserveExistingCredentials,
                cancellationToken: cancellationToken);

            var sql = $@"
                INSERT INTO Services ({SqlConstants.InsertColumns}) 
                VALUES ({SqlConstants.InsertValues})
                ON CONFLICT(LOWER(Name)) DO UPDATE SET
                {SqlConstants.UpsertSet};
                SELECT id FROM Services WHERE LOWER(Name) = LOWER(@Name);";

            var id = await _dapper.ExecuteScalarAsync<int>(sql, encryptedService, cancellationToken: cancellationToken);
            service.Id = id;

            return id;
        }

        /// <inheritdoc />
        public virtual async Task<int> UpsertBatchAsync(IEnumerable<ServiceDto> services, CancellationToken cancellationToken = default)
        {
            var serviceList = services?.ToList();
            if (serviceList == null || !serviceList.Any()) return 0;

            // 1. Create encrypted clones for database storage
            var encryptedServices = serviceList.Select(CreateEncryptedClone).ToList();

            var sql = $@"
                INSERT INTO Services ({SqlConstants.InsertColumns}) 
                VALUES ({SqlConstants.InsertValues})
                ON CONFLICT(LOWER(Name)) DO UPDATE SET
                {SqlConstants.UpsertSet};";

            // Wrap the entire batch sequence in an explicit transaction to enforce snapshot isolation.
            // This prevents concurrent mutations from creating skewed or missing ID references.
            using (var tx = _dapper.BeginTransaction())
            {
                // Preserve runtime state and credentials for each incoming DTO
                foreach (var dto in encryptedServices)
                {
                    await PatchRuntimeStateAsync(
                        incoming: dto,
                        preserveExistingRuntimeState: true,
                        preserveExistingCredentials: true,
                        cancellationToken: cancellationToken);
                }

                // 2. Execute the batch upsert within the transaction scope 
                var affectedRows = await _dapper.ExecuteAsync(sql, encryptedServices, transaction: tx, cancellationToken: cancellationToken);

                // 3. Sync IDs back to the original DTOs
                // SQLite has a default limit of 999 parameters. For larger batches, 
                // we process the ID sync in chunks to avoid 'Too many SQL variables' errors.
                const int chunkSize = 900;

                for (int i = 0; i < serviceList.Count; i += chunkSize)
                {
                    var currentChunk = serviceList.Skip(i).Take(chunkSize).ToList();

                    // Pass original names; let SQLite lower both sides in the SQL itself
                    var names = currentChunk.Select(s => s.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();

                    // Fetch the generated IDs using NOCASE collation on Name comparison to ensure index usage and case-insensitivity within the same snapshot
                    var idMap = (await _dapper.QueryAsync<(int Id, string Name)>(
                        "SELECT Id, Name FROM Services WHERE Name COLLATE NOCASE IN @names",
                        new { names },
                        transaction: tx,
                        cancellationToken: cancellationToken))
                        .ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);

                    // Update the original DTO references
                    foreach (var service in currentChunk)
                    {
                        if (string.IsNullOrEmpty(service.Name)) continue;

                        // OrdinalIgnoreCase here handles the mapping between the 
                        // user's input (MyService) and the DB's stored casing (myservice).
                        if (idMap.TryGetValue(service.Name, out var id))
                        {
                            service.Id = id;
                        }
                    }
                }

                // Commit all changes atomically only after the DTO ID fields have been successfully resolved
                tx.Commit();

                return affectedRows;
            }
        }

        /// <inheritdoc />
        public virtual async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var sql = "DELETE FROM Services WHERE Id = @Id;";
            return await _dapper.ExecuteAsync(sql, new { Id = id }, cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public virtual async Task<int> DeleteAsync(string? name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;

            var sql = "DELETE FROM Services WHERE LOWER(Name) = LOWER(@Name);";
            return await _dapper.ExecuteAsync(sql, new { Name = name.Trim() }, cancellationToken: cancellationToken);
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

            if (decrypt) DecryptDto(dto);
            return dto;
        }

        /// <inheritdoc />
        public async Task<int?> GetServicePidAsync(string? serviceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(serviceName)) return null;
            const string sql = "SELECT Pid FROM Services WHERE LOWER(Name) = LOWER(@Name) LIMIT 1;";
            return await _dapper.QueryFirstOrDefaultAsync<int?>(sql, new { Name = serviceName.Trim() }, cancellationToken: cancellationToken);
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

            return await _dapper.QueryFirstOrDefaultAsync<ServiceConsoleStateDto>(sql, new { Name = serviceName.Trim() }, cancellationToken: cancellationToken);
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
                    try
                    {
                        DecryptDto(dto);
                    }
                    catch (InvalidOperationException ex)
                    {
                        HandleCorruptServiceDecryption(dto, ex);
                    }
                }
            }

            return list;
        }

        /// <inheritdoc />
        public virtual async Task<IEnumerable<ServiceDto>> SearchAsync(string keyword, bool decrypt = true, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return await GetAllAsync(decrypt, cancellationToken: cancellationToken);
            }

            var sql = @"
        SELECT * FROM Services 
        WHERE LOWER(Name)       LIKE LOWER(@Pattern) ESCAPE '\' 
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
                    try
                    {
                        DecryptDto(dto);
                    }
                    catch (InvalidOperationException ex)
                    {
                        HandleCorruptServiceDecryption(dto, ex);
                    }
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

            return _xmlServiceSerializer.Serialize(service) ?? string.Empty;
        }

        /// <inheritdoc />
        public virtual async Task<bool> ImportXmlAsync(string xml, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(xml)) return false;

            try
            {
                var service = _xmlServiceSerializer.Deserialize(xml);
                if (service == null) return false;

                // Preserve runtime state (PID, ActiveStdoutPath/ActiveStderrPath paths) if the service exists and is running.
                await UpsertAsync(
                    service,
                    preserveExistingRuntimeState: true,
                    preserveExistingCredentials: true,
                    cancellationToken: cancellationToken
                    );
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;   // let the caller observe cancellation
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

            return _jsonServiceSerializer.Serialize(service) ?? string.Empty;
        }

        /// <inheritdoc />
        public virtual async Task<bool> ImportJsonAsync(string json, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                var service = _jsonServiceSerializer.Deserialize(json);
                if (service == null) return false;

                await UpsertAsync(
                    service,
                    preserveExistingRuntimeState: true,
                    preserveExistingCredentials: true,
                    cancellationToken: cancellationToken
                    );
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;   // let the caller observe cancellation
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
        /// Retrieves the existing runtime state from the database and applies it to the incoming DTO.
        /// This ensures that importing a configuration over a running service does not clobber
        /// its PID or active log paths, which would break Manager tracking.
        /// </summary>
        /// <param name="incoming">The DTO deserialized from an import file.</param>
        /// <param name="preserveExistingRuntimeState">Required flag to preserve runtime state (PID, ActiveStdoutPath, ActiveStderrPath, PreviousStopTimeout).</param>
        /// <param name="preserveExistingCredentials">Required flag to preserve existing credentials (RunAsLocalSystem, UserAccount, Password).</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        private async Task PatchRuntimeStateAsync(ServiceDto incoming, bool preserveExistingRuntimeState, bool preserveExistingCredentials, CancellationToken cancellationToken)
        {
            if (!preserveExistingRuntimeState && !preserveExistingCredentials) return;

            if (string.IsNullOrWhiteSpace(incoming.Name)) return;

            // Fetch current state without decryption (performance optimization)
            var existing = await GetByNameAsync(incoming.Name, decrypt: false, cancellationToken: cancellationToken);

            if (existing != null)
                ApplyRuntimeState(
                    incoming: incoming,
                    existing: existing,
                    preserveExistingRuntimeState: preserveExistingRuntimeState,
                    preserveExistingCredentials: preserveExistingCredentials
                    );
        }

        /// <summary>
        /// Retrieves the existing runtime state from the database and applies it to the incoming DTO.
        /// This ensures that importing a configuration over a running service does not clobber
        /// its PID or active log paths, which would break Manager tracking.
        /// </summary>
        /// <param name="incoming">The DTO deserialized from an import file.</param>
        /// <param name="preserveExistingRuntimeState">Required flag to preserve runtime state (PID, ActiveStdoutPath, ActiveStderrPath, PreviousStopTimeout).</param>
        /// <param name="preserveExistingCredentials">Required flag to preserve existing credentials (RunAsLocalSystem, UserAccount, Password).</param>
        private void PatchRuntimeState(ServiceDto incoming, bool preserveExistingRuntimeState, bool preserveExistingCredentials)
        {
            if (!preserveExistingRuntimeState && !preserveExistingCredentials) return;

            if (string.IsNullOrWhiteSpace(incoming.Name)) return;

            // Fetch current state without decryption (performance optimization)
            var existing = GetByName(incoming.Name, decrypt: false);

            if (existing != null)
                ApplyRuntimeState(
                    incoming: incoming,
                    existing: existing,
                    preserveExistingRuntimeState: preserveExistingRuntimeState,
                    preserveExistingCredentials: preserveExistingCredentials
                    );
        }

        /// <summary>
        /// Synchronizes transient, runtime-only operational fields from a verified database record 
        /// into an incoming configuration instance before execution mutations.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Single Source of Truth (SSoT):</b> This utility acts as the centralized authority for tracking fields 
        /// that are excluded from external export files (where <c>ShouldSerialize*() => false</c>). 
        /// </para>
        /// <para>
        /// If these fields are omitted or unmapped during an entry import lifecycle, the persistence engine 
        /// would inadvertently overwrite critical operational telemetry columns with <c>NULL</c> values, 
        /// decoupling the active running background processes from system instrumentation tracking hooks.
        /// </para>
        /// </remarks>
        /// <param name="incoming">The fresh configuration DTO targeted for database persistence.</param>
        /// <param name="existing">The authoritative snapshot currently stored in the database cache layer.</param>
        /// <param name="preserveExistingRuntimeState">Required flag to preserve runtime state (PID, ActiveStdoutPath, ActiveStderrPath, PreviousStopTimeout).</param>
        /// <param name="preserveExistingCredentials">Required flag to preserve existing credentials (RunAsLocalSystem, UserAccount, Password).</param>
        private static void ApplyRuntimeState(ServiceDto incoming, ServiceDto existing, bool preserveExistingRuntimeState, bool preserveExistingCredentials)
        {
            if (preserveExistingRuntimeState)
            {
                // Runtime state - never present in export files
                incoming.Pid = existing.Pid;
                incoming.ActiveStdoutPath = existing.ActiveStdoutPath;
                incoming.ActiveStderrPath = existing.ActiveStderrPath;
                incoming.PreviousStopTimeout = existing.PreviousStopTimeout;
            }

            if (preserveExistingCredentials)
            {
                // Credentials & account - intentionally excluded from export for security,
                // but must be preserved on import so updates do not wipe them.
                incoming.RunAsLocalSystem = existing.RunAsLocalSystem;
                incoming.UserAccount = existing.UserAccount;
                incoming.Password = existing.Password;
            }

        }

        /// <summary>
        /// Creates a shallow clone of the ServiceDto and encrypts sensitive fields.
        /// This prevents double-encryption and unintended mutation of the input object.
        /// </summary>
        /// <param name="source">The original DTO to clone and encrypt.</param>
        /// <returns>A new <see cref="ServiceDto"/> instance with sensitive fields encrypted.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a specific field fails to encrypt.</exception>
        private ServiceDto CreateEncryptedClone(ServiceDto source)
        {
            // 1. Perform the shallow clone to isolate mutations from the original DTO
            var clone = (ServiceDto)source.Clone();

            // 2. Iterate using the refactored triplet to maintain parity with the decryption path
            foreach (var (get, set, name) in SensitiveFields)
            {
                try
                {
                    // EncryptIfPresent handles null/whitespace checks before invoking _secureData.Encrypt
                    set(clone, EncryptIfPresent(get(clone)));
                }
                catch (Exception ex)
                {
                    // 3. Log granular failure details to pinpoint the exact field causing the drift
                    Logger.Error($"Failed to encrypt field '{name}' for service '{source.Name}'.", ex);

                    // 4. Rethrow a descriptive exception to prevent an unencrypted or half-encrypted 
                    // DTO from being persisted to the database.
                    throw new InvalidOperationException($"Encryption failed for field '{name}' on service '{source.Name}'.", ex);
                }
            }

            return clone;
        }

        /// <summary>
        /// Decrypts sensitive fields of a <see cref="ServiceDto"/> in-place.
        /// </summary>
        /// <param name="dto">The DTO to decrypt. If null, the method returns immediately.</param>
        /// <exception cref="InvalidOperationException">Thrown when a specific field fails to decrypt.</exception>
        private void DecryptDto(ServiceDto? dto)
        {
            if (dto == null) return;

            foreach (var (get, set, name) in SensitiveFields)
            {
                try
                {
                    // DecryptIfPresent handles the null/whitespace check internally
                    set(dto, DecryptIfPresent(get(dto)));
                }
                catch (Exception ex)
                {
                    // Log with specific field and service context for faster triage
                    Logger.Error($"Failed to decrypt field '{name}' for service '{dto.Name}'.", ex);

                    // Rethrowing prevents the caller from operating on a DTO with mixed plaintext and ciphertext
                    throw new InvalidOperationException($"Decryption failed for field '{name}' on service '{dto.Name}'.", ex);
                }
            }
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

        /// <summary>
        /// Gracefully handles individual record decryption failures to isolate data corruption.
        /// This prevents a single invalid row from poisoning multi-record queries.
        /// </summary>
        private void HandleCorruptServiceDecryption(ServiceDto dto, InvalidOperationException ex)
        {
            // Capture the original root cause name if available for actionable diagnostic feedback
            string rootCauseName = ex.InnerException?.GetType().Name ?? "CryptographicException";

            // Explicitly update descriptions to flag the target record in the UI
            dto.Description = $"[DECRYPTION FAILED: {rootCauseName}] The record's key or payload is corrupt. " +
                              $"Original Description: {dto.Description}";

            // Centralized scrub loop using the pre-existing reflection schema to safely strip poison data
            foreach (var field in SensitiveFields)
            {
                try
                {
                    field.Set(dto, null);
                }
                catch (Exception internalEx)
                {
                    Logger.Warn($"Could not safely sanitize corrupted field '{field.Name}' for service '{dto.Name}': {internalEx.Message}");
                }
            }
        }

        #endregion
    }
}
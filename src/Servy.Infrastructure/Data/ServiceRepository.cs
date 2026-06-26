using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using Servy.Core.Security;
using Servy.Core.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        private static readonly (Func<ServiceDto, string> Get, Action<ServiceDto, string> Set, string Name)[] SensitiveFields =
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
                INSERT INTO {SqlConstants.ServicesTableName} ({SqlConstants.InsertColumns}) 
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
                UPDATE {SqlConstants.ServicesTableName} SET
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
                UPDATE {SqlConstants.ServicesTableName} SET
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
                INSERT INTO {SqlConstants.ServicesTableName} ({SqlConstants.InsertColumns}) 
                VALUES ({SqlConstants.InsertValues})
                ON CONFLICT(Name COLLATE UNICODE_NOCASE) DO UPDATE SET
                {SqlConstants.UpsertSet};
                SELECT id FROM {SqlConstants.ServicesTableName} WHERE Name = @Name COLLATE UNICODE_NOCASE;";

            var id = await _dapper.ExecuteScalarAsync<int>(sql, encryptedService, cancellationToken: cancellationToken);
            service.Id = id;

            return id;
        }

        /// <inheritdoc />
        public virtual async Task<int> UpsertBatchAsync(IEnumerable<ServiceDto> services, CancellationToken cancellationToken = default)
        {
            var serviceList = services?.ToList();
            if (serviceList == null || !serviceList.Any()) return 0;

            // 1. Bulk pre-fetching dictionary optimization to bypass N+1 sequential row reads.
            var existingMap = new Dictionary<string, ServiceDto>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < serviceList.Count; i += AppConfig.DbBatchIdSyncChunkSize)
            {
                var currentChunk = serviceList.Skip(i).Take(AppConfig.DbBatchIdSyncChunkSize).ToList();
                var chunkNames = currentChunk.Select(s => s.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();

                if (chunkNames.Any())
                {
                    var existingRows = (await _dapper.QueryAsync<ServiceDto>(
                        $"SELECT * FROM {SqlConstants.ServicesTableName} WHERE Name COLLATE UNICODE_NOCASE IN @chunkNames",
                        new { chunkNames },
                        cancellationToken: cancellationToken)).ToList();

                    foreach (var row in existingRows)
                    {
                        if (!string.IsNullOrEmpty(row.Name))
                        {
                            // CRITICAL: Decrypt the existing DB record back to plain text 
                            // so that ApplyRuntimeState passes clear values to CreateEncryptedClone
                            SafeDecrypt(row);
                            existingMap[row.Name] = row;
                        }
                    }
                }
            }

            // 2. Patch runtime state and credentials on raw plain-text objects,
            // then create isolated encrypted clones for safe database persistence.
            var encryptedServices = new List<ServiceDto>();
            foreach (var rawDto in serviceList)
            {
                var localClone = (ServiceDto)rawDto.Clone();

                if (!string.IsNullOrEmpty(localClone.Name) && existingMap.TryGetValue(localClone.Name, out var existing))
                {
                    ApplyRuntimeState(
                        incoming: localClone,
                        existing: existing,
                        preserveExistingRuntimeState: true,
                        preserveExistingCredentials: true);
                }

                // Encrypt the finalized object properties cleanly right before pushing to disk
                encryptedServices.Add(CreateEncryptedClone(localClone));
            }

            var sql = $@"
                INSERT INTO {SqlConstants.ServicesTableName} ({SqlConstants.InsertColumns}) 
                VALUES ({SqlConstants.InsertValues})
                ON CONFLICT(Name COLLATE UNICODE_NOCASE) DO UPDATE SET
                {SqlConstants.UpsertSet};";

            // Wrap the entire batch sequence in an explicit transaction to enforce snapshot isolation.
            using (var tx = _dapper.BeginTransaction())
            {
                // 3. Execute the batch upsert within the transaction scope 
                var affectedRows = await _dapper.ExecuteAsync(sql, encryptedServices, transaction: tx, cancellationToken: cancellationToken);

                // 4. Sync IDs back to the original DTOs
                // SQLite has a default limit of 999 parameters. For larger batches, 
                // we process the ID sync in chunks to avoid 'Too many SQL variables' errors.
                for (int i = 0; i < serviceList.Count; i += AppConfig.DbBatchIdSyncChunkSize)
                {
                    var currentChunk = serviceList.Skip(i).Take(AppConfig.DbBatchIdSyncChunkSize).ToList();

                    // Pass original names; let SQLite lower both sides in the SQL itself
                    var names = currentChunk.Select(s => s.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();

                    var idMap = (await _dapper.QueryAsync<(int Id, string Name)>(
                        $"SELECT Id, Name FROM {SqlConstants.ServicesTableName} WHERE Name COLLATE UNICODE_NOCASE IN @names",
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
            string sql = $"DELETE FROM {SqlConstants.ServicesTableName} WHERE Id = @Id;";
            return await _dapper.ExecuteAsync(sql, new { Id = id }, cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public virtual async Task<int> DeleteAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;

            string sql = $"DELETE FROM {SqlConstants.ServicesTableName} WHERE Name = @Name COLLATE UNICODE_NOCASE;";

            return await ResolveWithLegacyFallbackAsync(
                sql: sql,
                queryExecutor: (executedSql, parameters) => _dapper.ExecuteAsync(executedSql, parameters, cancellationToken: cancellationToken),
                name: name,
                fallbackEvaluationPredicate: rowsAffected => rowsAffected == 0,
                cancellationToken: cancellationToken
            );
        }

        /// <inheritdoc />
        public virtual async Task<ServiceDto> GetByIdAsync(int id, bool decrypt = true, CancellationToken cancellationToken = default)
        {
            string sql = $"SELECT * FROM {SqlConstants.ServicesTableName} WHERE Id = @Id;";
            var dto = await _dapper.QuerySingleOrDefaultAsync<ServiceDto>(sql, new { Id = id }, cancellationToken: cancellationToken);

            if (decrypt) SafeDecrypt(dto);
            return dto;
        }

        /// <inheritdoc />
        public virtual async Task<ServiceDto> GetByNameAsync(string name, bool decrypt = true, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            string sql = $"SELECT * FROM {SqlConstants.ServicesTableName} WHERE Name = @Name COLLATE UNICODE_NOCASE;";
            var dto = await ResolveByNameAsync<ServiceDto>(sql, name, cancellationToken: cancellationToken);

            if (decrypt) SafeDecrypt(dto);
            return dto;
        }

        /// <inheritdoc />
        public virtual ServiceDto GetByName(string name, bool decrypt = true)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            string sql = $"SELECT * FROM {SqlConstants.ServicesTableName} WHERE Name = @Name COLLATE UNICODE_NOCASE;";

            var dto = ResolveWithLegacyFallback<ServiceDto>(
                sql: sql,
                queryExecutor: (executedSql, parameters) => _dapper.QuerySingleOrDefault<ServiceDto>(executedSql, parameters),
                name: name,
                fallbackEvaluationPredicate: result => EqualityComparer<ServiceDto>.Default.Equals(result, default)
            );

            if (decrypt) SafeDecrypt(dto);
            return dto;
        }

        /// <inheritdoc />
        public virtual async Task<int?> GetServicePidAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            string sql = $"SELECT Pid FROM {SqlConstants.ServicesTableName} WHERE Name = @Name COLLATE UNICODE_NOCASE LIMIT 1;";
            var pid = await ResolveByNameAsync<int?>(sql, name, cancellationToken: cancellationToken);
            return pid;
        }

        /// <inheritdoc />
        public virtual async Task<ServiceConsoleStateDto> GetServiceConsoleStateAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            string sql = $@"
                SELECT Pid, ActiveStdoutPath, ActiveStderrPath 
                FROM {SqlConstants.ServicesTableName} 
                WHERE Name = @Name COLLATE UNICODE_NOCASE
                LIMIT 1;";
            var dto = await ResolveByNameAsync<ServiceConsoleStateDto>(sql, name, cancellationToken: cancellationToken);
            return dto;
        }

        /// <inheritdoc />
        public virtual async Task<IEnumerable<ServiceDto>> GetAllAsync(bool decrypt = true, CancellationToken cancellationToken = default)
        {
            string sql = $"SELECT * FROM {SqlConstants.ServicesTableName} ORDER BY Name COLLATE UNICODE_NOCASE ASC;";
            var list = await _dapper.QueryAsync<ServiceDto>(sql, cancellationToken: cancellationToken);

            if (decrypt) SafeDecryptAll(list, cancellationToken);

            return list;
        }

        /// <inheritdoc />
        public virtual async Task<IEnumerable<ServiceDto>> SearchAsync(string keyword, bool decrypt = true, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return await GetAllAsync(decrypt, cancellationToken: cancellationToken);
            }

            // Optimized query layout configuration. SQLite executes 'LIKE' operations case-insensitively for standard ASCII
            // elements inherently by default configuration, but leveraging explicit ESCAPE patterns protects complex paths.
            var sql = $@"
                SELECT * FROM {SqlConstants.ServicesTableName} 
                WHERE Name       LIKE @Pattern ESCAPE '\' 
                   OR Description LIKE @Pattern ESCAPE '\' 
                ORDER BY Name COLLATE UNICODE_NOCASE ASC;";

            var escapedKeyword = keyword.Trim()
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");

            var pattern = $"%{escapedKeyword}%";

            var list = (await _dapper.QueryAsync<ServiceDto>(sql, new { Pattern = pattern }, cancellationToken: cancellationToken)).ToList();

            if (decrypt) SafeDecryptAll(list, cancellationToken);

            return list;
        }

        /// <inheritdoc />
        public virtual async Task<string> ExportXmlAsync(string name, CancellationToken cancellationToken = default)
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
        public virtual async Task<string> ExportJsonAsync(string name, CancellationToken cancellationToken = default)
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
        /// Executes an asynchronous single-row retrieval query by a service name parameter, 
        /// funneling execution through the centralized legacy tracking fallback pipeline.
        /// </summary>
        /// <typeparam name="T">The expected return type of the database record or primitive scalar value.</typeparam>
        /// <param name="sql">The target parameterized SQL statement to execute.</param>
        /// <param name="name">The name of the service used to query the data store layer.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests during the asynchronous sequence.</param>
        /// <returns>
        /// A task representing the asynchronous operation. The task result contains the retrieved 
        /// value of type <typeparamref name="T"/> if matched; otherwise, the default value of <typeparamref name="T"/>.
        /// </returns>
        private Task<T> ResolveByNameAsync<T>(string sql, string name, CancellationToken cancellationToken)
        {
            return ResolveWithLegacyFallbackAsync(
                sql: sql,
                queryExecutor: (executedSql, parameters) => _dapper.QuerySingleOrDefaultAsync<T>(executedSql, parameters, cancellationToken: cancellationToken),
                name: name,
                fallbackEvaluationPredicate: result => EqualityComparer<T>.Default.Equals(result, default),
                cancellationToken: cancellationToken
            );
        }

        /// <summary>
        /// Orchestrates an asynchronous data store command using a unified legacy whitespace fallback execution pattern.
        /// Evaluates the primary trimmed criteria, conditionally routing to verbatim untrimmed parameters if a historical 
        /// record configuration (Servy &lt;= 8.3 zombie rows) matches the evaluation predicate.
        /// </summary>
        /// <typeparam name="T">The type of the expected query return payload or operational status identifier.</typeparam>
        /// <param name="sql">The parameterized SQL statement to execute (must use a @Name parameter).</param>
        /// <param name="queryExecutor">Delegate that runs <paramref name="sql"/> with the supplied parameters.</param>
        /// <param name="fallbackEvaluationPredicate">Returns true when the trimmed-name result is "empty" and the verbatim-name fallback should be attempted.</param>
        /// <returns>A task representing the asynchronous orchestration. The task result contains the trimmed-name query, or the verbatim-name fallback result for legacy whitespace rows.</returns>
        private static async Task<T> ResolveWithLegacyFallbackAsync<T>(
            string sql,
            Func<string, object, Task<T>> queryExecutor,
            string name,
            Func<T, bool> fallbackEvaluationPredicate,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await queryExecutor(sql, new { Name = name.Trim() });

            // Legacy rows (Servy <= 8.3) stored Name with whitespace verbatim.
            if (fallbackEvaluationPredicate(result) && name != name.Trim())
            {
                cancellationToken.ThrowIfCancellationRequested();
                result = await queryExecutor(sql, new { Name = name });
            }

            return result;
        }

        /// <summary>
        /// Orchestrates a synchronous data store command using a unified legacy whitespace fallback execution pattern.
        /// Evaluates the primary trimmed criteria, conditionally routing to verbatim untrimmed parameters if a historical 
        /// record configuration (Servy &lt;= 8.3 zombie rows) matches the evaluation predicate.
        /// </summary>
        /// <typeparam name="T">The type of the expected query return payload or operational status identifier.</typeparam>
        /// <param name="sql">The parameterized SQL statement to execute (must use a @Name parameter).</param>
        /// <param name="queryExecutor">Delegate that runs <paramref name="sql"/> with the supplied parameters.</param>
        /// <param name="fallbackEvaluationPredicate">Returns true when the trimmed-name result is "empty" and the verbatim-name fallback should be attempted.</param>
        /// <returns>The result of the trimmed-name query, or the verbatim-name fallback result for legacy whitespace rows.</returns>
        private static T ResolveWithLegacyFallback<T>(
            string sql,
            Func<string, object, T> queryExecutor,
            string name,
            Func<T, bool> fallbackEvaluationPredicate)
        {
            var result = queryExecutor(sql, new { Name = name.Trim() });

            // Legacy rows (Servy <= 8.3) stored Name with whitespace verbatim.
            if (fallbackEvaluationPredicate(result) && name != name.Trim())
            {
                result = queryExecutor(sql, new { Name = name });
            }

            return result;
        }

        /// <summary>
        /// Safely attempts decryption on a single DTO, channeling errors to isolation logic.
        /// </summary>
        private void SafeDecrypt(ServiceDto dto)
        {
            if (dto == null) return;

            try
            {
                DecryptDto(dto);
            }
            catch (InvalidOperationException ex)
            {
                HandleCorruptServiceDecryption(dto, ex);
            }
        }

        /// <summary>
        /// Iterates over a collection of DTOs, validating cancellation boundaries and executing safe field isolation.
        /// </summary>
        private void SafeDecryptAll(IEnumerable<ServiceDto> list, CancellationToken cancellationToken)
        {
            foreach (var dto in list)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SafeDecrypt(dto);
            }
        }

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
        /// <param name="existing">The row currently stored in the database for the same service name.</param>
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
        /// <exception cref="ArgumentNullException">Thrown when the target input source reference parameter is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a specific field fails to encrypt.</exception>
        private ServiceDto CreateEncryptedClone(ServiceDto source)
        {
            // Implemented explicit entrance argument null verification pattern matching code conventions 
            // to stop propagation bugs before causing deeply nested NullReferenceExceptions.
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            // 1. Perform the shallow clone to isolate mutations from the original DTO
            var clone = (ServiceDto)source.Clone();

            // 2. Iterate the SensitiveFields triplets to maintain parity with the decryption path
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
        private void DecryptDto(ServiceDto dto)
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
        private string EncryptIfPresent(string value)
            => string.IsNullOrWhiteSpace(value) ? value : _secureData.Encrypt(value);

        /// <summary>
        /// Decrypts a string if it is not null or white space.
        /// </summary>
        private string DecryptIfPresent(string value)
            => string.IsNullOrWhiteSpace(value) ? value : _secureData.Decrypt(value);

        /// <summary>
        /// Gracefully handles individual record decryption failures to isolate data corruption.
        /// This prevents a single invalid row from poisoning multi-record queries.
        /// </summary>
        private void HandleCorruptServiceDecryption(ServiceDto dto, InvalidOperationException ex)
        {
            if (dto == null) return;

            // Capture the original root cause name if available for actionable diagnostic feedback
            string rootCauseName = ex.InnerException?.GetType().Name ?? ex.GetType().Name;

            // Explicitly update descriptions to flag the target record in the UI
            dto.Description = $"[DECRYPTION FAILED: {rootCauseName}] The record's key or payload is corrupt. " +
                              $"Original Description: {dto.Description}";

            // Scrub every sensitive field via the SensitiveFields delegate table
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
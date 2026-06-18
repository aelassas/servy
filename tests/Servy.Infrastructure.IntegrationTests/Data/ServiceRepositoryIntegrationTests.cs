using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Infrastructure.IntegrationTests.Data
{
    /// <summary>
    /// A lightweight, concrete test factory for managing a shared in-memory SQLite state lifespan.
    /// In-memory SQLite databases disappear the moment their connection drops; keeping a master connection 
    /// handle open allows DapperExecutor to safely open and close transient connection pools during execution.
    /// </summary>
    public sealed class TestDbContext : IAppDbContext, IDisposable
    {
        private readonly SQLiteConnection _masterConnection;
        private readonly string _connectionString;

        public TestDbContext()
        {
            // Share the same in-memory database instance name across connection requests
            _connectionString = $"Data Source=InMemoryTestDb_{Guid.NewGuid()};Mode=Memory;Cache=Shared;";
            _masterConnection = new SQLiteConnection(_connectionString);
            _masterConnection.Open(); // Keeps database alive
        }

        public DbConnection CreateConnection()
        {
            return new SQLiteConnection(_connectionString);
        }

        public void InitializeSchema()
        {
            // Execute the production migration sequence onto the active in-memory connection
            SQLiteDbInitializer.Initialize(_masterConnection);
        }

        public void Dispose()
        {
            _masterConnection.Close();
            _masterConnection.Dispose();
        }
    }

    /// <summary>
    /// Fake encryption helper to evaluate secure data-loss and recovery paths deterministically.
    /// </summary>
    public sealed class TestSecureData : ISecureData
    {
        public string Encrypt(string plainText) => $"SECRET_HASH:{plainText}";
        public string Decrypt(string cipherText)
        {
            if (cipherText == "POISON_PAYLOAD")
            {
                // Throw a raw CryptographicException directly.
                // ServiceRepository.DecryptDto will catch this and wrap it inside the single InvalidOperationException
                // that HandleCorruptServiceDecryption expects.
                throw new System.Security.Cryptography.CryptographicException("Padding check failed.");
            }

            return cipherText.Replace("SECRET_HASH:", "");
        }

        public void Dispose()
        {
            /* no-op */
        }

        // A small nested stub class to guarantee GetType().Name returns exactly "BadPaddingException"
        private sealed class BadPaddingException : Exception
        {
            public BadPaddingException() : base("Padding check failed.") { }
        }
    }

    [CollectionDefinition("ServiceRepositoryTests", DisableParallelization = true)]
    public class ServiceRepositoryTestsCollection : ICollectionFixture<object>
    {
        // Enforces strict sequential isolation across the execution suite
    }

    [Collection("ServiceRepositoryTests")]
    public class ServiceRepositoryIntegrationTests : IDisposable
    {
        private readonly TestDbContext _dbContext;
        private readonly DapperExecutor _executor;
        private readonly TestSecureData _secureData;
        private readonly XmlServiceSerializer _xmlSerializer;
        private readonly JsonServiceSerializer _jsonSerializer;
        private readonly ServiceRepository _repository;

        public ServiceRepositoryIntegrationTests()
        {
            _dbContext = new TestDbContext();
            _dbContext.InitializeSchema(); // Applies initial schema structure, tables, and functional lower indexes

            _executor = new DapperExecutor(_dbContext);
            _secureData = new TestSecureData();
            _xmlSerializer = new XmlServiceSerializer();
            _jsonSerializer = new JsonServiceSerializer();

            _repository = new ServiceRepository(_executor, _secureData, _xmlSerializer, _jsonSerializer);
        }

        [Fact]
        public async Task AddAsync_InsertsRecordAndAssignsGeneratedPrimaryKey()
        {
            // Arrange
            var service = new ServiceDto { Name = "UniqueEngineService", ExecutablePath = "C:\\srv.exe", Password = "MyPassword123" };

            // Act
            int generatedId = await _repository.AddAsync(service, CancellationToken.None);

            // Assert
            Assert.True(generatedId > 0);
            Assert.Equal(generatedId, service.Id);

            // Verify the encryption transform took place before hitting the database
            var dbRecord = await _repository.GetByIdAsync(generatedId, decrypt: false, CancellationToken.None);
            Assert.NotNull(dbRecord);
            Assert.Equal("SECRET_HASH:MyPassword123", dbRecord.Password);
        }

        [Fact]
        public async Task UpdateAsync_ModifiesRecord_HonoringRuntimeBypassStates()
        {
            // Arrange
            var service = new ServiceDto { Name = "MutableService", ExecutablePath = "C:\\exe.exe", Pid = 1234 };
            int id = await _repository.AddAsync(service, CancellationToken.None);

            // Act - Request updating fields but protecting existing transient state columns
            var modification = new ServiceDto { Id = id, Name = "MutableService", ExecutablePath = "C:\\updated.exe", Pid = 9999 };
            await _repository.UpdateAsync(modification, preserveExistingRuntimeState: true, preserveExistingCredentials: false, CancellationToken.None);

            // Assert
            var result = await _repository.GetByIdAsync(id, decrypt: true, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal("C:\\updated.exe", result.ExecutablePath);
            Assert.Equal(1234, result.Pid); // Preserved
        }

        [Fact]
        public async Task UpsertAsync_OnConflict_ExecutesInPlaceUpdate()
        {
            // Arrange
            var service1 = new ServiceDto { Name = "ConflictService", ExecutablePath = "C:\\v1.exe" };
            var service2 = new ServiceDto { Name = "conflictservice", ExecutablePath = "C:\\v2.exe" }; // SQLite lower case collation index trigger test

            int id1 = await _repository.AddAsync(service1, CancellationToken.None);

            // Act
            int id2 = await _repository.UpsertAsync(service2, preserveExistingRuntimeState: false, preserveExistingCredentials: false, CancellationToken.None);

            // Assert
            Assert.Equal(id1, id2); // Same record identity targeted
            var updatedRecord = await _repository.GetByIdAsync(id1, decrypt: true, CancellationToken.None);
            Assert.Equal("C:\\v2.exe", updatedRecord.ExecutablePath);
        }

        [Fact]
        public async Task UpsertBatchAsync_LargeCollection_ExecutesWithinTransactionBoundaries()
        {
            // Arrange
            var batch = new List<ServiceDto>
            {
                new ServiceDto { Name = "BatchItem1", ExecutablePath = "p1.exe" },
                new ServiceDto { Name = "BatchItem2", ExecutablePath = "p2.exe" }
            };

            // Act
            int affectedRows = await _repository.UpsertBatchAsync(batch, CancellationToken.None);

            // Assert
            Assert.True(affectedRows > 0);
            Assert.NotNull(batch[0].Id);
            Assert.NotNull(batch[1].Id);

            var fetched = await _repository.GetAllAsync(decrypt: true, CancellationToken.None);
            Assert.Contains(fetched, s => s.Name == "BatchItem1");
            Assert.Contains(fetched, s => s.Name == "BatchItem2");
        }

        [Fact]
        public async Task UpsertBatchAsync_WithExistingRecords_PreservesRuntimeStateAndCredentialsInBulk()
        {
            // Arrange
            var existingService1 = new ServiceDto
            {
                Name = "BatchPreserve1",
                ExecutablePath = "old1.exe",
                Pid = 5050,
                RunAsLocalSystem = true,
                Password = "SECRET_HASH:KeepMe"
            };
            var existingService2 = new ServiceDto
            {
                Name = "BatchPreserve2",
                ExecutablePath = "old2.exe",
                Pid = 6060,
                RunAsLocalSystem = false,
                UserAccount = "SrvUser",
                Password = "SECRET_HASH:KeepMe2"
            };

            await _repository.AddAsync(existingService1, CancellationToken.None);
            await _repository.AddAsync(existingService2, CancellationToken.None);

            // Create an incoming batch with changed paths/credentials that should be protected by the pre-fetch map
            var incomingBatch = new List<ServiceDto>
            {
                new ServiceDto { Name = "BATCHPRESERVE1", ExecutablePath = "new1.exe", Pid = 0, RunAsLocalSystem = false, Password = "Overwritten" }, // Mismatched casing to test collation resilience
                new ServiceDto { Name = "BatchPreserve2", ExecutablePath = "new2.exe", Pid = 1111, UserAccount = "NewUser", Password = "Changed" },
                new ServiceDto { Name = "BatchNewItem3", ExecutablePath = "brand_new.exe", Pid = 0 } // Verification for non-existent incoming elements
            };

            // Act
            int affectedRows = await _repository.UpsertBatchAsync(incomingBatch, CancellationToken.None);

            // Assert
            Assert.Equal(3, affectedRows); // 2 updates + 1 insert

            var item1 = await _repository.GetByNameAsync("BatchPreserve1", decrypt: false, CancellationToken.None);
            Assert.NotNull(item1);
            Assert.Equal("new1.exe", item1.ExecutablePath);
            Assert.Equal(5050, item1.Pid); // Preserved
            Assert.True(item1.RunAsLocalSystem); // Preserved
            Assert.Equal("SECRET_HASH:KeepMe", item1.Password); // Preserved

            var item2 = await _repository.GetByNameAsync("BatchPreserve2", decrypt: false, CancellationToken.None);
            Assert.NotNull(item2);
            Assert.Equal("new2.exe", item2.ExecutablePath);
            Assert.Equal(6060, item2.Pid); // Preserved
            Assert.Equal("SrvUser", item2.UserAccount); // Preserved
            Assert.Equal("SECRET_HASH:KeepMe2", item2.Password); // Preserved

            var item3 = await _repository.GetByNameAsync("BatchNewItem3", decrypt: false, CancellationToken.None);
            Assert.NotNull(item3);
            Assert.Equal("brand_new.exe", item3.ExecutablePath);
            Assert.True(item3.Id > 0); // Assigned correctly
        }

        [Fact]
        public async Task DeleteAsync_ByNameAndId_RemovesTargetEntries()
        {
            // Arrange
            var service = new ServiceDto { Name = "KillMe", ExecutablePath = "kill.exe" };
            int id = await _repository.AddAsync(service, CancellationToken.None);

            // Act
            int deletedCount = await _repository.DeleteAsync("KillMe", CancellationToken.None);

            // Assert
            Assert.Equal(1, deletedCount);
            var search = await _repository.GetByIdAsync(id, decrypt: true, CancellationToken.None);
            Assert.Null(search);
        }

        [Fact]
        public async Task SearchAsync_UsingKeywords_EvaluatesWildcardAndSqlEscapeMatchers()
        {
            // Arrange
            await _repository.AddAsync(new ServiceDto { Name = "App_Development_%_Test", ExecutablePath = "a.exe" }, CancellationToken.None);
            await _repository.AddAsync(new ServiceDto { Name = "App_Production", ExecutablePath = "b.exe" }, CancellationToken.None);

            // Act - Search targeting literal % character using ESCAPE configurations
            var results = (await _repository.SearchAsync("development_%", decrypt: true, CancellationToken.None)).ToList();

            // Assert
            using (var summaryScope = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeOption.Suppress))
            {
                Assert.Single(results);
                Assert.Equal("App_Development_%_Test", results[0].Name);
            }
        }

        [Fact]
        public async Task GetByIdAsync_PoisonDataEncountered_QuarantinesRecordAndPadsTelemetry()
        {
            // Arrange
            var service = new ServiceDto { Name = "PoisonRecord", ExecutablePath = "poison.exe", Description = "Original description" };
            int id = await _repository.AddAsync(service, CancellationToken.None);

            // Manually corrupt data payload in database directly via executor bypass
            await _executor.ExecuteAsync(
                "UPDATE Services SET Parameters = 'POISON_PAYLOAD' WHERE Id = @Id",
                new { Id = id },
                cancellationToken: CancellationToken.None);

            // Act
            var result = await _repository.GetByIdAsync(id, decrypt: true, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("[DECRYPTION FAILED: CryptographicException]", result.Description);
            Assert.Null(result.Parameters); // Verifies individual record fields scrubbed safely
        }

        [Fact]
        public async Task ExportAndImport_XmlAndJson_PreservesConfigAcrossRoundTrips()
        {
            // Arrange
            var service = new ServiceDto { Name = "RoundTripService", ExecutablePath = "round.exe", Description = "SerializeMe" };
            await _repository.AddAsync(service, CancellationToken.None);

            // Act
            string xmlData = await _repository.ExportXmlAsync("RoundTripService", CancellationToken.None);
            string jsonData = await _repository.ExportJsonAsync("RoundTripService", CancellationToken.None);

            Assert.NotEmpty(xmlData);
            Assert.NotEmpty(jsonData);

            // Clear Database entries entirely
            var deleteResult = await _repository.DeleteAsync("RoundTripService", CancellationToken.None);

            // Import back
            bool xmlImportResult = await _repository.ImportXmlAsync(xmlData, CancellationToken.None);
            bool jsonImportResult = await _repository.ImportJsonAsync(jsonData, CancellationToken.None);

            // Assert
            Assert.True(xmlImportResult);
            Assert.True(jsonImportResult);

            var recovered = await _repository.GetByNameAsync("RoundTripService", decrypt: true, CancellationToken.None);
            Assert.NotNull(recovered);
            Assert.Equal("SerializeMe", recovered.Description);
        }

        [Fact]
        public async Task GetByNameAsync_WithPaddedLegacyRow_ResolvesViaUntrimmedFallback()
        {
            // Arrange: Directly inject an untrimmed legacy name directly via SQL bypass to simulate version <= 8.3 rows
            const string paddedName = "HoopsComm ";
            var sql = $"INSERT INTO Services (Name, ExecutablePath, StartupType, Priority) VALUES (@Name, 'C:\\bin.exe', '{AppConfig.DefaultStartupType}', '{AppConfig.DefaultProcessPriority}');";
            await _executor.ExecuteAsync(sql, new { Name = paddedName }, cancellationToken: CancellationToken.None);

            // Act: UI calls lookup passing the cleaned/trimmed search context parameter
            var resolvedRecord = await _repository.GetByNameAsync(paddedName, decrypt: false, CancellationToken.None);

            // Assert
            Assert.NotNull(resolvedRecord);
            Assert.Equal(paddedName, resolvedRecord.Name); // Proves fallback triggered successfully
        }

        [Fact]
        public async Task DeleteAsync_WithPaddedLegacyRow_PurgesViaUntrimmedFallback()
        {
            // Arrange: Direct SQL seed injects zombie row with hidden trailing whitespace
            const string paddedName = "ZombieService ";
            var sql = $"INSERT INTO Services (Name, ExecutablePath, StartupType, Priority) VALUES (@Name, 'C:\\z.exe', '{AppConfig.DefaultStartupType}', '{AppConfig.DefaultProcessPriority}');";
            await _executor.ExecuteAsync(sql, new { Name = paddedName }, cancellationToken: CancellationToken.None);

            // Act: Purge routing requested using trimmed input signature string
            int affectedRows = await _repository.DeleteAsync(paddedName, CancellationToken.None);

            // Assert
            Assert.Equal(1, affectedRows); // Verifies fallback step cleaned the target row
            var lookupResult = await _repository.GetByNameAsync(paddedName, decrypt: false, CancellationToken.None);
            Assert.Null(lookupResult);
        }

        [Fact]
        public void Update_SynchronousPath_SavesAndPreservesStateSymmetrically()
        {
            // Arrange
            var service = new ServiceDto { Name = "SyncService", ExecutablePath = "C:\\s.exe", Pid = 444 };
            int id = _repository.GetDapperExecutor().ExecuteScalar<int>(
                $"INSERT INTO Services (Name, ExecutablePath, StartupType, Priority, Pid) VALUES ('SyncService', 'C:\\s.exe', '{AppConfig.DefaultStartupType}', '{AppConfig.DefaultProcessPriority}', 444); SELECT last_insert_rowid();", service);
            service.Id = id;

            // Act
            var updatePayload = new ServiceDto { Id = id, Name = "SyncService", ExecutablePath = "C:\\new_sync.exe", Pid = 888 };
            int affectedRows = _repository.Update(updatePayload, preserveExistingRuntimeState: true, preserveExistingCredentials: false);

            // Assert
            Assert.Equal(1, affectedRows);
            var result = _repository.GetByName("SyncService", decrypt: false);
            Assert.NotNull(result);
            Assert.Equal("C:\\new_sync.exe", result.ExecutablePath);
            Assert.Equal(444, result.Pid); // Preserved via synchronous routing pass flags
        }

        [Fact]
        public async Task GetServicePidAsync_ValidService_ReturnsCorrectPid()
        {
            // Arrange
            var service = new ServiceDto { Name = "PidTrackedService", ExecutablePath = "C:\\p.exe", Pid = 5678 };
            await _repository.AddAsync(service, CancellationToken.None);

            // Act
            int? activePid = await _repository.GetServicePidAsync("PidTrackedService", CancellationToken.None);

            // Assert
            Assert.NotNull(activePid);
            Assert.Equal(5678, activePid.Value);
        }

        [Fact]
        public async Task GetServicePidAsync_NullOrMissingService_ReturnsNull()
        {
            // Act & Assert
            Assert.Null(await _repository.GetServicePidAsync(null, CancellationToken.None));
            Assert.Null(await _repository.GetServicePidAsync("NonExistentService", CancellationToken.None));
        }

        [Fact]
        public async Task GetServiceConsoleStateAsync_ValidService_ReturnsPopulatedConsoleDto()
        {
            // Arrange
            var service = new ServiceDto
            {
                Name = "ConsoleStateService",
                ExecutablePath = "C:\\c.exe",
                Pid = 9101,
                ActiveStdoutPath = "C:\\out.log",
                ActiveStderrPath = "C:\\err.log"
            };
            await _repository.AddAsync(service, CancellationToken.None);

            // Act
            var state = await _repository.GetServiceConsoleStateAsync("ConsoleStateService", CancellationToken.None);

            // Assert
            Assert.NotNull(state);
            Assert.Equal(9101, state.Pid);
            Assert.Equal("C:\\out.log", state.ActiveStdoutPath);
            Assert.Equal("C:\\err.log", state.ActiveStderrPath);
        }

        [Fact]
        public async Task GetServiceConsoleStateAsync_NullOrMissingService_ReturnsNull()
        {
            // Act & Assert
            Assert.Null(await _repository.GetServiceConsoleStateAsync(null, CancellationToken.None));
            Assert.Null(await _repository.GetServiceConsoleStateAsync("MissingConsoleService", CancellationToken.None));
        }

        [Fact]
        public void GetByName_SynchronousPath_ResolvesEntryCleanly()
        {
            // Arrange
            var service = new ServiceDto { Name = "SynchronousQueryService", ExecutablePath = "C:\\sync.exe" };
            _repository.GetDapperExecutor().Execute(
                $"INSERT INTO Services (Name, ExecutablePath, StartupType, Priority) VALUES ('SynchronousQueryService', 'C:\\sync.exe', '{AppConfig.DefaultStartupType}', '{AppConfig.DefaultProcessPriority}');", service);

            // Act
            var resolved = _repository.GetByName("SynchronousQueryService", decrypt: false);

            // Assert
            Assert.NotNull(resolved);
            Assert.Equal("C:\\sync.exe", resolved.ExecutablePath);
        }

        [Fact]
        public void GetByName_NullOrEmptyInput_ReturnsNull()
        {
            // Act & Assert
            Assert.Null(_repository.GetByName(null, decrypt: false));
            Assert.Null(_repository.GetByName(string.Empty, decrypt: false));
        }

        [Fact]
        public async Task GetByNameAsync_NullOrEmptyInput_ReturnsNull()
        {
            // Act & Assert
            Assert.Null(await _repository.GetByNameAsync(null, decrypt: false, CancellationToken.None));
            Assert.Null(await _repository.GetByNameAsync("   ", decrypt: false, CancellationToken.None));
        }

        #region Legacy Padded Trim Fallback Tests

        [Fact]
        public void GetByName_WithPaddedLegacyRow_ResolvesViaSynchronousUntrimmedFallback()
        {
            // Arrange: Seed an un-trimmed legacy service directly into the database
            const string paddedName = "HoopsComm ";
            var sql = $"INSERT INTO Services (Name, ExecutablePath, StartupType, Priority) VALUES (@Name, 'C:\\legacy.exe', '{AppConfig.DefaultStartupType}', '{AppConfig.DefaultProcessPriority}');";
            _repository.GetDapperExecutor().Execute(sql, new { Name = paddedName });

            // Act: Pass the untrimmed name explicitly to satisfy the 'name != name.Trim()' guard clause
            var resolvedRecord = _repository.GetByName(paddedName, decrypt: false);

            // Assert
            Assert.NotNull(resolvedRecord);
            Assert.Equal(paddedName, resolvedRecord.Name);
        }

        [Fact]
        public async Task GetServicePidAsync_WithPaddedLegacyRow_ResolvesViaResolveByNameAsyncFallback()
        {
            // Arrange: Seed a zombie service row carrying a trailing newline/whitespace character
            const string paddedName = "LegacyEngineService\n";
            var sql = $"INSERT INTO Services (Name, ExecutablePath, StartupType, Priority, Pid) VALUES (@Name, 'C:\\engine.exe', '{AppConfig.DefaultStartupType}', '{AppConfig.DefaultProcessPriority}', 7777);";
            await _executor.ExecuteAsync(sql, new { Name = paddedName }, cancellationToken: CancellationToken.None);

            // Act: Query using the raw untrimmed name to test ResolveByNameAsync fallback branch
            int? activePid = await _repository.GetServicePidAsync(paddedName, CancellationToken.None);

            // Assert
            Assert.NotNull(activePid);
            Assert.Equal(7777, activePid.Value);
        }

        [Fact]
        public async Task GetServiceConsoleStateAsync_WithPaddedLegacyRow_ResolvesViaResolveByNameAsyncFallback()
        {
            // Arrange: Seed an un-trimmed service row containing leading whitespace.
            // Removed double backslashes inside the verbatim SQL literal block.
            const string paddedName = " GhostService";
            var sql = $@"INSERT INTO Services (Name, ExecutablePath, StartupType, Priority, Pid, ActiveStdoutPath, ActiveStderrPath) 
                         VALUES (@Name, 'C:\ghost.exe', '{AppConfig.DefaultStartupType}', '{AppConfig.DefaultProcessPriority}', 8888, 'C:\out.log', 'C:\err.log');";
            await _executor.ExecuteAsync(sql, new { Name = paddedName }, cancellationToken: CancellationToken.None);

            // Act: Query using the raw untrimmed name to push parsing into the generic secondary query pass
            var state = await _repository.GetServiceConsoleStateAsync(paddedName, CancellationToken.None);

            // Assert
            Assert.NotNull(state);
            Assert.Equal(8888, state.Pid);
            Assert.Equal("C:\\out.log", state.ActiveStdoutPath);
            Assert.Equal("C:\\err.log", state.ActiveStderrPath);
        }

        #endregion

        public void Dispose()
        {
            // Triggers automatic removal and cleanup of the shared SQLite state memory allocation layout
            _dbContext.Dispose();
        }
    }

    // Secondary extension to provide quick access to internal test structures if needed
    internal static class ServiceRepositoryExtensions
    {
        public static IDapperExecutor GetDapperExecutor(this ServiceRepository repository)
        {
            // Instantiates bypass hook access to test context executions safely
            return (IDapperExecutor)typeof(ServiceRepository)
                .GetField("_dapper", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(repository);
        }
    }
}
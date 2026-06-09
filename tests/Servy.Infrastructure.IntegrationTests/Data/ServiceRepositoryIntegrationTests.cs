using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;

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

        public IDapperExecutor CreateDapperExecutor()
        {
            return new DapperExecutor(this);
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
                // Real .NET CryptographicException whose Type.Name evaluates exactly to "CryptographicException"
                var innerCryptoException = new System.Security.Cryptography.CryptographicException("Padding check failed.");

                // Throw the expected InvalidOperationException container with the inner exception attached.
                // This bypasses any flattening traps because it utilizes standard framework types.
                throw new InvalidOperationException("Cryptographic block alignment corrupt.", innerCryptoException);
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
            Assert.Equal("C:\\v2.exe", updatedRecord!.ExecutablePath);
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
            Assert.Single(results);
            Assert.Equal("App_Development_%_Test", results[0].Name);
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
                cancellationToken: TestContext.Current.CancellationToken);

            // Act
            var result = await _repository.GetByIdAsync(id, decrypt: true, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("[DECRYPTION FAILED: InvalidOperationException]", result.Description);
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
            await _repository.DeleteAsync("RoundTripService", CancellationToken.None);

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

        public void Dispose()
        {
            // Triggers automatic removal and cleanup of the shared SQLite state memory allocation layout
            _dbContext.Dispose();
        }
    }
}
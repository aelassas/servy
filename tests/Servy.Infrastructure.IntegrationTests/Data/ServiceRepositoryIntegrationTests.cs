using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using System.Data.Common;
using System.Data.SQLite;

namespace Servy.Infrastructure.IntegrationTests.Data
{
    [Collection(DatabaseTestCollection.Name)]
    public class ServiceRepositoryIntegrationTests : IDisposable
    {
        #region Shared Test Doubles

        /// <summary>
        /// A lightweight, concrete test factory for managing a shared in-memory SQLite state lifespan.
        /// In-memory SQLite databases disappear the moment their connection drops; keeping a master connection
        /// handle open allows DapperExecutor to safely open and close transient connection pools during execution.
        /// </summary>
        private sealed class TestDbContext : IAppDbContext, IDisposable
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
                _masterConnection.Dispose();
            }
        }

        /// <summary>
        /// Fake encryption helper to evaluate secure data-loss and recovery paths deterministically.
        /// </summary>
        private sealed class TestSecureData : ISecureData
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

                if (cipherText == "LEGACY_PAYLOAD")
                {
                    // The legacy policy refuses the payload without examining it, which SafeDecrypt routes to
                    // HandleLegacyBlockedDecryption and which leaves every later sensitive field as ciphertext.
                    throw new SecureDataLegacyBlockedException("Legacy ciphertext refused by policy.");
                }

                return cipherText.StartsWith("SECRET_HASH:", StringComparison.Ordinal)
                    ? cipherText.Substring("SECRET_HASH:".Length)
                    : cipherText;
            }

            public void Dispose()
            {
                /* no-op */
            }
        }

        #endregion

        private readonly TestDbContext _dbContext;
        private readonly DapperExecutor _executor;
        private readonly TestSecureData _secureData;
        private readonly XmlServiceSerializer _xmlSerializer;
        private readonly JsonServiceSerializer _jsonSerializer;
        private readonly ServiceRepository _repository;

        public ServiceRepositoryIntegrationTests()
        {
            _dbContext = new TestDbContext();
            _dbContext.InitializeSchema(); // Applies the full migration chain: tables, and the UNICODE_NOCASE unique index on Name

            _executor = new DapperExecutor(_dbContext);
            _secureData = new TestSecureData();
            _xmlSerializer = new XmlServiceSerializer();
            _jsonSerializer = new JsonServiceSerializer();

            _repository = new ServiceRepository(_executor, _secureData, _xmlSerializer, _jsonSerializer);
        }

        [Fact]
        public async Task UpdateRuntimeStateAsync_WritesTheRuntimeColumnsAndLeavesTheConfigurationAlone()
        {
            // Arrange
            var service = new ServiceDto { Name = "RuntimeOnly", ExecutablePath = "C:\\app.exe", Parameters = "--secret", PreviousStopTimeout = 15 };
            await _repository.UpsertAsync(service, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Act
            var updated = await _repository.UpdateRuntimeStateAsync("RuntimeOnly", new ServiceRuntimeStateDto
            {
                Pid = 4321,
                ActiveStdoutPath = "C:\\out.log",
                ActiveStderrPath = "C:\\err.log",
                UpdatePreviousStopTimeout = false,
                PreviousStopTimeout = 99,
            }, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, updated);
            var row = await _repository.GetByNameAsync("RuntimeOnly", decrypt: true, TestContext.Current.CancellationToken);
            Assert.NotNull(row);
            Assert.Equal(4321, row!.Pid);
            Assert.Equal("C:\\out.log", row.ActiveStdoutPath);
            Assert.Equal("C:\\err.log", row.ActiveStderrPath);
            Assert.Equal(15, row.PreviousStopTimeout); // not asked to change
            Assert.Equal("C:\\app.exe", row.ExecutablePath);
            Assert.Equal("--secret", row.Parameters);

            // Act: clear the state and set the previous stop timeout
            await _repository.UpdateRuntimeStateAsync("runtimeonly", new ServiceRuntimeStateDto { UpdatePreviousStopTimeout = true, PreviousStopTimeout = 30 }, TestContext.Current.CancellationToken);

            // Assert: the name is matched case-insensitively, like every other lookup
            row = await _repository.GetByNameAsync("RuntimeOnly", decrypt: true, TestContext.Current.CancellationToken);
            Assert.Null(row!.Pid);
            Assert.Null(row.ActiveStdoutPath);
            Assert.Null(row.ActiveStderrPath);
            Assert.Equal(30, row.PreviousStopTimeout);
        }

        [Fact]
        public async Task UpdateRuntimeStateAsync_UnknownService_UpdatesNothing()
        {
            // Act
            var updated = await _repository.UpdateRuntimeStateAsync("NoSuchService", new ServiceRuntimeStateDto { Pid = 1 }, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0, updated);
        }

        [Fact]
        public async Task RestartAttempts_RoundTrip_AndSurviveAConfigurationUpdate()
        {
            // Arrange
            var service = new ServiceDto { Name = "Counted", ExecutablePath = "C:\\app.exe" };
            await _repository.UpsertAsync(service, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken);
            var when = new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc);

            // Act
            var before = await _repository.GetRestartAttemptsAsync("Counted", TestContext.Current.CancellationToken);
            var written = await _repository.UpdateRestartAttemptsAsync("Counted", 2, when, TestContext.Current.CancellationToken);
            var after = await _repository.GetRestartAttemptsAsync("COUNTED", TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(before);
            Assert.Equal(0, before!.Attempts);
            Assert.Null(before.UpdatedAtUtc);
            Assert.Equal(1, written);
            Assert.Equal(2, after!.Attempts);
            Assert.Equal(when, after.UpdatedAtUtc);

            // Act: an edit in the desktop app or the Manager upserts the configuration and preserves runtime state
            await _repository.UpsertAsync(new ServiceDto { Name = "Counted", ExecutablePath = "C:\\new.exe" },
                preserveExistingRuntimeState: true, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Assert: the quota is not reset by a configuration change
            var kept = await _repository.GetRestartAttemptsAsync("Counted", TestContext.Current.CancellationToken);
            Assert.Equal(2, kept!.Attempts);
            Assert.Equal(when, kept.UpdatedAtUtc);
        }

        [Fact]
        public async Task GetRestartAttemptsAsync_UnknownService_ReturnsNull()
        {
            Assert.Null(await _repository.GetRestartAttemptsAsync("NoSuchService", TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task UpsertAsync_ModifiesRecord_HonoringRuntimeBypassStates()
        {
            // Arrange
            var service = new ServiceDto { Name = "MutableService", ExecutablePath = "C:\\exe.exe", Pid = 1234 };
            await _repository.UpsertAsync(service, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Act - Request updating fields but protecting existing transient state columns
            var modification = new ServiceDto { Name = "MutableService", ExecutablePath = "C:\\updated.exe", Pid = 9999 };
            await _repository.UpsertAsync(modification, preserveExistingRuntimeState: true, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Assert
            var result = await _repository.GetByNameAsync("MutableService", decrypt: true, TestContext.Current.CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("C:\\updated.exe", result.ExecutablePath);
            Assert.Equal(1234, result.Pid); // Preserved
        }

        [Fact]
        public void Query_With_Unregistered_Collation_Throws_SQLiteException()
        {
            SQLiteConnection.ClearAllPools();

            using (var conn = new SQLiteConnection("Data Source=:memory:"))
            {
                conn.Open();

                // Simulates what happens in production when UNICODE_NOCASE auto-discovery fails
                var ex = Assert.Throws<SQLiteException>(() =>
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT 1 WHERE 'a' = 'A' COLLATE UNregistered_COLLATION;";
                        cmd.ExecuteNonQuery();
                    }
                });

                Assert.Contains("no such collation sequence", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void AppDbContext_DeclaresStaticInitializer_RegisteringUnicodeNoCaseCollation()
        {
            // The collation is registered process-wide at three sites (SQLiteDbInitializer,
            // AppDbContext's static constructor, DatabaseInitializer) and registration cannot be undone,
            // so no query-based test can attribute the registration to this one. Assert the two things a
            // consolidation of those three sites would break, which do not depend on execution order:

            // 1. AppDbContext declares a type initializer. It holds no static fields, so removing the
            //    explicit static constructor that fixed #5631 leaves TypeInitializer null.
            Assert.NotNull(typeof(AppDbContext).TypeInitializer);

            // 2. The collation type it registers still advertises the name the schema's unique index uses.
            var functionAttribute = typeof(UnicodeNoCaseCollation)
                .GetCustomAttributes(typeof(SQLiteFunctionAttribute), inherit: false)
                .Cast<SQLiteFunctionAttribute>()
                .SingleOrDefault();

            Assert.NotNull(functionAttribute);
            Assert.Equal("UNICODE_NOCASE", functionAttribute.Name);
            Assert.Equal(FunctionType.Collation, functionAttribute.FuncType);
        }

        [Fact]
        public void AppDbContext_Connection_RunsUnicodeNoCaseQuery_WhenCollationIsRegistered()
        {
            // NOTE: this test cannot guard the static constructor, and its name no longer claims to.
            // This class's own fixture runs SQLiteDbInitializer.Initialize before every test, whose first
            // statement registers UNICODE_NOCASE process-wide, so the query below succeeds even with
            // AppDbContext's registration deleted. What it does verify is that a connection handed out by
            // AppDbContext resolves the collation - see the sibling test above for the constructor itself.
            var context = new AppDbContext("Data Source=:memory:");

            using (var connection = context.CreateConnection())
            {
                connection.Open();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT 1 WHERE 'test' = 'TEST' COLLATE UNICODE_NOCASE;";
                    var result = cmd.ExecuteScalar();

                    Assert.Equal(1L, result);
                }
            }
        }

        [Fact]
        public async Task UpsertAsync_OnPooledConnectionAfterInitializer_SuccessfullySavesWithoutCollationError()
        {
            // Arrange
            var service = new ServiceDto
            {
                Name = $"CollationTest_{Guid.NewGuid():N}",
                ExecutablePath = @"C:\Windows\System32\cmd.exe",
                RunAsLocalSystem = true
            };

            var id = await _repository.UpsertAsync(service, preserveExistingRuntimeState: false, preserveExistingCredentials: false, cancellationToken: TestContext.Current.CancellationToken);

            // Clear connection pools to force next connection request to take a fresh handle from pool
            SQLiteConnection.ClearAllPools();

            // Act
            service.Pid = 1234;
            var upsertedId = await _repository.UpsertAsync(
                service,
                preserveExistingRuntimeState: false,
                preserveExistingCredentials: false,
                cancellationToken: TestContext.Current.CancellationToken);

            var updated = await _repository.GetByNameAsync(service.Name, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(id, upsertedId);
            Assert.NotNull(updated);
            Assert.Equal(1234, updated.Pid);
        }

        [Fact]
        public async Task UpsertAsync_OnConflict_ExecutesInPlaceUpdate()
        {
            // Arrange
            var service1 = new ServiceDto { Name = "ConflictService", ExecutablePath = "C:\\v1.exe" };
            var service2 = new ServiceDto { Name = "conflictservice", ExecutablePath = "C:\\v2.exe" }; // Trips idx_services_name_unique under COLLATE UNICODE_NOCASE

            int id1 = await _repository.UpsertAsync(service1, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Act
            int id2 = await _repository.UpsertAsync(service2, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(id1, id2); // Same record identity targeted
            var updatedRecord = await _repository.GetByNameAsync("ConflictService", decrypt: true, TestContext.Current.CancellationToken);
            Assert.Equal("C:\\v2.exe", updatedRecord!.ExecutablePath);
        }

        [Fact]
        public async Task UpsertAsync_WithPreserveExistingCredentialsTrue_PreservesCredentialFieldsOnConflict()
        {
            // Arrange
            var originalService = new ServiceDto
            {
                Name = "SingleUpsertCredService",
                ExecutablePath = "C:\\upsert_v1.exe",
                RunAsLocalSystem = false,
                UserAccount = "Domain\\UpsertUser",
                Password = "UpsertSecretPass123"
            };
            int originalId = await _repository.UpsertAsync(originalService, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Act - Upsert on conflicting name with changed credential properties
            var incomingPayload = new ServiceDto
            {
                Name = "singleupsertcredservice", // Collation test casing match
                ExecutablePath = "C:\\upsert_v2.exe",
                RunAsLocalSystem = true,
                UserAccount = "Domain\\BadUser",
                Password = "BadPassword"
            };
            int upsertedId = await _repository.UpsertAsync(incomingPayload, preserveExistingRuntimeState: false, preserveExistingCredentials: true, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(originalId, upsertedId);

            // Verify raw cipher text retention
            var rawRecord = await _repository.GetByNameAsync("SingleUpsertCredService", decrypt: false, TestContext.Current.CancellationToken);
            Assert.NotNull(rawRecord);
            Assert.False(rawRecord.RunAsLocalSystem);
            Assert.Equal("Domain\\UpsertUser", rawRecord.UserAccount);
            Assert.Equal("SECRET_HASH:UpsertSecretPass123", rawRecord.Password);

            // Verify decrypted state
            var decryptedRecord = await _repository.GetByNameAsync("SingleUpsertCredService", decrypt: true, TestContext.Current.CancellationToken);
            Assert.NotNull(decryptedRecord);
            Assert.Equal("C:\\upsert_v2.exe", decryptedRecord.ExecutablePath);
            Assert.False(decryptedRecord.RunAsLocalSystem);
            Assert.Equal("Domain\\UpsertUser", decryptedRecord.UserAccount);
            Assert.Equal("UpsertSecretPass123", decryptedRecord.Password);
        }

        [Fact]
        public async Task DeleteAsync_ByNameAndId_RemovesTargetEntries()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;

            // --- 1. Verify "ByName" deletion path ---
            var serviceByName = new ServiceDto { Name = "KillMeByName", ExecutablePath = "kill_name.exe" };
            await _repository.UpsertAsync(serviceByName, preserveExistingRuntimeState: false, preserveExistingCredentials: false, cancellationToken);

            // Act
            int deletedByNameCount = await _repository.DeleteAsync("KillMeByName", cancellationToken);

            // Assert
            Assert.Equal(1, deletedByNameCount);
            var searchByName = await _repository.GetByNameAsync("KillMeByName", decrypt: true, cancellationToken);
            Assert.Null(searchByName);

            // --- 2. Verify "ById" deletion path ---
            // Arrange
            var serviceById = new ServiceDto { Name = "KillMeById", ExecutablePath = "kill_id.exe" };
            int idToDelete = await _repository.UpsertAsync(serviceById, preserveExistingRuntimeState: false, preserveExistingCredentials: false, cancellationToken);

            // Act
            // Act against the ID-based delete API overload to cover the other half of the name's promise
            int deletedByIdCount = await _repository.DeleteAsync(idToDelete, cancellationToken);

            // Assert
            Assert.Equal(1, deletedByIdCount);
            var searchById = await _repository.GetByNameAsync("KillMeById", decrypt: true, cancellationToken);
            Assert.Null(searchById);
        }

        [Fact]
        public async Task SearchAsync_UsingKeywords_EvaluatesWildcardAndSqlEscapeMatchers()
        {
            // Arrange
            var cancellationToken = TestContext.Current.CancellationToken;

            // 1. The target record containing literal % and _ characters
            await _repository.UpsertAsync(new ServiceDto { Name = "App_Development_%_Test", ExecutablePath = "a.exe" }, preserveExistingRuntimeState: false, preserveExistingCredentials: false, cancellationToken);

            // 2. Decoy A: Matches if '%' in "development_%" is treated as a wildcard instead of a literal '%'
            await _repository.UpsertAsync(new ServiceDto { Name = "App_Development_XYZ_Test", ExecutablePath = "b.exe" }, preserveExistingRuntimeState: false, preserveExistingCredentials: false, cancellationToken);

            // 3. Decoy B: Matches if '_' in "development" is treated as a wildcard (e.g. matching "developmenX")
            await _repository.UpsertAsync(new ServiceDto { Name = "App_DevelopmenX_%_Test", ExecutablePath = "c.exe" }, preserveExistingRuntimeState: false, preserveExistingCredentials: false, cancellationToken);

            // Act - Search targeting literal "_" and "%" characters using ESCAPE configurations
            var results = (await _repository.SearchAsync("development_%", decrypt: true, cancellationToken)).ToList();

            // Assert
            // If escaping is broken, "development_%" will match "App_Development_XYZ_Test" (wildcard %)
            // and return multiple results. Asserting single-result delivery ensures strict literal evaluation.
            Assert.Single(results);
            Assert.Equal("App_Development_%_Test", results[0].Name);
        }

        [Fact]
        public async Task GetByNameAsync_PoisonDataEncountered_QuarantinesRecordAndPadsTelemetry()
        {
            // Arrange
            // Every sensitive field carries a distinct value, so the quarantine scrub of each one
            // is falsifiable: a field left unscrubbed comes back holding its own content.
            var service = new ServiceDto
            {
                Name = "PoisonRecord",
                ExecutablePath = "poison.exe",
                Description = "Original description",
                Parameters = "SentinelParameters",
                FailureProgramParameters = "SentinelFailureProgramParameters",
                PreLaunchParameters = "SentinelPreLaunchParameters",
                PostLaunchParameters = "SentinelPostLaunchParameters",
                Password = "SentinelPassword",
                EnvironmentVariables = "SentinelEnvironmentVariables",
                PreLaunchEnvironmentVariables = "SentinelPreLaunchEnvironmentVariables",
                PreStopParameters = "SentinelPreStopParameters",
                PostStopParameters = "SentinelPostStopParameters",
            };
            int id = await _repository.UpsertAsync(service, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Manually corrupt data payload in database directly via executor bypass
            await _executor.ExecuteAsync(
                $"UPDATE {SqlConstants.ServicesTableName} SET Parameters = 'POISON_PAYLOAD' WHERE Id = @Id",
                new { Id = id },
                cancellationToken: TestContext.Current.CancellationToken);

            // Act
            var result = await _repository.GetByNameAsync("PoisonRecord", decrypt: true, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("[DECRYPTION FAILED: CryptographicException]", result.Description);

            // One corrupt field quarantines the whole record: all nine sensitive fields are scrubbed.
            Assert.Null(result.Parameters);
            Assert.Null(result.FailureProgramParameters);
            Assert.Null(result.PreLaunchParameters);
            Assert.Null(result.PostLaunchParameters);
            Assert.Null(result.Password);
            Assert.Null(result.EnvironmentVariables);
            Assert.Null(result.PreLaunchEnvironmentVariables);
            Assert.Null(result.PreStopParameters);
            Assert.Null(result.PostStopParameters);
        }

        [Fact]
        public async Task ExportAndUpsert_XmlRoundTrip_PreservesConfigFidelity()
        {
            // Arrange
            var service = new ServiceDto { Name = "RoundTripXmlService", ExecutablePath = "round_xml.exe", Description = "SerializeXml" };
            await _repository.UpsertAsync(service, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Act - Export configuration out to XML schema format
            string xmlData = await _repository.ExportXmlAsync("RoundTripXmlService", TestContext.Current.CancellationToken);
            Assert.NotEmpty(xmlData);

            // Purge database record to prepare for clean restoration validation path
            var deleteResult = await _repository.DeleteAsync("RoundTripXmlService", TestContext.Current.CancellationToken);
            Assert.Equal(1, deleteResult);
            Assert.Null(await _repository.GetByNameAsync("RoundTripXmlService", decrypt: true, TestContext.Current.CancellationToken));

            // Act - Deserialize the XML record and write it back the way the import paths do, keeping runtime state and credentials
            var imported = _xmlSerializer.Deserialize(xmlData);
            Assert.NotNull(imported);
            await _repository.UpsertAsync(imported, preserveExistingRuntimeState: true, preserveExistingCredentials: true, TestContext.Current.CancellationToken);

            // Assert - Verify complete field mapping data fidelity (#3041 Fix)

            var recovered = await _repository.GetByNameAsync("RoundTripXmlService", decrypt: true, TestContext.Current.CancellationToken);
            Assert.NotNull(recovered);
            Assert.Equal("SerializeXml", recovered.Description);
            Assert.Equal("round_xml.exe", recovered.ExecutablePath);
        }

        [Fact]
        public async Task ExportAndUpsert_JsonRoundTrip_PreservesConfigFidelity()
        {
            // Arrange
            var service = new ServiceDto { Name = "RoundTripJsonService", ExecutablePath = "round_json.exe", Description = "SerializeJson" };
            await _repository.UpsertAsync(service, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Act - Export configuration out to JSON format layout specifications
            string jsonData = await _repository.ExportJsonAsync("RoundTripJsonService", TestContext.Current.CancellationToken);
            Assert.NotEmpty(jsonData);

            // Purge database record to prepare for clean restoration validation path
            var deleteResult = await _repository.DeleteAsync("RoundTripJsonService", TestContext.Current.CancellationToken);
            Assert.Equal(1, deleteResult);
            Assert.Null(await _repository.GetByNameAsync("RoundTripJsonService", decrypt: true, TestContext.Current.CancellationToken));

            // Act - Deserialize the JSON record and write it back the way the import paths do, keeping runtime state and credentials
            var imported = _jsonSerializer.Deserialize(jsonData);
            Assert.NotNull(imported);
            await _repository.UpsertAsync(imported, preserveExistingRuntimeState: true, preserveExistingCredentials: true, TestContext.Current.CancellationToken);

            // Assert - Verify complete field mapping data fidelity (#3041 Fix)

            var recovered = await _repository.GetByNameAsync("RoundTripJsonService", decrypt: true, TestContext.Current.CancellationToken);
            Assert.NotNull(recovered);
            Assert.Equal("SerializeJson", recovered.Description);
            Assert.Equal("round_json.exe", recovered.ExecutablePath);
        }

        [Fact]
        public async Task GetByNameAsync_WithPaddedLegacyRow_ResolvesViaUntrimmedFallback()
        {
            // Arrange: Directly inject an untrimmed legacy name directly via SQL bypass to simulate version <= 8.3 rows
            const string paddedName = "HoopsComm ";
            var sql = $"INSERT INTO {SqlConstants.ServicesTableName} (Name, ExecutablePath, StartupType, Priority) VALUES (@Name, 'C:\\bin.exe', '{AppConfig.DefaultStartupType}', '{AppConfig.DefaultProcessPriority}');";
            await _executor.ExecuteAsync(sql, new { Name = paddedName }, cancellationToken: TestContext.Current.CancellationToken);

            // Act: caller looks up using the raw untrimmed legacy name to exercise the untrimmed fallback path
            var resolvedRecord = await _repository.GetByNameAsync(paddedName, decrypt: false, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(resolvedRecord);
            Assert.Equal(paddedName, resolvedRecord.Name); // Proves fallback triggered successfully
        }

        [Fact]
        public async Task GetByNameAsync_NonAsciiCasingDifference_ResolvesViaUnicodeNoCaseCollation()
        {
            // Arrange - stored through the repository, i.e. on a connection other than the initializer's.
            var ct = TestContext.Current.CancellationToken;
            await _repository.UpsertAsync(new ServiceDto { Name = "ÖffnenService", ExecutablePath = "C:\\o.exe" }, preserveExistingRuntimeState: false, preserveExistingCredentials: false, ct);

            // Act
            var found = await _repository.GetByNameAsync("öffnenservice", decrypt: false, ct);

            // Assert - built-in NOCASE cannot fold 'Ö'/'ö'; only UNICODE_NOCASE can.
            Assert.NotNull(found);
            Assert.Equal("ÖffnenService", found.Name);
        }

        [Fact]
        public async Task DeleteAsync_WithPaddedLegacyRow_PurgesViaUntrimmedFallback()
        {
            // Arrange: Direct SQL seed injects zombie row with hidden trailing whitespace
            const string paddedName = "ZombieService ";
            var sql = $"INSERT INTO {SqlConstants.ServicesTableName} (Name, ExecutablePath, StartupType, Priority) VALUES (@Name, 'C:\\z.exe', '{AppConfig.DefaultStartupType}', '{AppConfig.DefaultProcessPriority}');";
            await _executor.ExecuteAsync(sql, new { Name = paddedName }, cancellationToken: TestContext.Current.CancellationToken);

            // Act: Purge routing requested using the raw untrimmed legacy name to exercise the untrimmed fallback path
            int affectedRows = await _repository.DeleteAsync(paddedName, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, affectedRows); // Verifies fallback step cleaned the target row
            var lookupResult = await _repository.GetByNameAsync(paddedName, decrypt: false, TestContext.Current.CancellationToken);
            Assert.Null(lookupResult);
        }

        [Fact]
        public async Task GetServicePidAsync_ValidService_ReturnsCorrectPid()
        {
            // Arrange
            var service = new ServiceDto { Name = "PidTrackedService", ExecutablePath = "C:\\p.exe", Pid = 5678 };
            await _repository.UpsertAsync(service, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Act
            int? activePid = await _repository.GetServicePidAsync("PidTrackedService", TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(activePid);
            Assert.Equal(5678, activePid.Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public async Task GetServicePidAsync_NullOrEmptyInput_ReturnsNull(string? input)
        {
            // Arrange & Act & Assert
            Assert.Null(await _repository.GetServicePidAsync(input, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetServicePidAsync_MissingService_ReturnsNull()
        {
            // Arrange & Act & Assert
            Assert.Null(await _repository.GetServicePidAsync("NonExistentService", TestContext.Current.CancellationToken));
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
            await _repository.UpsertAsync(service, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Act
            var state = await _repository.GetServiceConsoleStateAsync("ConsoleStateService", TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(state);
            Assert.Equal(9101, state.Pid);
            Assert.Equal("C:\\out.log", state.ActiveStdoutPath);
            Assert.Equal("C:\\err.log", state.ActiveStderrPath);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public async Task GetServiceConsoleStateAsync_NullOrEmptyInput_ReturnsNull(string? input)
        {
            // Arrange & Act & Assert
            Assert.Null(await _repository.GetServiceConsoleStateAsync(input, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task GetServiceConsoleStateAsync_MissingService_ReturnsNull()
        {
            // Arrange & Act & Assert
            Assert.Null(await _repository.GetServiceConsoleStateAsync("MissingConsoleService", TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("    ")]
        public async Task GetByNameAsync_NullOrEmptyInput_ReturnsNull(string? input)
        {
            // Arrange & Act & Assert
            Assert.Null(await _repository.GetByNameAsync(input, decrypt: false, TestContext.Current.CancellationToken));
        }

        #region Undecryptable Row Write Guard Tests (#7334)

        private async Task AddPoisonedAsync(string name)
        {
            var id = await _repository.UpsertAsync(new ServiceDto
            {
                Name = name,
                ExecutablePath = "poison.exe",
                Description = "Original description",
                Parameters = "SentinelParameters",
                Password = "SentinelPassword",
                EnvironmentVariables = "A=1",
            }, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // One sensitive field the current key cannot decrypt, as after aes_key.dat was corrupted or replaced
            await _executor.ExecuteAsync(
                $"UPDATE {SqlConstants.ServicesTableName} SET Parameters = 'POISON_PAYLOAD' WHERE Id = @Id",
                new { Id = id },
                cancellationToken: TestContext.Current.CancellationToken);
        }

        private static void AssertRowUntouched(ServiceDto? raw)
        {
            Assert.NotNull(raw);
            Assert.Equal("Original description", raw!.Description);
            Assert.Equal("POISON_PAYLOAD", raw.Parameters);
            Assert.Equal("SECRET_HASH:SentinelPassword", raw.Password);
            Assert.Equal("SECRET_HASH:A=1", raw.EnvironmentVariables);
            Assert.Equal("poison.exe", raw.ExecutablePath);
        }

        [Fact]
        public async Task UpsertAsync_ServiceReadFromAnUndecryptableRow_IsRefusedAndTheRowIsLeftAsItIs()
        {
            // Arrange: the service as the UI gets it - secrets cleared, description marked
            var ct = TestContext.Current.CancellationToken;
            await AddPoisonedAsync("PoisonUpdate");
            var read = await _repository.GetByNameAsync("PoisonUpdate", decrypt: true, ct);
            Assert.True(DecryptionFailureMarker.HasDecryptionFailure(read));
            read!.ExecutablePath = "changed.exe";

            // Act
            var ex = await Assert.ThrowsAsync<ServiceDecryptionFailedException>(() => _repository.UpsertAsync(read, false, false, ct));

            // Assert
            Assert.Equal("PoisonUpdate", ex.ServiceName);
            Assert.Contains("aes_key.dat", ex.Message);
            AssertRowUntouched(await _repository.GetByNameAsync("PoisonUpdate", decrypt: false, ct));
        }

        [Fact]
        public async Task UpsertAsync_RowWhoseStoredDescriptionCarriesAMarkerButDecrypts_SucceedsAndStripsTheMarker()
        {
            // Arrange: the shape a Servy v9.5 save persisted (#5186) - the marker is in the STORED description and
            // the sensitive columns are blank, so this row decrypts cleanly today. Deciding from the description
            // text made every install, update and import of it fail for good with an aes_key.dat error that is
            // not true, and editing the marker out in the UI did not help because the STORED row was checked (#7348).
            var ct = TestContext.Current.CancellationToken;
            var marked = string.Format(DecryptionFailureMarker.CorruptFormat, "CryptographicException")
                + DecryptionFailureMarker.OriginalDescriptionSeparator + "My app";
            var id = await _repository.UpsertAsync(new ServiceDto
            {
                Name = "LegacyMarked",
                ExecutablePath = "legacy.exe",
                Description = "Original description",
            }, preserveExistingRuntimeState: false, preserveExistingCredentials: false, ct);
            await _executor.ExecuteAsync(
                $"UPDATE {SqlConstants.ServicesTableName} SET Description = @Description WHERE Id = @Id",
                new { Id = id, Description = marked },
                cancellationToken: ct);

            var read = await _repository.GetByNameAsync("LegacyMarked", decrypt: true, ct);
            Assert.Equal(marked, read!.Description);
            Assert.False(DecryptionFailureMarker.HasDecryptionFailure(read));
            read.ExecutablePath = "changed.exe";

            // Act
            await _repository.UpsertAsync(read, false, false, ct);

            // Assert: the write went through, and the save stripped the stored marker as v9.6 to v10.1 did
            var after = await _repository.GetByNameAsync("LegacyMarked", decrypt: true, ct);
            Assert.Equal("changed.exe", after!.ExecutablePath);
            Assert.Equal("My app", after.Description);
            Assert.False(DecryptionFailureMarker.HasDecryptionFailure(after));
        }

        [Fact]
        public async Task UpsertAsync_FreshConfigurationOverAnUndecryptableRow_IsRefusedNamingTheFieldAndTheRowIsLeftAsItIs()
        {
            // Arrange: an import or an install with a complete, valid configuration for the same service
            var ct = TestContext.Current.CancellationToken;
            await AddPoisonedAsync("PoisonImport");
            var incoming = new ServiceDto { Name = "PoisonImport", ExecutablePath = "new.exe", Description = "New", Parameters = "--new" };

            // Act
            var ex = await Assert.ThrowsAsync<ServiceDecryptionFailedException>(() => _repository.UpsertAsync(incoming, true, true, ct));

            // Assert
            Assert.Equal(nameof(ServiceDto.Parameters), ex.FieldName);
            AssertRowUntouched(await _repository.GetByNameAsync("PoisonImport", decrypt: false, ct));
        }

        [Fact]
        public async Task ExportXmlAsync_UndecryptableRow_IsRefusedRatherThanWrittenWithoutItsSensitiveFields()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            await AddPoisonedAsync("PoisonExportXml");

            // Act
            var ex = await Assert.ThrowsAsync<ServiceDecryptionFailedException>(() => _repository.ExportXmlAsync("PoisonExportXml", ct));

            // Assert
            Assert.Equal("PoisonExportXml", ex.ServiceName);
            AssertRowUntouched(await _repository.GetByNameAsync("PoisonExportXml", decrypt: false, ct));
        }

        [Fact]
        public async Task ExportJsonAsync_UndecryptableRow_IsRefusedRatherThanWrittenWithoutItsSensitiveFields()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            await AddPoisonedAsync("PoisonExportJson");

            // Act
            var ex = await Assert.ThrowsAsync<ServiceDecryptionFailedException>(() => _repository.ExportJsonAsync("PoisonExportJson", ct));

            // Assert
            Assert.Equal("PoisonExportJson", ex.ServiceName);
            AssertRowUntouched(await _repository.GetByNameAsync("PoisonExportJson", decrypt: false, ct));
        }

        [Fact]
        public async Task UpsertAsync_HealthyRow_IsStillWritten()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            await _repository.UpsertAsync(new ServiceDto { Name = "HealthyUpsert", ExecutablePath = "ok.exe", Parameters = "--old" }, preserveExistingRuntimeState: false, preserveExistingCredentials: false, ct);

            // Act
            await _repository.UpsertAsync(new ServiceDto { Name = "HealthyUpsert", ExecutablePath = "ok.exe", Parameters = "--new" }, true, true, ct);

            // Assert
            Assert.Equal("--new", (await _repository.GetByNameAsync("HealthyUpsert", decrypt: true, ct))!.Parameters);
        }

        #endregion

        #region Refresh Tick Metadata Write Tests (#7328)

        /// <summary>
        /// Runs what the Manager's refresh tick does under the two-column write: read every row without
        /// decrypting, and when the stored description differs from the one the service control manager
        /// reports, write back only <c>Description</c> and <c>StartupType</c>.
        /// </summary>
        /// <param name="name">The service name the tick syncs.</param>
        /// <param name="scmDescription">The description the service control manager reports.</param>
        /// <param name="scmStartupType">The startup type the service control manager reports.</param>
        /// <param name="ct">A token to monitor for cancellation requests.</param>
        /// <returns>The number of rows the tick wrote.</returns>
        private async Task<int> RunRefreshTickAsync(string name, string scmDescription, int scmStartupType, CancellationToken ct)
        {
            var all = await _repository.GetAllAsync(decrypt: false, ct);
            var dto = all.First(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

            bool drifted = !string.Equals(dto.Description ?? string.Empty, scmDescription, StringComparison.Ordinal)
                || dto.StartupType != scmStartupType;

            return drifted
                ? await _repository.UpdateDescriptionAndStartupTypeAsync(name, scmDescription, scmStartupType, ct)
                : 0;
        }

        [Fact]
        public async Task RefreshTick_LegacyBlockedRecord_RepeatedTicksLeaveEveryCiphertextByteIdentical()
        {
            // Arrange: a record whose Password is legacy ciphertext the policy refuses. Password sits at index 4
            // of SensitiveFields, so DecryptDto stops there and EnvironmentVariables stays v2 ciphertext - the
            // field the old whole-row write-back re-encrypted once per tick (#7328).
            var ct = TestContext.Current.CancellationToken;
            var id = await _repository.UpsertAsync(new ServiceDto
            {
                Name = "LegacyTick",
                ExecutablePath = "legacy.exe",
                Description = "Old description",
                StartupType = 2,
                Parameters = "--port 8080",
                EnvironmentVariables = "A=1",
            }, preserveExistingRuntimeState: false, preserveExistingCredentials: false, ct);
            await _executor.ExecuteAsync(
                $"UPDATE {SqlConstants.ServicesTableName} SET Password = 'LEGACY_PAYLOAD' WHERE Id = @Id",
                new { Id = id },
                cancellationToken: ct);

            var before = await _repository.GetByNameAsync("LegacyTick", decrypt: false, ct);

            // Act: six ticks, the number the issue's reproduction used to show the geometric growth
            var writes = 0;
            for (var tick = 0; tick < 6; tick++)
            {
                writes += await RunRefreshTickAsync("LegacyTick", "From the SCM", 3, ct);
            }

            // Assert: the first tick syncs the two columns and the five after it find no drift left, and no
            // ciphertext ever changed, so nothing can grow a second encryption layer
            Assert.Equal(1, writes);
            var after = await _repository.GetByNameAsync("LegacyTick", decrypt: false, ct);
            Assert.NotNull(after);
            Assert.Equal("From the SCM", after!.Description);
            Assert.Equal(3, after.StartupType);
            Assert.Equal(before!.Parameters, after.Parameters);
            Assert.Equal(before.EnvironmentVariables, after.EnvironmentVariables);
            Assert.Equal("LEGACY_PAYLOAD", after.Password);
            Assert.Equal(before.ExecutablePath, after.ExecutablePath);

            // And the stored value is still one encryption deep: decrypting it returns the plaintext,
            // not another ciphertext
            Assert.Equal("A=1", _secureData.Decrypt(after.EnvironmentVariables!));
        }

        [Fact]
        public async Task RefreshTick_CorruptRecord_LeavesTheSensitiveColumnsAsStoredInsteadOfNulling()
        {
            // Arrange: one field the current key cannot decrypt, as after aes_key.dat was replaced. A read with
            // decrypt: true scrubs all nine sensitive fields, and the old write-back stored those nulls (#7328).
            var ct = TestContext.Current.CancellationToken;
            await AddPoisonedAsync("CorruptTick");
            var scrubbed = await _repository.GetByNameAsync("CorruptTick", decrypt: true, ct);
            Assert.True(DecryptionFailureMarker.HasDecryptionFailure(scrubbed));
            Assert.Null(scrubbed!.EnvironmentVariables);

            // Act
            var writes = await RunRefreshTickAsync("CorruptTick", "From the SCM", 3, ct);

            // Assert: only the two metadata columns moved; the ciphertexts that a restored key could still
            // recover are exactly as they were stored
            Assert.Equal(1, writes);
            var after = await _repository.GetByNameAsync("CorruptTick", decrypt: false, ct);
            Assert.NotNull(after);
            Assert.Equal("From the SCM", after!.Description);
            Assert.Equal(3, after.StartupType);
            Assert.Equal("POISON_PAYLOAD", after.Parameters);
            Assert.Equal("SECRET_HASH:A=1", after.EnvironmentVariables);
            Assert.Equal("SECRET_HASH:SentinelPassword", after.Password);
            Assert.Equal("poison.exe", after.ExecutablePath);
        }

        [Fact]
        public async Task RefreshTick_HealthyRecord_SyncsTheTwoColumnsAndLeavesTheConfigurationAlone()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            await _repository.UpsertAsync(new ServiceDto
            {
                Name = "HealthyTick",
                ExecutablePath = "ok.exe",
                Description = "Old description",
                StartupType = 2,
                Parameters = "--keep",
                Password = "SentinelPassword",
            }, preserveExistingRuntimeState: false, preserveExistingCredentials: false, ct);

            // Act
            var writes = await RunRefreshTickAsync("HealthyTick", "From the SCM", 3, ct);

            // Assert
            Assert.Equal(1, writes);
            var after = await _repository.GetByNameAsync("HealthyTick", decrypt: true, ct);
            Assert.NotNull(after);
            Assert.Equal("From the SCM", after!.Description);
            Assert.Equal(3, after.StartupType);
            Assert.Equal("--keep", after.Parameters);
            Assert.Equal("SentinelPassword", after.Password);
            Assert.Equal("ok.exe", after.ExecutablePath);
        }

        [Fact]
        public async Task UpdateDescriptionAndStartupTypeAsync_UnknownService_UpdatesNothing()
        {
            // Arrange, Act & Assert
            Assert.Equal(0, await _repository.UpdateDescriptionAndStartupTypeAsync("NoSuchService", "desc", 2, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task UpdateDescriptionAndStartupTypeAsync_NullStartupType_LeavesTheStoredStartupType()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            await _repository.UpsertAsync(new ServiceDto { Name = "NoStartup", ExecutablePath = "ok.exe", Description = "Old", StartupType = 2 }, preserveExistingRuntimeState: false, preserveExistingCredentials: false, ct);

            // Act
            var updated = await _repository.UpdateDescriptionAndStartupTypeAsync("NoStartup", "New", null, ct);

            // Assert
            Assert.Equal(1, updated);
            var after = await _repository.GetByNameAsync("NoStartup", decrypt: false, ct);
            Assert.Equal("New", after!.Description);
            Assert.Equal(2, after.StartupType);
        }

        #endregion

        #region Legacy Padded Trim Fallback Tests

        [Fact]
        public async Task GetServicePidAsync_WithPaddedLegacyRow_ResolvesViaResolveByNameAsyncFallback()
        {
            // Arrange: Seed a zombie service row carrying a trailing newline/whitespace character
            const string paddedName = "LegacyEngineService\n";
            var sql = $"INSERT INTO {SqlConstants.ServicesTableName} (Name, ExecutablePath, StartupType, Priority, Pid) VALUES (@Name, 'C:\\engine.exe', '{AppConfig.DefaultStartupType}', '{AppConfig.DefaultProcessPriority}', 7777);";
            await _executor.ExecuteAsync(sql, new { Name = paddedName }, cancellationToken: TestContext.Current.CancellationToken);

            // Act: Query using the raw untrimmed name to test ResolveByNameAsync fallback branch
            int? activePid = await _repository.GetServicePidAsync(paddedName, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(activePid);
            Assert.Equal(7777, activePid.Value);
        }

        [Fact]
        public async Task GetServiceConsoleStateAsync_WithPaddedLegacyRow_ResolvesViaResolveByNameAsyncFallback()
        {
            // Arrange: Seed an un-trimmed service row containing leading whitespace.
            const string paddedName = " GhostService";
            var sql = $@"INSERT INTO {SqlConstants.ServicesTableName} (Name, ExecutablePath, StartupType, Priority, Pid, ActiveStdoutPath, ActiveStderrPath)
                         VALUES (@Name, 'C:\ghost.exe', '{AppConfig.DefaultStartupType}', '{AppConfig.DefaultProcessPriority}', 8888, 'C:\out.log', 'C:\err.log');";
            await _executor.ExecuteAsync(sql, new { Name = paddedName }, cancellationToken: TestContext.Current.CancellationToken);

            // Act: Query using the raw untrimmed name to push parsing into the generic secondary query pass
            var state = await _repository.GetServiceConsoleStateAsync(paddedName, TestContext.Current.CancellationToken);

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
}

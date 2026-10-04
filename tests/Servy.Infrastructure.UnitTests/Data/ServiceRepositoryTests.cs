using Moq;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using Servy.Testing;
using System.Data;
using System.Security.Cryptography;

namespace Servy.Infrastructure.UnitTests.Data
{
    public class ServiceRepositoryTests
    {
        private readonly Mock<IDapperExecutor> _mockDapper;
        private readonly Mock<ISecureData> _mockSecureData;
        private readonly Mock<IXmlServiceSerializer> _mockXmlServiceSerializer;
        private readonly Mock<IJsonServiceSerializer> _mockJsonServiceSerializer;

        // Shared field list driving centralized mock environments and property validation loops
        private static readonly Dictionary<string, string> SensitiveFields = new Dictionary<string, string>()
        {
            { nameof(ServiceDto.Password), "pwd" },
            { nameof(ServiceDto.Parameters), "args" },
            { nameof(ServiceDto.EnvironmentVariables), "env" },
            { nameof(ServiceDto.FailureProgramParameters), "fail_args" },
            { nameof(ServiceDto.PreLaunchParameters), "pre_args" },
            { nameof(ServiceDto.PreLaunchEnvironmentVariables), "pre_env" },
            { nameof(ServiceDto.PostLaunchParameters), "post_args" },
            { nameof(ServiceDto.PreStopParameters), "pre_stop" },
            { nameof(ServiceDto.PostStopParameters), "post_stop" }
        };

        public ServiceRepositoryTests()
        {
            _mockDapper = new Mock<IDapperExecutor>();
            _mockSecureData = new Mock<ISecureData>(MockBehavior.Loose);
            _mockXmlServiceSerializer = new Mock<IXmlServiceSerializer>();
            _mockJsonServiceSerializer = new Mock<IJsonServiceSerializer>();
        }

        private ServiceRepository CreateRepository()
        {
            return new ServiceRepository(_mockDapper.Object, _mockSecureData.Object, _mockXmlServiceSerializer.Object, _mockJsonServiceSerializer.Object);
        }

        private void SetupEncryptPassthrough()
        {
            _mockSecureData.Setup(s => s.Encrypt(It.IsAny<string>()))
                           .Returns<string>(v => v.Replace("_plain", "_enc"));
        }

        private void SetupDecryptPassthrough()
        {
            _mockSecureData.Setup(s => s.Decrypt(It.IsAny<string>()))
                           .Returns<string>(v => v.EndsWith("_enc") ? v.Replace("_enc", "_plain") : v);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_NullDapper_Throws()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ServiceRepository(null!, _mockSecureData.Object, _mockXmlServiceSerializer.Object, _mockJsonServiceSerializer.Object));
            Assert.Equal("dapper", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullSecureData_Throws()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ServiceRepository(_mockDapper.Object, null!, _mockXmlServiceSerializer.Object, _mockJsonServiceSerializer.Object));
            Assert.Equal("secureData", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullXmlServiceSerializer_Throws()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ServiceRepository(_mockDapper.Object, _mockSecureData.Object, null!, _mockJsonServiceSerializer.Object));
            Assert.Equal("xmlServiceSerializer", ex.ParamName);
        }

        [Fact]
        public void Constructor_NullJsonServiceSerializer_Throws()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new ServiceRepository(_mockDapper.Object, _mockSecureData.Object, _mockXmlServiceSerializer.Object, null!));
            Assert.Equal("jsonServiceSerializer", ex.ParamName);
        }

        #endregion

        #region Centralized Security Audit Infrastructure

        private async Task ExecuteFullSecurityAuditTestAsync(Func<ServiceRepository, ServiceDto, Task<object>> repositoryAction, Action<Action<object>> dapperSetupAction)
        {
            // Arrange - Build full DTO dynamically via our shared field registry matrix
            var dto = new ServiceDto { Name = "AuditService", Id = 123 };
            var type = typeof(ServiceDto);
            foreach (var field in SensitiveFields)
            {
                type.GetProperty(field.Key)?.SetValue(dto, $"{field.Value}_plain");
            }

            SetupEncryptPassthrough();

            object? capturedParam = null;
            dapperSetupAction(param => capturedParam = param);

            var repo = CreateRepository();

            // Act
            var result = await repositoryAction(repo, dto);

            // Assert - Functional Results & Side-Effect Protections
            Assert.NotNull(result);
            foreach (var field in SensitiveFields)
            {
                Assert.Equal($"{field.Value}_plain", type.GetProperty(field.Key)?.GetValue(dto));
            }

            // Verify - Ensure object properties passed to Dapper match the encrypted clones accurately
            Assert.NotNull(capturedParam);
            var paramDict = capturedParam.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(capturedParam));

            foreach (var field in SensitiveFields)
            {
                Assert.Equal($"{field.Value}_enc", paramDict[field.Key]);
            }
        }

        #endregion

        #region Mutator Operations & Auditing

        [Fact]
        public async Task UpsertAsync_FullSecurityAudit_EncryptsAllNineSensitiveFields()
        {
            // Arrange & Act & Assert
            await ExecuteFullSecurityAuditTestAsync(
                async (repo, dto) =>
                {
                    var id = await repo.UpsertAsync(dto, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken);
                    Assert.Equal(99, id);
                    Assert.Equal(99, dto.Id);
                    return id;
                },
                captureAction => _mockDapper
                    .Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                    .Callback<string, object, IDbTransaction, CancellationToken>((sql, param, _, token) => captureAction(param))
                    .ReturnsAsync(99)
            );
        }

        [Fact]
        public async Task UpsertAsync_WithPreserveExistingCredentialsTrue_FetchesExistingAndPreservesCredentialsOnConflict()
        {
            // Arrange
            var repo = CreateRepository();
            var incoming = new ServiceDto
            {
                Name = "UpsertTargetService",
                ExecutablePath = "C:\\upsert_updated.exe",
                RunAsLocalSystem = true,
                UserAccount = "UpsertNewUser",
                Password = "UpsertNewPassword"
            };

            var existingInDb = new ServiceDto
            {
                Id = 30,
                Name = "UpsertTargetService",
                ExecutablePath = "C:\\upsert_old.exe",
                RunAsLocalSystem = false,
                UserAccount = "Domain\\UpsertOrigUser",
                Password = "UpsertEncryptedOrigPassword"
            };

            // Setup lookup by name for PatchRuntimeStateAsync
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(
                It.Is<string>(sql => sql.Contains("WHERE Name = @Name")),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingInDb);

            ServiceDto? capturedDto = null;
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<CancellationToken>()))
                .Callback<string, object, IDbTransaction, CancellationToken>((sql, param, _, token) => capturedDto = param as ServiceDto)
                .ReturnsAsync(30);

            // Act
            int upsertedId = await repo.UpsertAsync(incoming, preserveExistingRuntimeState: false, preserveExistingCredentials: true, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(30, upsertedId);
            Assert.NotNull(capturedDto);
            Assert.Equal("C:\\upsert_updated.exe", capturedDto.ExecutablePath);
            Assert.False(capturedDto.RunAsLocalSystem);
            Assert.Equal("Domain\\UpsertOrigUser", capturedDto.UserAccount);
            Assert.Equal("UpsertEncryptedOrigPassword", capturedDto.Password);
        }

        [Fact]
        public async Task UpsertAsync_ReturnsGeneratedId_AndSetsDtoId()
        {
            // Arrange
            var dto = new ServiceDto { Name = "S1" };
            const int expectedId = 5;

            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<IDbTransaction>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedId);

            var repo = CreateRepository();

            // Act
            var resultId = await repo.UpsertAsync(
                dto,
                preserveExistingRuntimeState: false,
                preserveExistingCredentials: false,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(expectedId, resultId);
            Assert.Equal(expectedId, dto.Id);
        }

        [Fact]
        public async Task UpsertAsync_WithPassword_UsesEncryptedPasswordInSql()
        {
            // Arrange
            var dto = new ServiceDto { Name = "NewService", Password = "plain" };
            const string encryptedValue = "encrypted_secret";
            const int generatedId = 7;

            _mockSecureData.Setup(s => s.Encrypt("plain")).Returns(encryptedValue);

            object? capturedParam = null;
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(
                It.IsAny<string>(),
                It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .Callback<string, object, IDbTransaction, CancellationToken>((sql, param, _, token) => capturedParam = param)
                .ReturnsAsync(generatedId);

            var repo = CreateRepository();

            // Act
            var result = await repo.UpsertAsync(
                dto,
                preserveExistingRuntimeState: false,
                preserveExistingCredentials: false,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(generatedId, result);
            Assert.Equal(generatedId, dto.Id);
            Assert.Equal("plain", dto.Password);

            // Verify that the object passed to Dapper actually contained the encrypted value
            Assert.NotNull(capturedParam);
            var paramDict = capturedParam.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(capturedParam));

            Assert.Equal(encryptedValue, paramDict["Password"]);
        }

        [Fact]
        public async Task DeleteAsync_ById_ReturnsAffectedRows()
        {
            // Arrange
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
            var repo = CreateRepository();

            // Act
            var rows = await repo.DeleteAsync(10, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, rows);
        }

        [Fact]
        public async Task DeleteAsync_ByName_ReturnsAffectedRows()
        {
            // Arrange
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
            var repo = CreateRepository();

            // Act
            var rows = await repo.DeleteAsync("ServiceName", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, rows);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task DeleteAsync_ByName_ReturnsZero(string? name)
        {
            // Arrange
            // Explicitly configure no database behavior setup for ExecuteAsync here.
            // The method under test should hit an early-return string constraint check,
            // meaning any Dapper routing constitutes a direct test failure.
            var repo = CreateRepository();

            // Act
            var rows = await repo.DeleteAsync(name, TestContext.Current.CancellationToken);

            // Assert
            // 1. Verify that the method cleanly returned a neutral 0-row metric count
            Assert.Equal(0, rows);

            // 2. Negative Verification: Explicitly prove that the empty-string guard clause
            // intercepted the execution path and never touch the underlying database layer.
            _mockDapper.Verify(d => d.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        #endregion

        #region Retrieval Operations & Decryption

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetByNameAsync_BlankName_ReturnsNullWithoutQuerying(string? name)
        {
            // Arrange
            var repo = CreateRepository();

            // Act
            var result = await repo.GetByNameAsync(name, true, TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(result);
            _mockDapper.Verify(d => d.QuerySingleOrDefaultAsync<ServiceDto>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetServicePidAsync_BlankName_ReturnsNullWithoutQuerying(string? name)
        {
            // Arrange
            var repo = CreateRepository();

            // Act
            var result = await repo.GetServicePidAsync(name, TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(result);
            _mockDapper.Verify(d => d.QuerySingleOrDefaultAsync<int?>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetServiceConsoleStateAsync_BlankName_ReturnsNullWithoutQuerying(string? name)
        {
            // Arrange
            var repo = CreateRepository();

            // Act
            var result = await repo.GetServiceConsoleStateAsync(name, TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(result);
            _mockDapper.Verify(d => d.QuerySingleOrDefaultAsync<ServiceConsoleStateDto?>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }

        private static ServiceDto CreateEncryptedServiceDto()
        {
            var dto = new ServiceDto { Id = 1, Name = "S" };
            var type = typeof(ServiceDto);
            foreach (var field in SensitiveFields)
            {
                type.GetProperty(field.Key)?.SetValue(dto, $"{field.Value}_enc");
            }
            return dto;
        }

        private static void AssertDecryptedDtoProperties(ServiceDto? result)
        {
            Assert.NotNull(result);
            var type = typeof(ServiceDto);
            foreach (var field in SensitiveFields)
            {
                Assert.Equal($"{field.Value}_plain", type.GetProperty(field.Key)?.GetValue(result));
            }
        }

        [Fact]
        public async Task GetByNameAsync_NullPassword()
        {
            // Arrange
            var dto = new ServiceDto { Id = 1, Name = "S", Password = null };
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            var repo = CreateRepository();

            // Act
            var result = await repo.GetByNameAsync("S", true, TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(result!.Password);
        }

        [Fact]
        public async Task GetByNameAsync_EmptyPassword()
        {
            // Arrange
            var dto = new ServiceDto { Id = 1, Name = "S", Password = string.Empty };
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            var repo = CreateRepository();

            // Act
            var result = await repo.GetByNameAsync("S", true, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result!.Password);
            Assert.Empty(result.Password);
        }

        [Fact]
        public async Task GetByNameAsync_NullDto()
        {
            // Arrange
            ServiceDto dto = null!;
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            var repo = CreateRepository();

            // Act
            var result = await repo.GetByNameAsync("S", true, TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetByNameAsync_DecryptsPassword()
        {
            // Arrange
            var dto = CreateEncryptedServiceDto();
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            SetupDecryptPassthrough();
            var repo = CreateRepository();

            // Act
            var result = await repo.GetByNameAsync("S", true, TestContext.Current.CancellationToken);

            // Assert
            AssertDecryptedDtoProperties(result);
        }

        #region Runtime State and Restart Attempts

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task UpdateRuntimeStateAsync_BlankName_ReturnsZeroWithoutWriting(string? name)
        {
            // Arrange
            var repo = CreateRepository();

            // Act
            var result = await repo.UpdateRuntimeStateAsync(name, new ServiceRuntimeStateDto { Pid = 1 }, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0, result);
            _mockDapper.Verify(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateRuntimeStateAsync_NullState_Throws()
        {
            // Arrange
            var repo = CreateRepository();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => repo.UpdateRuntimeStateAsync("svc", null!, TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task UpdateRuntimeStateAsync_WritesOnlyTheRuntimeColumns(bool updatePreviousStopTimeout)
        {
            // Arrange
            string? sql = null;
            object? parameters = null;
            _mockDapper
                .Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .Callback<string, object, IDbTransaction, CancellationToken>((s, p, _, _) => { sql = s; parameters = p; })
                .ReturnsAsync(1);
            var repo = CreateRepository();
            var state = new ServiceRuntimeStateDto
            {
                Pid = 42,
                ActiveStdoutPath = "out.log",
                ActiveStderrPath = "err.log",
                UpdatePreviousStopTimeout = updatePreviousStopTimeout,
                PreviousStopTimeout = 60,
            };

            // Act
            var result = await repo.UpdateRuntimeStateAsync(" svc ", state, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, result);
            Assert.NotNull(sql);
            Assert.Contains("SET Pid = @Pid, ActiveStdoutPath = @ActiveStdoutPath, ActiveStderrPath = @ActiveStderrPath", sql);
            Assert.Equal(updatePreviousStopTimeout, sql!.Contains("PreviousStopTimeout = @PreviousStopTimeout"));

            // No configuration column is ever part of the statement
            foreach (var column in new[] { "ExecutablePath", "Parameters", "Password", "UserAccount", "RestartAttempts" })
                Assert.DoesNotContain(column + " =", sql);

            Assert.Equal("svc", parameters!.GetType().GetProperty("Name")!.GetValue(parameters));
            Assert.Equal(42, parameters.GetType().GetProperty("Pid")!.GetValue(parameters));
        }

        [Fact]
        public async Task UpdateRuntimeStateAsync_LegacyPaddedName_RetriesWithTheVerbatimName()
        {
            // Arrange: no row under the trimmed name, one under the verbatim (Servy <= 8.3) name
            var names = new List<string>();
            _mockDapper
                .Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .Callback<string, object, IDbTransaction, CancellationToken>((_, p, _, _) => names.Add((string)p.GetType().GetProperty("Name")!.GetValue(p)!))
                .ReturnsAsync(() => names.Count == 1 ? 0 : 1);
            var repo = CreateRepository();

            // Act
            var result = await repo.UpdateRuntimeStateAsync(" padded ", new ServiceRuntimeStateDto(), TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, result);
            Assert.Equal(new[] { "padded", " padded " }, names);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task UpdateDescriptionAndStartupTypeAsync_BlankName_ReturnsZeroWithoutWriting(string? name)
        {
            // Arrange
            var repo = CreateRepository();

            // Act
            var result = await repo.UpdateDescriptionAndStartupTypeAsync(name, "desc", 2, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0, result);
            _mockDapper.Verify(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(2)]
        [InlineData(null)]
        public async Task UpdateDescriptionAndStartupTypeAsync_WritesOnlyTheTwoMetadataColumns(int? startupType)
        {
            // Arrange
            string? sql = null;
            object? parameters = null;
            _mockDapper
                .Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .Callback<string, object, IDbTransaction, CancellationToken>((s, p, _, _) => { sql = s; parameters = p; })
                .ReturnsAsync(1);
            var repo = CreateRepository();

            // Act
            var result = await repo.UpdateDescriptionAndStartupTypeAsync(" svc ", "from the SCM", startupType, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, result);
            Assert.NotNull(sql);
            Assert.Contains("SET Description = @Description", sql);
            Assert.Equal(startupType.HasValue, sql!.Contains("StartupType = @StartupType"));
            Assert.Contains("WHERE Name = @Name COLLATE UNICODE_NOCASE", sql);

            // No sensitive or runtime column is ever part of the statement: this is the whole point of the
            // call, because the refresh tick's DTO is a display read and not the stored row (#7328)
            foreach (var column in new[] { "Parameters", "EnvironmentVariables", "PreLaunchEnvironmentVariables", "PreStopParameters", "PostStopParameters", "Password", "Pid" })
                Assert.DoesNotContain(column + " =", sql);

            Assert.Equal("svc", parameters!.GetType().GetProperty("Name")!.GetValue(parameters));
            Assert.Equal("from the SCM", parameters.GetType().GetProperty("Description")!.GetValue(parameters));
            Assert.Equal(startupType, parameters.GetType().GetProperty("StartupType")!.GetValue(parameters));
        }

        [Fact]
        public async Task UpdateDescriptionAndStartupTypeAsync_LegacyPaddedName_RetriesWithTheVerbatimName()
        {
            // Arrange: no row under the trimmed name, one under the verbatim (Servy <= 8.3) name
            var names = new List<string>();
            _mockDapper
                .Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .Callback<string, object, IDbTransaction, CancellationToken>((_, p, _, _) => names.Add((string)p.GetType().GetProperty("Name")!.GetValue(p)!))
                .ReturnsAsync(() => names.Count == 1 ? 0 : 1);
            var repo = CreateRepository();

            // Act
            var result = await repo.UpdateDescriptionAndStartupTypeAsync(" padded ", "desc", 2, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, result);
            Assert.Equal(new[] { "padded", " padded " }, names);
        }

        [Fact]
        public async Task UpdateRestartAttemptsAsync_NegativeAttempts_Throws()
        {
            // Arrange
            var repo = CreateRepository();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repo.UpdateRestartAttemptsAsync("svc", -1, DateTime.UtcNow, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task UpdateRestartAttemptsAsync_WritesTheCounterAndItsUtcTicks()
        {
            // Arrange
            object? parameters = null;
            string? sql = null;
            _mockDapper
                .Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .Callback<string, object, IDbTransaction, CancellationToken>((s, p, _, _) => { sql = s; parameters = p; })
                .ReturnsAsync(1);
            var repo = CreateRepository();
            var when = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

            // Act
            var result = await repo.UpdateRestartAttemptsAsync("svc", 3, when, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, result);
            Assert.Contains("SET RestartAttempts = @RestartAttempts, RestartAttemptsUpdatedAtTicks = @RestartAttemptsUpdatedAtTicks", sql);
            Assert.Equal(3, parameters!.GetType().GetProperty("RestartAttempts")!.GetValue(parameters));
            Assert.Equal(when.Ticks, parameters.GetType().GetProperty("RestartAttemptsUpdatedAtTicks")!.GetValue(parameters));
        }

        [Fact]
        public async Task UpdateRestartAttemptsAsync_LocalTime_IsStoredAsUtc()
        {
            // Arrange
            object? parameters = null;
            _mockDapper
                .Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .Callback<string, object, IDbTransaction, CancellationToken>((_, p, _, _) => parameters = p)
                .ReturnsAsync(1);
            var repo = CreateRepository();
            var local = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Local);

            // Act
            await repo.UpdateRestartAttemptsAsync("svc", 1, local, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(local.ToUniversalTime().Ticks, parameters!.GetType().GetProperty("RestartAttemptsUpdatedAtTicks")!.GetValue(parameters));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task GetRestartAttemptsAsync_BlankName_ReturnsNull(string? name)
        {
            // Arrange
            var repo = CreateRepository();

            // Act
            var result = await repo.GetRestartAttemptsAsync(name, TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetRestartAttemptsAsync_NoRow_ReturnsNull()
        {
            // Arrange
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto?>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ServiceDto?)null);
            var repo = CreateRepository();

            // Act
            var result = await repo.GetRestartAttemptsAsync("ghost", TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData(null, null, 0, false)]          // never written
        [InlineData(5, 638_000_000_000_000_000L, 5, true)]
        [InlineData(-3, 638_000_000_000_000_000L, 0, true)]  // a negative stored value reads back as 0
        [InlineData(2, -1L, 2, false)]              // ticks out of DateTime's range read back as no time
        public async Task GetRestartAttemptsAsync_Row_MapsCounterAndTimestamp(int? stored, long? ticks, int expectedAttempts, bool expectTime)
        {
            // Arrange
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto?>(
                    It.Is<string>(s => s.Contains("SELECT Name, RestartAttempts, RestartAttemptsUpdatedAtTicks")),
                    It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceDto { Name = "svc", RestartAttempts = stored, RestartAttemptsUpdatedAtTicks = ticks });
            var repo = CreateRepository();

            // Act
            var result = await repo.GetRestartAttemptsAsync("svc", TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedAttempts, result!.Attempts);
            Assert.Equal(expectTime, result.UpdatedAtUtc.HasValue);
            if (expectTime)
            {
                Assert.Equal(ticks!.Value, result.UpdatedAtUtc!.Value.Ticks);
                Assert.Equal(DateTimeKind.Utc, result.UpdatedAtUtc.Value.Kind);
            }
        }

        #endregion

        [Fact]
        public async Task GetServicePidAsync_ServiceIsRunning_ReturnsPid()
        {
            // Arrange
            var serviceName = "RunningService";
            int expectedPid = 1234;

            _mockDapper
                .Setup(e => e.QuerySingleOrDefaultAsync<int?>(
                    It.Is<string>(sql => sql.Contains($"SELECT Pid FROM {SqlConstants.ServicesTableName}")),
                    It.Is<object>(p => p.GetType().GetProperty("Name")!.GetValue(p)!.ToString() == serviceName), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedPid);

            var repo = CreateRepository();

            // Act
            var result = await repo.GetServicePidAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedPid, result);
            _mockDapper.VerifyAll();
        }

        [Fact]
        public async Task GetServicePidAsync_NoPidAvailable_ReturnsNull()
        {
            // Arrange
            var serviceName = "MissingOrStoppedService";

            // Enforce the strict SQL matcher condition to lock down the targeted schema query pattern
            _mockDapper
                .Setup(e => e.QuerySingleOrDefaultAsync<int?>(
                    It.Is<string>(sql => sql.Contains($"SELECT Pid FROM {SqlConstants.ServicesTableName}")),
                    It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int?)null!);

            var repo = CreateRepository();

            // Act
            var result = await repo.GetServicePidAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetServicePidAsync_PassesCancellationToken()
        {
            // Arrange
            var serviceName = "TestService";
            var repo = CreateRepository();

            // Act
            await repo.GetServicePidAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            _mockDapper.Verify(e => e.QuerySingleOrDefaultAsync<int?>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.Is<CancellationToken>(t => t == TestContext.Current.CancellationToken)), Times.Once);
        }

        [Fact]
        public async Task GetServiceConsoleStateAsync_ServiceExists_ReturnsLightweightDto()
        {
            // Arrange
            var serviceName = "ConsoleTestService";
            var expectedState = new ServiceConsoleStateDto
            {
                Pid = 5678,
                ActiveStdoutPath = @"C:\Logs\stdout.log",
                ActiveStderrPath = @"C:\Logs\stderr.log"
            };

            _mockDapper
                .Setup(e => e.QuerySingleOrDefaultAsync<ServiceConsoleStateDto?>(
                    It.Is<string>(sql => sql.Contains("SELECT Pid, ActiveStdoutPath, ActiveStderrPath")),
                    It.Is<object>(p => p.GetType()!.GetProperty("Name")!.GetValue(p)!.ToString()! == serviceName), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedState);

            var repo = CreateRepository();

            // Act
            var result = await repo.GetServiceConsoleStateAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedState.Pid, result.Pid);
            Assert.Equal(expectedState.ActiveStdoutPath, result.ActiveStdoutPath);
            Assert.Equal(expectedState.ActiveStderrPath, result.ActiveStderrPath);
            _mockDapper.VerifyAll();
        }

        [Fact]
        public async Task GetServiceConsoleStateAsync_ServiceDoesNotExist_ReturnsNull()
        {
            // Arrange
            var serviceName = "MissingService";

            _mockDapper
                .Setup(e => e.QuerySingleOrDefaultAsync<ServiceConsoleStateDto?>(
                    It.IsAny<string>(),
                    It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ServiceConsoleStateDto?)null);

            var repo = CreateRepository();

            // Act
            var result = await repo.GetServiceConsoleStateAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetServiceConsoleStateAsync_VerifiesSqlParametersAndStructure()
        {
            // Arrange
            var serviceName = "SqlVerifyService";
            var expectedDto = new ServiceConsoleStateDto();

            // Maintain a broad setup so that Dapper returns a valid instance when called,
            // ensuring execution proceeds smoothly to the assertion phase.
            _mockDapper
                .Setup(e => e.QuerySingleOrDefaultAsync<ServiceConsoleStateDto?>(
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<IDbTransaction>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDto);

            var repo = CreateRepository();

            // Act
            var result = await repo.GetServiceConsoleStateAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            // 1. Verify structural response integrity to intercept unexpected setup misses
            Assert.NotNull(result);
            Assert.Same(expectedDto, result);

            // 2. Firmly validate that the executed SQL structure conforms to design rules
            _mockDapper.Verify(e => e.QuerySingleOrDefaultAsync<ServiceConsoleStateDto?>(
                It.Is<string>(sql =>
                    sql.Contains("SELECT Pid, ActiveStdoutPath, ActiveStderrPath") &&
                    sql.Contains($"FROM {SqlConstants.ServicesTableName}") &&
                    sql.Contains("WHERE Name = @Name") &&
                    sql.Contains("LIMIT 1")),
                It.Is<object>(p => (string?)p.GetType().GetProperty("Name")!.GetValue(p) == serviceName),
                It.IsAny<IDbTransaction>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetAllAsync_DecryptsAll()
        {
            // Arrange
            var service1 = new ServiceDto { Id = 1 };
            var service2 = new ServiceDto { Id = 2 };
            var type = typeof(ServiceDto);

            foreach (var field in SensitiveFields)
            {
                type.GetProperty(field.Key)?.SetValue(service1, $"{field.Value}1_enc");
                type.GetProperty(field.Key)?.SetValue(service2, $"{field.Value}2_enc");

                _mockSecureData.Setup(s => s.Decrypt($"{field.Value}1_enc")).Returns($"{field.Value}1_plain");
                _mockSecureData.Setup(s => s.Decrypt($"{field.Value}2_enc")).Returns($"{field.Value}2_plain");
            }

            var list = new List<ServiceDto> { service1, service2 };

            _mockDapper
                .Setup(d => d.QueryAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);

            var repo = CreateRepository();

            // Act
            var result = (await repo.GetAllAsync(true, TestContext.Current.CancellationToken)).ToList();

            // Assert
            Assert.Collection(result,
                r =>
                {
                    foreach (var field in SensitiveFields)
                    {
                        Assert.Equal($"{field.Value}1_plain", type.GetProperty(field.Key)?.GetValue(r));
                    }
                },
                r =>
                {
                    foreach (var field in SensitiveFields)
                    {
                        Assert.Equal($"{field.Value}2_plain", type.GetProperty(field.Key)?.GetValue(r));
                    }
                }
            );
        }

        [Fact]
        public async Task GetAllAsync_DecryptFalse_ReturnsCiphertextAndNeverCallsDecrypt()
        {
            // Arrange - the exact negative control of GetAllAsync_DecryptsAll
            var service1 = new ServiceDto { Id = 1 };
            var type = typeof(ServiceDto);

            foreach (var field in SensitiveFields)
            {
                type.GetProperty(field.Key)?.SetValue(service1, $"{field.Value}1_enc");
            }

            SetupDecryptPassthrough();

            _mockDapper
                .Setup(d => d.QueryAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ServiceDto> { service1 });

            var repo = CreateRepository();

            // Act
            var result = (await repo.GetAllAsync(false, TestContext.Current.CancellationToken)).ToList();

            // Assert
            var single = Assert.Single(result);
            foreach (var field in SensitiveFields)
            {
                Assert.Equal($"{field.Value}1_enc", type.GetProperty(field.Key)?.GetValue(single));
            }
            _mockSecureData.Verify(s => s.Decrypt(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SearchAsync_MatchingKeyword_DecryptFalse_ForwardsFlagToFilteredPath()
        {
            // Arrange
            var list = new List<ServiceDto> { new ServiceDto { Name = "A", Password = "enc1" } };
            _mockDapper
                .Setup(d => d.QueryAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);
            SetupDecryptPassthrough();

            var repo = CreateRepository();

            // Act
            var result = (await repo.SearchAsync("A", false, TestContext.Current.CancellationToken)).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal("enc1", result[0].Password);
            _mockSecureData.Verify(s => s.Decrypt(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SearchAsync_NullKeyword_DecryptFalse_ForwardsFlagToShortCircuit()
        {
            // Arrange
            var list = new List<ServiceDto> { CreateEncryptedServiceDto() };
            _mockDapper
                .Setup(d => d.QueryAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);
            SetupDecryptPassthrough();

            var repo = CreateRepository();

            // Act
            var result = (await repo.SearchAsync(null!, false, TestContext.Current.CancellationToken)).ToList();

            // Assert
            var single = Assert.Single(result);
            var type = typeof(ServiceDto);
            foreach (var field in SensitiveFields)
            {
                Assert.Equal($"{field.Value}_enc", type.GetProperty(field.Key)?.GetValue(single));
            }
            _mockSecureData.Verify(s => s.Decrypt(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SearchAsync_MatchingKeyword_DecryptsPasswords()
        {
            // Arrange
            var list = new List<ServiceDto> { new ServiceDto { Name = "A", Password = "enc1" } };
            _mockDapper
                .Setup(d => d.QueryAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);
            _mockSecureData.Setup(s => s.Decrypt("enc1")).Returns("pwd1");

            var repo = CreateRepository();

            // Act
            var result = (await repo.SearchAsync("A", true, TestContext.Current.CancellationToken)).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal("pwd1", result[0].Password);
        }

        [Fact]
        public async Task SearchAsync_NullKeyword_ShortCircuitsToGetAll()
        {
            // Arrange
            var list = new List<ServiceDto> { CreateEncryptedServiceDto() };

            // Enforce that a null/whitespace keyword routes explicitly
            // to the GetAllAsync SQL string pattern instead of matching any arbitrary query definition.
            string expectedGetAllSql = $"SELECT * FROM {SqlConstants.ServicesTableName} ORDER BY Name COLLATE UNICODE_NOCASE ASC;";

            _mockDapper
                .Setup(d => d.QueryAsync<ServiceDto>(
                    It.Is<string>(sql => sql == expectedGetAllSql),
                    It.IsAny<object>(),
                    It.IsAny<IDbTransaction>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);

            SetupDecryptPassthrough();
            var repo = CreateRepository();

            // Act
            var result = (await repo.SearchAsync(null!, true, TestContext.Current.CancellationToken)).ToList();

            // Assert
            Assert.Single(result);
            AssertDecryptedDtoProperties(result[0]);

            // Verify that our constrained Dapper call was hit exactly once
            _mockDapper.Verify(d => d.QueryAsync<ServiceDto>(
                It.Is<string>(sql => sql == expectedGetAllSql),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Import/Export Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ExportXmlAsync_BlankName_ReturnsEmptyStringWithoutQuerying(string? name)
        {
            // Arrange
            var repo = CreateRepository();

            // Act
            var result = await repo.ExportXmlAsync(name, TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(result);
            _mockDapper.Verify(d => d.QuerySingleOrDefaultAsync<ServiceDto>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
            _mockXmlServiceSerializer.Verify(s => s.Serialize(It.IsAny<ServiceDto>()), Times.Never);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ExportJsonAsync_BlankName_ReturnsEmptyStringWithoutQuerying(string? name)
        {
            // Arrange
            var repo = CreateRepository();

            // Act
            var result = await repo.ExportJsonAsync(name, TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(result);
            _mockDapper.Verify(d => d.QuerySingleOrDefaultAsync<ServiceDto>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
            _mockJsonServiceSerializer.Verify(s => s.Serialize(It.IsAny<ServiceDto>()), Times.Never);
        }

        [Fact]
        public async Task ExportXmlAsync_ServiceMissing_ReturnsEmptyString()
        {
            // Arrange
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ServiceDto)null!);

            var repo = CreateRepository();

            // Act
            var xml = await repo.ExportXmlAsync("A", TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(xml);
        }

        [Fact]
        public async Task ExportXmlAsync_ServiceFound_ReturnsSerializedService()
        {
            // Arrange
            var dto = new ServiceDto { Name = "A", Password = "pwd1" };

            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            _mockSecureData
                .Setup(s => s.Decrypt("pwd1"))
                .Returns("pwd1");

            _mockXmlServiceSerializer
                .Setup(s => s.Serialize(dto))
                .Returns((ServiceDto d) => $"<ServiceDto><Name>{d.Name}</Name></ServiceDto>");

            var repo = CreateRepository();

            // Act
            var xml = await repo.ExportXmlAsync("A", TestContext.Current.CancellationToken);

            // Assert
            // Verify that the serialization engine executes completely and returns the payload stream
            Assert.Contains("<ServiceDto", xml);
            Assert.Contains("<Name>A</Name>", xml);
        }

        [Fact]
        public async Task ExportJsonAsync_ServiceMissing_ReturnsEmptyString()
        {
            // Arrange
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(
               It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((ServiceDto)null!);

            var repo = CreateRepository();

            // Act
            var json = await repo.ExportJsonAsync("A", TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(json);
        }

        [Fact]
        public async Task ExportJsonAsync_ServiceFound_ReturnsSerializedService()
        {
            // Arrange
            var name = "A";
            var dto = new ServiceDto { Name = name };
            var expectedJson = "{\"Name\": \"A\"}";

            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            _mockJsonServiceSerializer
                .Setup(s => s.Serialize(It.IsAny<ServiceDto>()))
                .Returns(expectedJson);

            var repo = CreateRepository();

            // Act
            var json = await repo.ExportJsonAsync(name, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(expectedJson, json);
        }

        #endregion

        #region Private Helper Branch Coverage Tests

        [Fact]
        public async Task CreateEncryptedClone_StripsDecryptionFailureMarkerFromDescription()
        {
            // Arrange
            var repo = CreateRepository();
            const string originalDescription = "This is the real service description.";
            const string markedDescription = "[DECRYPTION FAILED: SecureDataIntegrityException] The record's key or payload is corrupt. Original Description: " + originalDescription;

            var dto = new ServiceDto
            {
                Name = "TestService",
                Description = markedDescription,
                Password = "plain_password"
            };

            ServiceDto? capturedEncryptedClone = null;
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<CancellationToken>()))
                .Callback<string, object, IDbTransaction, CancellationToken>((sql, param, _, token) => capturedEncryptedClone = param as ServiceDto)
                .ReturnsAsync(1);

            // Act
            await repo.UpsertAsync(dto, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(capturedEncryptedClone);
            Assert.Equal(originalDescription, capturedEncryptedClone.Description);
            Assert.Equal(markedDescription, dto.Description);
        }

        [Fact]
        public async Task CreateEncryptedClone_StripsStackedDecryptionFailureMarkersFromDescription()
        {
            // Arrange
            var repo = CreateRepository();
            const string originalDescription = "Original clean description.";
            const string stackedMarkedDescription =
                "[DECRYPTION FAILED: SecureDataIntegrityException] The record's key or payload is corrupt. Original Description: " +
                "[DECRYPTION FAILED: CryptographicException] The record's key or payload is corrupt. Original Description: " +
                originalDescription;

            var dto = new ServiceDto
            {
                Name = "TestServiceStacked",
                Description = stackedMarkedDescription,
                Password = "plain_password"
            };

            ServiceDto? capturedEncryptedClone = null;
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<CancellationToken>()))
                .Callback<string, object, IDbTransaction, CancellationToken>((sql, param, _, token) => capturedEncryptedClone = param as ServiceDto)
                .ReturnsAsync(1);

            // Act
            await repo.UpsertAsync(dto, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(capturedEncryptedClone);
            Assert.Equal(originalDescription, capturedEncryptedClone.Description);
        }

        [Fact]
        public async Task CreateEncryptedClone_StripsTheCorruptMarkerAsHandleCorruptServiceDecryptionActuallyWritesIt()
        {
            // Arrange
            // The marker is built by the real producer rather than transcribed here, so a reword of
            // its text no longer passes unnoticed: the strip regex is the only other copy, and if the
            // two drift apart this assertion fails instead of the marker reaching the Description column.
            var repo = CreateRepository();
            const string originalDescription = "This is the real service description.";

            var dto = new ServiceDto
            {
                Name = "TestService",
                Description = originalDescription,
                Password = "plain_password"
            };

            TestReflection.InvokeNonPublic(repo, "HandleCorruptServiceDecryption", dto,
                new InvalidOperationException("decryption failed", new CryptographicException("bad key")));

            // Guard the guard: a producer that stopped marking would make the round-trip below vacuous.
            Assert.NotEqual(originalDescription, dto.Description);

            ServiceDto? capturedEncryptedClone = null;
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<CancellationToken>()))
                .Callback<string, object, IDbTransaction, CancellationToken>((sql, param, _, token) => capturedEncryptedClone = param as ServiceDto)
                .ReturnsAsync(1);

            // Act
            await repo.UpsertAsync(dto, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(capturedEncryptedClone);
            Assert.Equal(originalDescription, capturedEncryptedClone.Description);
        }

        [Fact]
        public async Task CreateEncryptedClone_StripsTheLegacyMarkerAsHandleLegacyBlockedDecryptionActuallyWritesIt()
        {
            // Arrange
            // Same round-trip for the second producer: its message is the longer of the two branches
            // in the strip pattern and the one most likely to be reworded.
            var repo = CreateRepository();
            const string originalDescription = "Original clean description.";

            var dto = new ServiceDto
            {
                Name = "LegacyRow",
                Description = originalDescription,
                Password = "plain_password"
            };

            TestReflection.InvokeNonPublic(repo, "HandleLegacyBlockedDecryption", dto,
                new SecureDataLegacyBlockedException("v1 payload refused by policy"));

            Assert.NotEqual(originalDescription, dto.Description);

            ServiceDto? capturedEncryptedClone = null;
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IDbTransaction>(),
                It.IsAny<CancellationToken>()))
                .Callback<string, object, IDbTransaction, CancellationToken>((sql, param, _, token) => capturedEncryptedClone = param as ServiceDto)
                .ReturnsAsync(1);

            // Act
            await repo.UpsertAsync(dto, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(capturedEncryptedClone);
            Assert.Equal(originalDescription, capturedEncryptedClone.Description);
        }

        [Fact]
        public async Task PatchRuntimeStateAsync_ExistingNotNull_ExecutesApplyRuntimeState()
        {
            // Arrange
            var repo = CreateRepository();
            var incoming = new ServiceDto { Name = "TargetService", Pid = 0 };
            var databaseMatch = new ServiceDto { Name = "TargetService", Pid = 9999, ActiveStdoutPath = "db.log" };

            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(databaseMatch);

            // Act
            await repo.UpsertAsync(incoming, preserveExistingRuntimeState: true, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Assert
            _mockDapper.Verify(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.Is<ServiceDto>(s => s.Pid == 9999 && s.ActiveStdoutPath == "db.log"), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(true, true)]   // Covers Both State + Credentials branches
        [InlineData(true, false)]  // Covers State branch only
        [InlineData(false, true)]  // Covers Credentials branch only
        [InlineData(false, false)] // Covers neither
        public void ApplyRuntimeState_AllCombinationsAndBranches_Covered(bool preserveState, bool preserveCredentials)
        {
            // Arrange
            var incoming = new ServiceDto
            {
                Name = "Test",
                Pid = 0,
                ActiveStdoutPath = "inc_out.log",
                ActiveStderrPath = "inc_err.log",
                PreviousStopTimeout = 10,
                RestartAttempts = 1,
                RestartAttemptsUpdatedAtTicks = 100,
                RunAsLocalSystem = true,
                UserAccount = "inc_user",
                Password = "new_password"
            };

            var existing = new ServiceDto
            {
                Name = "Test",
                Pid = 555,
                ActiveStdoutPath = "active_stdout.log",
                ActiveStderrPath = "active_stderr.log",
                PreviousStopTimeout = 30,
                RestartAttempts = 4,
                RestartAttemptsUpdatedAtTicks = 400,
                RunAsLocalSystem = false,
                UserAccount = "old_user",
                Password = "old_password"
            };

            // Act
            TestReflection.InvokeNonPublicStatic(
                typeof(ServiceRepository),
                "ApplyRuntimeState",
                incoming,
                existing,
                preserveState,
                preserveCredentials);

            // Assert
            if (preserveState)
            {
                Assert.Equal(555, incoming.Pid);
                Assert.Equal("active_stdout.log", incoming.ActiveStdoutPath);
                Assert.Equal("active_stderr.log", incoming.ActiveStderrPath);
                Assert.Equal(30, incoming.PreviousStopTimeout);

                // The restart attempts counter is runtime state too: editing a service never resets its quota
                Assert.Equal(4, incoming.RestartAttempts);
                Assert.Equal(400, incoming.RestartAttemptsUpdatedAtTicks);
            }
            else
            {
                Assert.Equal(0, incoming.Pid);
                Assert.Equal("inc_out.log", incoming.ActiveStdoutPath);
                Assert.Equal("inc_err.log", incoming.ActiveStderrPath);
                Assert.Equal(10, incoming.PreviousStopTimeout);
                Assert.Equal(1, incoming.RestartAttempts);
                Assert.Equal(100, incoming.RestartAttemptsUpdatedAtTicks);
            }

            if (preserveCredentials)
            {
                Assert.False(incoming.RunAsLocalSystem);
                Assert.Equal("old_user", incoming.UserAccount);
                Assert.Equal("old_password", incoming.Password);
            }
            else
            {
                Assert.True(incoming.RunAsLocalSystem);
                Assert.Equal("inc_user", incoming.UserAccount);
                Assert.Equal("new_password", incoming.Password);
            }
        }

        [Fact]
        public async Task CreateEncryptedClone_CatchBlock_ThrowsInvalidOperationException()
        {
            // Arrange
            var repo = CreateRepository();
            var dto = new ServiceDto { Name = "FaultyService", Parameters = "plain-text" };

            _mockSecureData.Setup(s => s.Encrypt(It.IsAny<string>()))
                           .Throws(new CryptographicException("Hardware key missing"));

            // Act & Assert
            var wrapperEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                 repo.UpsertAsync(dto, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken));

            Assert.Contains("Encryption failed for field", wrapperEx.Message);
            Assert.NotNull(wrapperEx.InnerException);
            Assert.IsType<CryptographicException>(wrapperEx.InnerException);
        }

        [Fact]
        public async Task UpsertAsync_NullService_ThrowsArgumentNullExceptionFromCreateEncryptedClone()
        {
            // Arrange
            var repo = CreateRepository();

            // Act & Assert
            // UpsertAsync has no null check of its own; the guard under test is the one
            // CreateEncryptedClone applies before anything is written.
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                repo.UpsertAsync(null!, preserveExistingRuntimeState: false, preserveExistingCredentials: false, TestContext.Current.CancellationToken));

            Assert.Equal("source", ex.ParamName);

            _mockDapper.Verify(
                e => e.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public void DecryptDto_CatchBlock_BubblesUpDescriptiveException()
        {
            // Arrange
            var repo = CreateRepository();
            var corruptDto = new ServiceDto { Id = 1, Password = "corrupt-payload" };

            _mockSecureData.Setup(s => s.Decrypt(It.IsAny<string>()))
                           .Throws(new FormatException("Invalid base64 string layout"));

            // Act & Assert
            var baseEx = Assert.Throws<InvalidOperationException>(() =>
                TestReflection.InvokeNonPublic(repo, "DecryptDto", corruptDto));

            Assert.Contains("Decryption failed for field", baseEx.Message);
        }

        [Fact]
        public async Task SafeDecrypt_CatchInvalidOperationException_RoutesToCorruptServiceDecryptionHandler()
        {
            // Arrange
            var repo = CreateRepository();
            var poisonDto = new ServiceDto { Id = 77, Name = "PoisonRow", Description = "Original Description" };
            var type = typeof(ServiceDto);
            foreach (var field in SensitiveFields)
            {
                type.GetProperty(field.Key)?.SetValue(poisonDto, $"{field.Value}_poison");
            }

            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(poisonDto);

            _mockSecureData.Setup(s => s.Decrypt(It.IsAny<string>()))
                           .Throws(new TimeoutException("Cryptographic subsystem timed out."));

            // Act
            var result = await repo.GetByNameAsync("PoisonRow", decrypt: true, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("[DECRYPTION FAILED: TimeoutException]", result.Description);

            foreach (var field in SensitiveFields)
            {
                Assert.Null(type.GetProperty(field.Key)?.GetValue(result));
            }
        }

        [Fact]
        public void HandleCorruptServiceDecryption_AllBranchesAndNullGuards_Covered()
        {
            // Arrange
            var repo = CreateRepository();
            var dto = new ServiceDto { Name = "CorruptDataRow", Description = "KeepMe", Password = "XYZ" };

            var targetExNoInner = new InvalidOperationException("Generic operational fault context");
            var targetExWithInner = new InvalidOperationException("Root diagnostic path", new UnauthorizedAccessException());

            // Act & Assert
            // 1. Branch path verification: Guard tracking on null DTO elements
            TestReflection.InvokeNonPublic(repo, "HandleCorruptServiceDecryption", null, targetExWithInner);

            // 2. Branch path verification: Inner Exception evaluates to Null fallback logic mapping
            TestReflection.InvokeNonPublic(repo, "HandleCorruptServiceDecryption", dto, targetExNoInner);

            Assert.Contains("[DECRYPTION FAILED: InvalidOperationException]", dto.Description);

            // 3. Branch path verification: Inner Exception matches concrete reference mapping layout rules
            var freshDto = new ServiceDto { Name = "Row2", Description = "Meta" };
            var type = typeof(ServiceDto);
            foreach (var field in SensitiveFields)
            {
                type.GetProperty(field.Key)?.SetValue(freshDto, $"{field.Value}_corrupt");
            }

            TestReflection.InvokeNonPublic(repo, "HandleCorruptServiceDecryption", freshDto, targetExWithInner);

            Assert.Contains("[DECRYPTION FAILED: UnauthorizedAccessException]", freshDto.Description);
            foreach (var field in SensitiveFields)
            {
                Assert.Null(type.GetProperty(field.Key)?.GetValue(freshDto));
            }
        }

        [Fact]
        public void HandleLegacyBlockedDecryption_AllBranchesAndNullGuards_Covered()
        {
            // Arrange
            var repo = CreateRepository();
            var dto = new ServiceDto { Name = "LegacyRow", Description = "KeepMe", Password = "XYZ" };
            var ex = new SecureDataLegacyBlockedException("v1 payload refused by policy");

            // Act & Assert
            // 1. Branch path verification: Guard tracking on null DTO elements
            TestReflection.InvokeNonPublic(repo, "HandleLegacyBlockedDecryption", null, ex);

            // 2. The description is flagged and, unlike the corrupt-record handler above, the sensitive
            // fields are deliberately left as stored ciphertext - the record is intact, not corrupt.
            TestReflection.InvokeNonPublic(repo, "HandleLegacyBlockedDecryption", dto, ex);

            Assert.Contains("[LEGACY ENCRYPTION BLOCKED]", dto.Description);
            Assert.Contains("KeepMe", dto.Description);
            Assert.Equal("XYZ", dto.Password);
        }

        [Fact]
        public void SafeDecrypt_LegacyBlockedException_RoutesToLegacyHandlerNotCorruptHandler()
        {
            // Arrange
            _mockSecureData.Setup(s => s.Decrypt(It.IsAny<string>()))
                           .Throws(new SecureDataLegacyBlockedException("v1 payload refused by policy"));

            var repo = CreateRepository();
            var dto = new ServiceDto { Name = "LegacyRow", Description = "KeepMe", Password = "XYZ" };

            // Act
            TestReflection.InvokeNonPublic(repo, "SafeDecrypt", dto);

            // Assert: flagged as policy-refused, not scrubbed as corrupt
            Assert.Contains("[LEGACY ENCRYPTION BLOCKED]", dto.Description);
            Assert.DoesNotContain("[DECRYPTION FAILED", dto.Description);
            Assert.Equal("XYZ", dto.Password);
        }

        #endregion
    }
}

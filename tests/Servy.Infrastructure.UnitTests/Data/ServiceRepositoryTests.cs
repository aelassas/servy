using Dapper;
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
                           .Returns<string>(v =>
                           {
                               // Handle explicitly hardcoded bulk variants or plain string variations cleanly
                               if (v == "encrypted" || v == "enc1" || v == "enc2")
                                   return v.Replace("encrypted", "plain").Replace("enc1", "pwd1").Replace("enc2", "pwd2");

                               // If generated dynamically via SensitiveFields rules, revert back seamlessly
                               if (v.EndsWith("_enc"))
                                   return v.Replace("_enc", "_plain");

                               return v;
                           });
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_NullDapper_Throws()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(null!, _mockSecureData.Object, _mockXmlServiceSerializer.Object, _mockJsonServiceSerializer.Object));
        }

        [Fact]
        public void Constructor_NullSecureData_Throws()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(_mockDapper.Object, null!, _mockXmlServiceSerializer.Object, _mockJsonServiceSerializer.Object));
        }

        [Fact]
        public void Constructor_NullXmlServiceSerializer_Throws()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(_mockDapper.Object, _mockSecureData.Object, null!, _mockJsonServiceSerializer.Object));
        }

        [Fact]
        public void Constructor_NullJsonServiceSerializer_Throws()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(_mockDapper.Object, _mockSecureData.Object, _mockXmlServiceSerializer.Object, null!));
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
        public async Task AddAsync_FullSecurityAudit_EncryptsAllNineSensitiveFields()
        {
            await ExecuteFullSecurityAuditTestAsync(
                async (repo, dto) =>
                {
                    var id = await repo.AddAsync(dto, TestContext.Current.CancellationToken);
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
        public async Task UpdateAsync_FullSecurityAudit_EncryptsAllNineSensitiveFields()
        {
            await ExecuteFullSecurityAuditTestAsync(
                async (repo, dto) => await repo.UpdateAsync(dto, false, false, TestContext.Current.CancellationToken),
                captureAction => _mockDapper
                    .Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                    .Callback<string, object, IDbTransaction, CancellationToken>((sql, param, _, token) => captureAction(param))
                    .ReturnsAsync(1)
            );
        }

        [Fact]
        public async Task Update_FullSecurityAudit_EncryptsAllNineSensitiveFields()
        {
            await ExecuteFullSecurityAuditTestAsync(
                (repo, dto) => Task.FromResult<object>(repo.Update(dto, false, false)),
                captureAction => _mockDapper
                    .Setup(d => d.Execute(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>()))
                    .Callback<string, object, IDbTransaction>((sql, param, _) => captureAction(param))
                    .Returns(1)
            );
        }

        [Fact]
        public async Task UpsertAsync_ExistingService_UpdatesAndReturnsId()
        {
            // Arrange
            var dto = new ServiceDto { Name = "S1" };
            const int expectedId = 5;

            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(
                    It.IsAny<string>(),
                    It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedId);

            _mockSecureData.Setup(s => s.Encrypt(It.IsAny<string>())).Returns<string>(s => s);

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
        public async Task UpsertAsync_NewService_Adds()
        {
            // Arrange
            var dto = new ServiceDto { Name = "NewService" };
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync((ServiceDto)null!);
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>())).ReturnsAsync(7);
            _mockSecureData.Setup(s => s.Encrypt(It.IsAny<string>())).Returns<string>(s => s);

            var repo = CreateRepository();

            // Act
            var rows = await repo.UpsertAsync(
                dto,
                preserveExistingRuntimeState: false,
                preserveExistingCredentials: false,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(7, rows);
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
        public async Task UpsertBatchAsync_NullServices_ReturnsZero()
        {
            // Arrange
            var repo = CreateRepository();

            // Act
            var result = await repo.UpsertBatchAsync(null!, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0, result);
            _mockDapper.Verify(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpsertBatchAsync_EmptyServices_ReturnsZero()
        {
            // Arrange
            var repo = CreateRepository();
            var services = new List<ServiceDto>();

            // Act
            var result = await repo.UpsertBatchAsync(services, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0, result);
            _mockDapper.Verify(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpsertBatchAsync_FullyPopulatedServices_MapsAllColumnsAndEncrypts()
        {
            // Arrange
            var repo = CreateRepository();

            var service = new ServiceDto
            {
                Name = "FullService",
                DisplayName = "Display Name",
                Description = "Description",
                Pid = 1234,
                ExecutablePath = "C:\\path.exe",
                StartupDirectory = "C:\\dir",
                Parameters = "--args",
                StartupType = 2,
                Priority = 1,
                StartTimeout = 45,
                StopTimeout = 45,
                RunAsLocalSystem = false,
                UserAccount = "User",
                Password = "plain_password",
                StdoutPath = "C:\\out.log",
                StderrPath = "C:\\err.log",
                EnableSizeRotation = true,
                RotationSize = 10,
                MaxRotations = 5,
                EnableDateRotation = true,
                DateRotationType = 1,
                UseLocalTimeForRotation = true,
                EnableHealthMonitoring = true,
                HeartbeatInterval = 30,
                MaxFailedChecks = 3,
                RecoveryAction = 1,
                MaxRestartAttempts = 5,
                EnvironmentVariables = "VAR=VAL",
                ServiceDependencies = "Dep1",
                EnableDebugLogs = true,
                PreLaunchExecutablePath = "C:\\pre.exe",
                PreLaunchStartupDirectory = "C:\\pre_dir",
                PreLaunchParameters = "--pre",
                PreLaunchEnvironmentVariables = "PRE_VAR=VAL",
                PreLaunchStdoutPath = "C:\\pre_out.log",
                PreLaunchStderrPath = "C:\\pre_err.log",
                PreLaunchTimeoutSeconds = 60,
                PreLaunchRetryAttempts = 2,
                PreLaunchIgnoreFailure = true,
                FailureProgramPath = "C:\\fail.exe",
                FailureProgramStartupDirectory = "C:\\fail_dir",
                FailureProgramParameters = "--fail",
                PostLaunchExecutablePath = "C:\\post.exe",
                PostLaunchStartupDirectory = "C:\\post_dir",
                PostLaunchParameters = "--post",
                PreStopExecutablePath = "C:\\pre_stop.exe",
                PreStopStartupDirectory = "C:\\pre_stop_dir",
                PreStopParameters = "--pre-stop",
                PreStopTimeoutSeconds = 15,
                PreStopLogAsError = true,
                PostStopExecutablePath = "C:\\post_stop.exe",
                PostStopStartupDirectory = "C:\\post_stop_dir",
                PostStopParameters = "--post-stop"
            };

            var services = new List<ServiceDto> { service };
            const string encryptedPrefix = "encrypted_";

            var mockTx = new Mock<IDbTransaction>();
            _mockDapper.Setup(d => d.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(mockTx.Object);

            _mockSecureData.Setup(s => s.Encrypt(It.IsAny<string>()))
                           .Returns((string input) => encryptedPrefix + input);

            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(1);

            _mockDapper.Setup(d => d.QueryAsync<(int Id, string Name)>(
                           It.Is<string>(sql => sql.Contains($"SELECT Id, Name FROM {SqlConstants.ServicesTableName}")),
                           It.IsAny<object>(),
                           It.IsAny<IDbTransaction>(),
                           It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<(int Id, string Name)> { (Id: 1, Name: "FullService") });

            // Act
            await repo.UpsertBatchAsync(services, TestContext.Current.CancellationToken);

            // Assert
            _mockDapper.Verify(d => d.ExecuteAsync(
                 It.Is<string>(sql =>
                     sql.Contains($"INSERT INTO {SqlConstants.ServicesTableName}") &&
                     sql.Contains("ON CONFLICT(Name COLLATE UNICODE_NOCASE) DO UPDATE SET") &&
                     sql.Contains("PreStopParameters = excluded.PreStopParameters") &&
                     sql.Contains("UseLocalTimeForRotation = excluded.UseLocalTimeForRotation")),
                 It.Is<IEnumerable<ServiceDto>>(list =>
                     list.Count() == 1 && VerifyAllProperties(list.First(), service, encryptedPrefix)),
                 It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);

            mockTx.Verify(t => t.Commit(), Times.Once);
        }

        private bool VerifyAllProperties(ServiceDto actual, ServiceDto expected, string enc)
        {
            bool coreOk = actual.Name == expected.Name &&
                          actual.DisplayName == expected.DisplayName &&
                          actual.Description == expected.Description &&
                          actual.Pid == expected.Pid;

            bool encryptedOk = actual.Password == (enc + expected.Password) &&
                               actual.Parameters == (enc + expected.Parameters) &&
                               actual.EnvironmentVariables == (enc + expected.EnvironmentVariables) &&
                               actual.FailureProgramParameters == (enc + expected.FailureProgramParameters) &&
                               actual.PreLaunchParameters == (enc + expected.PreLaunchParameters) &&
                               actual.PostLaunchParameters == (enc + expected.PostLaunchParameters) &&
                               actual.PreLaunchEnvironmentVariables == (enc + expected.PreLaunchEnvironmentVariables) &&
                               actual.PreStopParameters == (enc + expected.PreStopParameters) &&
                               actual.PostStopParameters == (enc + expected.PostStopParameters);

            bool executionOk = actual.ExecutablePath == expected.ExecutablePath &&
                               actual.StartupDirectory == expected.StartupDirectory &&
                               actual.StartupType == expected.StartupType &&
                               actual.Priority == expected.Priority &&
                               actual.StartTimeout == expected.StartTimeout &&
                               actual.StopTimeout == expected.StopTimeout;

            bool loggingOk = actual.StdoutPath == expected.StdoutPath &&
                             actual.StderrPath == expected.StderrPath &&
                             actual.EnableSizeRotation == expected.EnableSizeRotation &&
                             actual.RotationSize == expected.RotationSize &&
                             actual.MaxRotations == expected.MaxRotations &&
                             actual.EnableDateRotation == expected.EnableDateRotation &&
                             actual.DateRotationType == expected.DateRotationType &&
                             actual.UseLocalTimeForRotation == expected.UseLocalTimeForRotation;

            bool hooksOk = actual.EnableHealthMonitoring == expected.EnableHealthMonitoring &&
                           actual.HeartbeatInterval == expected.HeartbeatInterval &&
                           actual.PreLaunchExecutablePath == expected.PreLaunchExecutablePath &&
                           actual.PreStopExecutablePath == expected.PreStopExecutablePath &&
                           actual.PostStopExecutablePath == expected.PostStopExecutablePath &&
                           actual.PreStopLogAsError == expected.PreStopLogAsError;

            return coreOk && encryptedOk && executionOk && loggingOk && hooksOk;
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

        [Fact]
        public async Task DeleteAsync_ByName_ReturnsZero()
        {
            // Arrange
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
            var repo = CreateRepository();

            // Act
            var rows = await repo.DeleteAsync(string.Empty, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0, rows);
        }

        #endregion

        #region Retrieval Operations & Decryption

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
        public async Task GetByIdAsync_DecryptsPassword()
        {
            // Arrange
            var dto = CreateEncryptedServiceDto();
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            SetupDecryptPassthrough();
            var repo = CreateRepository();

            // Act
            var result = await repo.GetByIdAsync(1, true, TestContext.Current.CancellationToken);

            // Assert
            AssertDecryptedDtoProperties(result);
        }

        [Fact]
        public async Task GetByIdAsync_NullPassword()
        {
            // Arrange
            var dto = new ServiceDto { Id = 1, Password = null! };
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            var repo = CreateRepository();

            // Act
            var result = await repo.GetByIdAsync(1, true, TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(result!.Password);
        }

        [Fact]
        public async Task GetByIdAsync_EmptyPassword()
        {
            // Arrange
            var dto = new ServiceDto { Id = 1, Password = string.Empty };
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            var repo = CreateRepository();

            // Act
            var result = await repo.GetByIdAsync(1, true, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result!.Password);
            Assert.Empty(result.Password);
        }

        [Fact]
        public async Task GetByIdAsync_NullDto()
        {
            // Arrange
            ServiceDto dto = null!;
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(dto);

            var repo = CreateRepository();

            // Act
            var result = await repo.GetByIdAsync(1, true, TestContext.Current.CancellationToken);

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

        [Fact]
        public void GetByName_DecryptsPassword()
        {
            // Arrange
            var dto = CreateEncryptedServiceDto();
            _mockDapper.Setup(d => d.QuerySingleOrDefault<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>())).Returns(dto);

            SetupDecryptPassthrough();
            var repo = CreateRepository();

            // Act
            var result = repo.GetByName("S", true);

            // Assert
            AssertDecryptedDtoProperties(result);
        }

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
        public async Task GetServicePidAsync_ServiceIsStopped_ReturnsNull()
        {
            // Arrange
            var serviceName = "StoppedService";

            _mockDapper
                .Setup(e => e.QueryFirstOrDefaultAsync<int?>(
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
        public async Task GetServicePidAsync_ServiceDoesNotExist_ReturnsNull()
        {
            // Arrange
            var serviceName = "NonExistentService";

            _mockDapper
                .Setup(e => e.QueryFirstOrDefaultAsync<int?>(
                    It.IsAny<string>(),
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
                It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
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

            _mockDapper
                .Setup(e => e.QuerySingleOrDefaultAsync<ServiceConsoleStateDto?>(
                    It.Is<string>(sql =>
                        sql.Contains($"FROM {SqlConstants.ServicesTableName}") &&
                        sql.Contains("WHERE Name = @Name") &&
                        sql.Contains("LIMIT 1")),
                    It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceConsoleStateDto());

            var repo = CreateRepository();

            // Act
            await repo.GetServiceConsoleStateAsync(serviceName, TestContext.Current.CancellationToken);

            // Assert
            _mockDapper.Verify(e => e.QuerySingleOrDefaultAsync<ServiceConsoleStateDto?>(
                It.IsAny<string>(),
                It.Is<object>(p => p.GetType().GetProperty("Name") != null!), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()),
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
        public async Task Search_DecryptsPasswords()
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
        public async Task Search_NullKeyword()
        {
            // Arrange
            var list = new List<ServiceDto> { CreateEncryptedServiceDto() };
            _mockDapper
                .Setup(d => d.QueryAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);

            SetupDecryptPassthrough();
            var repo = CreateRepository();

            // Act
            var result = (await repo.SearchAsync(null!, true, TestContext.Current.CancellationToken)).ToList();

            // Assert
            Assert.Single(result);
            AssertDecryptedDtoProperties(result[0]);
        }

        #endregion

        #region Import/Export Tests

        [Fact]
        public async Task ExportXML_ReturnsEmptyString()
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
        public async Task ExportXML_ReturnsSerializedService()
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
            Assert.Contains("<ServiceDto", xml);
            Assert.DoesNotContain("pwd1", xml);
        }

        [Fact]
        public async Task ImportXML_ValidXml_ReturnsTrue()
        {
            // Arrange
            var dto = new ServiceDto { Name = "A" };
            var repo = CreateRepository();
            var xml = $"<ServiceDto><Name>{dto.Name}</Name></ServiceDto>";

            _mockXmlServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Returns(dto);
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<int>(It.IsAny<CommandDefinition>())).ReturnsAsync(0);
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await repo.ImportXmlAsync(xml, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ImportXML_EmptyXml_ReturnsFalse()
        {
            // Arrange
            var repo = CreateRepository();
            var xml = string.Empty;

            // Act
            var result = await repo.ImportXmlAsync(xml, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ImportXML_InvalidXml_ReturnsFalse()
        {
            // Arrange
            var repo = CreateRepository();
            var xml = "<ServiceDto><Name></Invalid></ServiceDto>";
            _mockXmlServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Throws<Exception>();

            // Act
            var result = await repo.ImportXmlAsync(xml, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ImportXML_ServiceNull_ReturnsFalse()
        {
            // Arrange
            var repo = CreateRepository();
            var xml = "<ServiceDto></ServiceDto>";

            _mockXmlServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Returns((ServiceDto)null!);

            // Act
            var result = await repo.ImportXmlAsync(xml, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ExportJSON_ReturnsEmptyString()
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
        public async Task ExportJSON_ReturnsSerializedService()
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
            Assert.Contains("\"Name\": \"A\"", json);
        }

        [Fact]
        public async Task ImportJSON_ValidJson_ReturnsTrue()
        {
            // Arrange
            var dto = new ServiceDto { Name = "A" };
            var repo = CreateRepository();
            var json = "{\"Name\":\"A\"}";

            _mockJsonServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Returns(dto);
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<int>(It.IsAny<CommandDefinition>())).ReturnsAsync(0);
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await repo.ImportJsonAsync(json, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ImportJSON_EmptyJson_ReturnsFalse()
        {
            // Arrange
            var repo = CreateRepository();
            var json = string.Empty;

            // Act
            var result = await repo.ImportJsonAsync(json, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ImportJSON_NullObject_ReturnsFalse()
        {
            // Arrange
            var repo = CreateRepository();
            var json = "null!";

            // Act
            var result = await repo.ImportJsonAsync(json, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ImportJSON_InvalidJson_ReturnsFalse()
        {
            // Arrange
            var repo = CreateRepository();
            var json = "{ invalid json }";

            // Act
            var result = await repo.ImportJsonAsync(json, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ImportJSON_Throws_ReturnsFalse()
        {
            // Arrange
            var repo = CreateRepository();
            var json = "{ invalid json }";
            _mockJsonServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Throws<Exception>();

            // Act
            var result = await repo.ImportJsonAsync(json, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Private Helper Branch Coverage Tests

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
            await repo.UpdateAsync(incoming, preserveExistingRuntimeState: true, preserveExistingCredentials: false, TestContext.Current.CancellationToken);

            // Assert
            _mockDapper.Verify(d => d.ExecuteAsync(It.IsAny<string>(), It.Is<ServiceDto>(s => s.Pid == 9999 && s.ActiveStdoutPath == "db.log"), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void PatchRuntimeState_ExistingNotNull_ExecutesApplyRuntimeState()
        {
            // Arrange
            var repo = CreateRepository();
            var incoming = new ServiceDto { Name = "TargetSyncService", Pid = 0 };
            var databaseMatch = new ServiceDto { Name = "TargetSyncService", Pid = 8888, ActiveStderrPath = "err.log" };

            _mockDapper.Setup(d => d.QuerySingleOrDefault<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>()))
                       .Returns(databaseMatch);

            // Act
            repo.Update(incoming, preserveExistingRuntimeState: true, preserveExistingCredentials: false);

            // Assert
            _mockDapper.Verify(d => d.Execute(It.IsAny<string>(), It.Is<ServiceDto>(s => s.Pid == 8888 && s.ActiveStderrPath == "err.log"), It.IsAny<IDbTransaction>()), Times.Once);
        }

        [Theory]
        [InlineData(true, true)]   // Covers Both State + Credentials branches
        [InlineData(true, false)]  // Covers State branch only
        [InlineData(false, true)]  // Covers Credentials branch only
        [InlineData(false, false)] // Covers neither 
        public void ApplyRuntimeState_AllCombinationsAndBranches_Covered(bool preserveState, bool preserveCredentials)
        {
            // Arrange
            var incoming = new ServiceDto { Name = "Test", Pid = 0, Password = "new_password" };
            var existing = new ServiceDto { Name = "Test", Pid = 555, Password = "old_password", ActiveStdoutPath = "active.log" };

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
                Assert.Equal("active.log", incoming.ActiveStdoutPath);
            }
            else
            {
                Assert.Equal(0, incoming.Pid);
                Assert.Null(incoming.ActiveStdoutPath);
            }

            if (preserveCredentials)
            {
                Assert.Equal("old_password", incoming.Password);
            }
            else
            {
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
                 repo.AddAsync(dto, TestContext.Current.CancellationToken));

            Assert.Contains("Encryption failed for field", wrapperEx.Message);
            Assert.NotNull(wrapperEx.InnerException);
            Assert.IsType<CryptographicException>(wrapperEx.InnerException);
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
            var poisonDto = new ServiceDto { Id = 77, Name = "PoisonRow", Description = "Original Description", Password = "poison_payload" };

            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(poisonDto);

            _mockSecureData.Setup(s => s.Decrypt(It.IsAny<string>()))
                           .Throws(new TimeoutException("Cryptographic subsystem timed out."));

            // Act
            var result = await repo.GetByIdAsync(77, decrypt: true, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("[DECRYPTION FAILED: TimeoutException]", result.Description);

            Assert.Null(result.Password);
            Assert.Null(result.Parameters);
            Assert.Null(result.EnvironmentVariables);
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
            var freshDto = new ServiceDto { Name = "Row2", Description = "Meta", Password = "ABC" };
            TestReflection.InvokeNonPublic(repo, "HandleCorruptServiceDecryption", freshDto, targetExWithInner);

            Assert.Contains("[DECRYPTION FAILED: UnauthorizedAccessException]", freshDto.Description);
            Assert.Null(freshDto.Password);
            Assert.Null(freshDto.Parameters);
        }

        #endregion
    }
}
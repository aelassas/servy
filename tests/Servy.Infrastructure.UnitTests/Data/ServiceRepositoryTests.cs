using Dapper;
using Moq;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Infrastructure.UnitTests.Data
{
    public class ServiceRepositoryTests
    {
        private readonly Mock<IDapperExecutor> _mockDapper;
        private readonly Mock<ISecureData> _mockSecureData;
        private readonly Mock<IXmlServiceSerializer> _mockXmlServiceSerializer;
        private readonly Mock<IJsonServiceSerializer> _mockJsonServiceSerializer;

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

        [Fact]
        public void Constructor_NullDapper_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(null, _mockSecureData.Object, _mockXmlServiceSerializer.Object, _mockJsonServiceSerializer.Object));
        }

        [Fact]
        public void Constructor_NullSecureData_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(_mockDapper.Object, null, _mockXmlServiceSerializer.Object, _mockJsonServiceSerializer.Object));
        }

        [Fact]
        public void Constructor_NullXmlServiceSerializer_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(_mockDapper.Object, _mockSecureData.Object, null, _mockJsonServiceSerializer.Object));
        }

        [Fact]
        public void Constructor_NullJsonServiceSerializer_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(_mockDapper.Object, _mockSecureData.Object, _mockXmlServiceSerializer.Object, null));
        }

        [Fact]
        public async Task AddAsync_FullSecurityAudit_EncryptsAllNineSensitiveFields()
        {
            // 1. Arrange - Define unique plain-text values for every sensitive field
            var dto = new ServiceDto
            {
                Name = "AuditService",
                Password = "p_plain",
                Parameters = "args_plain",
                EnvironmentVariables = "env_plain",
                FailureProgramParameters = "fail_args_plain",
                PreLaunchParameters = "pre_args_plain",
                PreLaunchEnvironmentVariables = "pre_env_plain",
                PostLaunchParameters = "post_args_plain",
                PreStopParameters = "pre_stop_plain",
                PostStopParameters = "post_stop_plain"
            };

            // 2. Setup Mocks - One-to-one mapping for verification
            _mockSecureData.Setup(s => s.Encrypt("p_plain")).Returns("p_enc");
            _mockSecureData.Setup(s => s.Encrypt("args_plain")).Returns("args_enc");
            _mockSecureData.Setup(s => s.Encrypt("env_plain")).Returns("env_enc");
            _mockSecureData.Setup(s => s.Encrypt("fail_args_plain")).Returns("fail_args_enc");
            _mockSecureData.Setup(s => s.Encrypt("pre_args_plain")).Returns("pre_args_enc");
            _mockSecureData.Setup(s => s.Encrypt("pre_env_plain")).Returns("pre_env_enc");
            _mockSecureData.Setup(s => s.Encrypt("post_args_plain")).Returns("post_args_enc");
            _mockSecureData.Setup(s => s.Encrypt("pre_stop_plain")).Returns("pre_stop_enc");
            _mockSecureData.Setup(s => s.Encrypt("post_stop_plain")).Returns("post_stop_enc");

            // Setup callback to capture the anonymous parameters passed to Dapper
            object capturedParam = null;
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                       .Callback<string, object, IDbTransaction, CancellationToken>((sql, param, _, token) => capturedParam = param)
                       .ReturnsAsync(99);

            var repo = CreateRepository();

            // 3. Act
            var result = await repo.AddAsync(dto, CancellationToken.None);

            // 4. Assert - Functional Result
            Assert.Equal(99, result);
            Assert.Equal(99, dto.Id);

            // 5. Assert - Side-Effect Protection
            Assert.Equal("p_plain", dto.Password);
            Assert.Equal("env_plain", dto.EnvironmentVariables);
            Assert.Equal("pre_stop_plain", dto.PreStopParameters);

            // 6. Verify - Ensure the object sent to Dapper was the encrypted clone
            Assert.NotNull(capturedParam);
            var paramDict = capturedParam.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(capturedParam));

            Assert.Equal("p_enc", paramDict["Password"]);
            Assert.Equal("args_enc", paramDict["Parameters"]);
            Assert.Equal("env_enc", paramDict["EnvironmentVariables"]);
            Assert.Equal("fail_args_enc", paramDict["FailureProgramParameters"]);
            Assert.Equal("pre_args_enc", paramDict["PreLaunchParameters"]);
            Assert.Equal("pre_env_enc", paramDict["PreLaunchEnvironmentVariables"]);
            Assert.Equal("post_args_enc", paramDict["PostLaunchParameters"]);
            Assert.Equal("pre_stop_enc", paramDict["PreStopParameters"]);
            Assert.Equal("post_stop_enc", paramDict["PostStopParameters"]);
        }

        [Fact]
        public async Task UpdateAsync_FullSecurityAudit_EncryptsAllNineSensitiveFields()
        {
            // 1. Arrange - Unique plain-text values for every sensitive field
            var dto = new ServiceDto
            {
                Id = 123,
                Name = "UpdateAuditService",
                Password = "p_plain",
                Parameters = "args_plain",
                EnvironmentVariables = "env_plain",
                FailureProgramParameters = "fail_args_plain",
                PreLaunchParameters = "pre_args_plain",
                PreLaunchEnvironmentVariables = "pre_env_plain",
                PostLaunchParameters = "post_args_plain",
                PreStopParameters = "pre_stop_plain",
                PostStopParameters = "post_stop_plain"
            };

            // 2. Setup Mocks - One-to-one mapping for verification
            _mockSecureData.Setup(s => s.Encrypt("p_plain")).Returns("p_enc");
            _mockSecureData.Setup(s => s.Encrypt("args_plain")).Returns("args_enc");
            _mockSecureData.Setup(s => s.Encrypt("env_plain")).Returns("env_enc");
            _mockSecureData.Setup(s => s.Encrypt("fail_args_plain")).Returns("fail_args_enc");
            _mockSecureData.Setup(s => s.Encrypt("pre_args_plain")).Returns("pre_args_enc");
            _mockSecureData.Setup(s => s.Encrypt("pre_env_plain")).Returns("pre_env_enc");
            _mockSecureData.Setup(s => s.Encrypt("post_args_plain")).Returns("post_args_enc");
            _mockSecureData.Setup(s => s.Encrypt("pre_stop_plain")).Returns("pre_stop_enc");
            _mockSecureData.Setup(s => s.Encrypt("post_stop_plain")).Returns("post_stop_enc");

            object capturedParam = null;
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                       .Callback<string, object, IDbTransaction, CancellationToken>((sql, param, _, token) => capturedParam = param)
                       .ReturnsAsync(1);

            var repo = CreateRepository();

            // 3. Act
            var rowsAffected = await repo.UpdateAsync(
                dto,
                preserveExistingRuntimeState: false,
                preserveExistingCredentials: false,
                CancellationToken.None);

            // 4. Assert - Functional Results
            Assert.Equal(1, rowsAffected);

            // 5. Assert - Side-Effect Protection
            Assert.Equal("p_plain", dto.Password);
            Assert.Equal("env_plain", dto.EnvironmentVariables);
            Assert.Equal("pre_stop_plain", dto.PreStopParameters);

            // 6. Verify - Ensure the object sent to Dapper was the encrypted clone
            Assert.NotNull(capturedParam);
            var paramDict = capturedParam.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(capturedParam));

            Assert.Equal(123, paramDict["Id"]);
            Assert.Equal("p_enc", paramDict["Password"]);
            Assert.Equal("args_enc", paramDict["Parameters"]);
            Assert.Equal("env_enc", paramDict["EnvironmentVariables"]);
            Assert.Equal("fail_args_enc", paramDict["FailureProgramParameters"]);
            Assert.Equal("pre_args_enc", paramDict["PreLaunchParameters"]);
            Assert.Equal("pre_env_enc", paramDict["PreLaunchEnvironmentVariables"]);
            Assert.Equal("post_args_enc", paramDict["PostLaunchParameters"]);
            Assert.Equal("pre_stop_enc", paramDict["PreStopParameters"]);
            Assert.Equal("post_stop_enc", paramDict["PostStopParameters"]);
        }

        [Fact]
        public void Update_FullSecurityAudit_EncryptsAllNineSensitiveFields()
        {
            // 1. Arrange - Unique plain-text values for every sensitive field
            var dto = new ServiceDto
            {
                Id = 123,
                Name = "UpdateAuditService",
                Password = "p_plain",
                Parameters = "args_plain",
                EnvironmentVariables = "env_plain",
                FailureProgramParameters = "fail_args_plain",
                PreLaunchParameters = "pre_args_plain",
                PreLaunchEnvironmentVariables = "pre_env_plain",
                PostLaunchParameters = "post_args_plain",
                PreStopParameters = "pre_stop_plain",
                PostStopParameters = "post_stop_plain"
            };

            // 2. Setup Mocks - One-to-one mapping for verification
            _mockSecureData.Setup(s => s.Encrypt("p_plain")).Returns("p_enc");
            _mockSecureData.Setup(s => s.Encrypt("args_plain")).Returns("args_enc");
            _mockSecureData.Setup(s => s.Encrypt("env_plain")).Returns("env_enc");
            _mockSecureData.Setup(s => s.Encrypt("fail_args_plain")).Returns("fail_args_enc");
            _mockSecureData.Setup(s => s.Encrypt("pre_args_plain")).Returns("pre_args_enc");
            _mockSecureData.Setup(s => s.Encrypt("pre_env_plain")).Returns("pre_env_enc");
            _mockSecureData.Setup(s => s.Encrypt("post_args_plain")).Returns("post_args_enc");
            _mockSecureData.Setup(s => s.Encrypt("pre_stop_plain")).Returns("pre_stop_enc");
            _mockSecureData.Setup(s => s.Encrypt("post_stop_plain")).Returns("post_stop_enc");

            object capturedParam = null;
            _mockDapper.Setup(d => d.Execute(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>()))
                       .Callback<string, object, IDbTransaction>((sql, param, _) => capturedParam = param)
                       .Returns(1);

            var repo = CreateRepository();

            // 3. Act
            var rowsAffected = repo.Update(
                dto,
                preserveExistingRuntimeState: false,
                preserveExistingCredentials: false
                );

            // 4. Assert - Functional Results
            Assert.Equal(1, rowsAffected);

            // 5. Assert - Side-Effect Protection
            Assert.Equal("p_plain", dto.Password);
            Assert.Equal("env_plain", dto.EnvironmentVariables);
            Assert.Equal("pre_stop_plain", dto.PreStopParameters);

            // 6. Verify - Ensure the object sent to Dapper was the encrypted clone
            Assert.NotNull(capturedParam);
            var paramDict = capturedParam.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(capturedParam));

            Assert.Equal(123, paramDict["Id"]);
            Assert.Equal("p_enc", paramDict["Password"]);
            Assert.Equal("args_enc", paramDict["Parameters"]);
            Assert.Equal("env_enc", paramDict["EnvironmentVariables"]);
            Assert.Equal("fail_args_enc", paramDict["FailureProgramParameters"]);
            Assert.Equal("pre_args_enc", paramDict["PreLaunchParameters"]);
            Assert.Equal("pre_env_enc", paramDict["PreLaunchEnvironmentVariables"]);
            Assert.Equal("post_args_enc", paramDict["PostLaunchParameters"]);
            Assert.Equal("pre_stop_enc", paramDict["PreStopParameters"]);
            Assert.Equal("post_stop_enc", paramDict["PostStopParameters"]);
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
                CancellationToken.None);

            // Assert
            Assert.Equal(expectedId, resultId);
            Assert.Equal(expectedId, dto.Id);
        }

        [Fact]
        public async Task UpsertAsync_NewService_Adds()
        {
            var dto = new ServiceDto { Name = "NewService" };
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync((ServiceDto)null);
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>())).ReturnsAsync(7);
            _mockSecureData.Setup(s => s.Encrypt(It.IsAny<string>())).Returns<string>(s => s);

            var repo = CreateRepository();
            var rows = await repo.UpsertAsync(
                dto,
                preserveExistingRuntimeState: false,
                preserveExistingCredentials: false,
                CancellationToken.None);

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

            object capturedParam = null;
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
                CancellationToken.None);

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
            var result = await repo.UpsertBatchAsync(null, CancellationToken.None);

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
            var result = await repo.UpsertBatchAsync(services, CancellationToken.None);

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
            _mockDapper.Setup(d => d.BeginTransaction()).Returns(mockTx.Object);

            _mockSecureData.Setup(s => s.Encrypt(It.IsAny<string>()))
                           .Returns((string input) => encryptedPrefix + input);

            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(1);

            _mockDapper.Setup(d => d.QueryAsync<(int Id, string Name)>(
                           It.Is<string>(sql => sql.Contains("SELECT Id, Name FROM Services")),
                           It.IsAny<object>(),
                           It.IsAny<IDbTransaction>(),
                           It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new List<(int Id, string Name)> { (Id: 1, Name: "FullService") });

            // Act
            await repo.UpsertBatchAsync(services, CancellationToken.None);

            // Assert
            _mockDapper.Verify(d => d.ExecuteAsync(
                 It.Is<string>(sql =>
                     sql.Contains("INSERT INTO Services") &&
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
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var repo = CreateRepository();
            var rows = await repo.DeleteAsync(10, CancellationToken.None);

            Assert.Equal(1, rows);
        }

        [Fact]
        public async Task DeleteAsync_ByName_ReturnsAffectedRows()
        {
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var repo = CreateRepository();
            var rows = await repo.DeleteAsync("ServiceName", CancellationToken.None);

            Assert.Equal(1, rows);
        }

        [Fact]
        public async Task DeleteAsync_ByName_ReturnsZero()
        {
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var repo = CreateRepository();
            var rows = await repo.DeleteAsync(string.Empty, CancellationToken.None);

            Assert.Equal(0, rows);
        }

        [Fact]
        public async Task GetByIdAsync_DecryptsPassword()
        {
            var dto = new ServiceDto
            {
                Id = 1,
                Parameters = "encrypted_params",
                FailureProgramParameters = "encrypted_failure_prog_params",
                PreLaunchParameters = "encrypted_pre_launch_params",
                PostLaunchParameters = "encrypted_post_launch_params",
                Password = "encrypted",
                EnvironmentVariables = "encrypted_vars",
                PreLaunchEnvironmentVariables = "encrypted_pre_vars",
                PreStopParameters = "encrypted_pre_stop_params",
                PostStopParameters = "encrypted_post_stop_params",
            };
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);
            _mockSecureData.Setup(s => s.Decrypt("encrypted")).Returns("plain");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_vars")).Returns("v1=val1;v2=val2");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_vars")).Returns("v3=val3");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_params")).Returns("params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_failure_prog_params")).Returns("failure-prog-params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_launch_params")).Returns("pre-launch-params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_post_launch_params")).Returns("post-launch-params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_stop_params")).Returns("pre-stop-params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_post_stop_params")).Returns("post-stop-params");

            var repo = CreateRepository();
            var result = await repo.GetByIdAsync(1, true, CancellationToken.None);

            Assert.Equal("params", result.Parameters);
            Assert.Equal("failure-prog-params", result.FailureProgramParameters);
            Assert.Equal("pre-launch-params", result.PreLaunchParameters);
            Assert.Equal("post-launch-params", result.PostLaunchParameters);
            Assert.Equal("plain", result.Password);
            Assert.Equal("v1=val1;v2=val2", result.EnvironmentVariables);
            Assert.Equal("v3=val3", result.PreLaunchEnvironmentVariables);
            Assert.Equal("pre-stop-params", result.PreStopParameters);
            Assert.Equal("post-stop-params", result.PostStopParameters);
        }

        [Fact]
        public async Task GetByIdAsync_EmptyPassword()
        {
            var dto = new ServiceDto { Id = 1, Password = null };
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            var repo = CreateRepository();
            var result = await repo.GetByIdAsync(1, true, CancellationToken.None);

            Assert.Null(result.Password);
        }

        [Fact]
        public async Task GetByIdAsync_NullDto()
        {
            ServiceDto dto = null;
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(dto);

            var repo = CreateRepository();
            var result = await repo.GetByIdAsync(1, true, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetByNameAsync_DecryptsPassword()
        {
            var dto = new ServiceDto
            {
                Name = "S",
                Parameters = "encrypted_params",
                FailureProgramParameters = "encrypted_failure_prog_params",
                PreLaunchParameters = "encrypted_pre_launch_params",
                PostLaunchParameters = "encrypted_post_launch_params",
                Password = "encrypted",
                EnvironmentVariables = "encrypted_vars",
                PreLaunchEnvironmentVariables = "encrypted_pre_vars",
                PreStopParameters = "encrypted_pre_stop_params",
                PostStopParameters = "encrypted_post_stop_params",
            };
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);
            _mockSecureData.Setup(s => s.Decrypt("encrypted")).Returns("plain");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_vars")).Returns("v1=val1;v2=val2");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_vars")).Returns("v3=val3");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_params")).Returns("params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_failure_prog_params")).Returns("failure-prog-params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_launch_params")).Returns("pre-launch-params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_post_launch_params")).Returns("post-launch-params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_stop_params")).Returns("pre-stop-params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_post_stop_params")).Returns("post-stop-params");

            var repo = CreateRepository();
            var result = await repo.GetByNameAsync("S", true, CancellationToken.None);

            Assert.Equal("params", result.Parameters);
            Assert.Equal("failure-prog-params", result.FailureProgramParameters);
            Assert.Equal("pre-launch-params", result.PreLaunchParameters);
            Assert.Equal("post-launch-params", result.PostLaunchParameters);
            Assert.Equal("plain", result.Password);
            Assert.Equal("v1=val1;v2=val2", result.EnvironmentVariables);
            Assert.Equal("v3=val3", result.PreLaunchEnvironmentVariables);
            Assert.Equal("pre-stop-params", result.PreStopParameters);
            Assert.Equal("post-stop-params", result.PostStopParameters);
        }

        [Fact]
        public void GetByName_DecryptsPassword()
        {
            var dto = new ServiceDto
            {
                Name = "S",
                Parameters = "encrypted_params",
                FailureProgramParameters = "encrypted_failure_prog_params",
                PreLaunchParameters = "encrypted_pre_launch_params",
                PostLaunchParameters = "encrypted_post_launch_params",
                Password = "encrypted",
                EnvironmentVariables = "encrypted_vars",
                PreLaunchEnvironmentVariables = "encrypted_pre_vars",
                PreStopParameters = "encrypted_pre_stop_params",
                PostStopParameters = "encrypted_post_stop_params",
            };
            _mockDapper.Setup(d => d.QuerySingleOrDefault<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>())).Returns(dto);
            _mockSecureData.Setup(s => s.Decrypt("encrypted")).Returns("plain");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_vars")).Returns("v1=val1;v2=val2");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_vars")).Returns("v3=val3");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_params")).Returns("params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_failure_prog_params")).Returns("failure-prog-params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_launch_params")).Returns("pre-launch-params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_post_launch_params")).Returns("post-launch-params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_stop_params")).Returns("pre-stop-params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_post_stop_params")).Returns("post-stop-params");

            var repo = CreateRepository();
            var result = repo.GetByName("S", true);

            Assert.Equal("params", result.Parameters);
            Assert.Equal("failure-prog-params", result.FailureProgramParameters);
            Assert.Equal("pre-launch-params", result.PreLaunchParameters);
            Assert.Equal("post-launch-params", result.PostLaunchParameters);
            Assert.Equal("plain", result.Password);
            Assert.Equal("v1=val1;v2=val2", result.EnvironmentVariables);
            Assert.Equal("v3=val3", result.PreLaunchEnvironmentVariables);
            Assert.Equal("pre-stop-params", result.PreStopParameters);
            Assert.Equal("post-stop-params", result.PostStopParameters);
        }

        [Fact]
        public async Task GetServicePidAsync_ServiceIsRunning_ReturnsPid()
        {
            var serviceName = "RunningService";
            int expectedPid = 1234;

            _mockDapper
                .Setup(e => e.QuerySingleOrDefaultAsync<int?>(
                    It.Is<string>(sql => sql.Contains("SELECT Pid FROM Services")),
                    It.Is<object>(p => p.GetType().GetProperty("Name").GetValue(p).ToString() == serviceName), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedPid);

            var repo = CreateRepository();
            var result = await repo.GetServicePidAsync(serviceName, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(expectedPid, result);
            _mockDapper.VerifyAll();
        }

        [Fact]
        public async Task GetServicePidAsync_ServiceIsStopped_ReturnsNull()
        {
            var serviceName = "StoppedService";

            _mockDapper
                .Setup(e => e.QueryFirstOrDefaultAsync<int?>(
                    It.Is<string>(sql => sql.Contains("SELECT Pid FROM Services")),
                    It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int?)null);

            var repo = CreateRepository();
            var result = await repo.GetServicePidAsync(serviceName, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetServicePidAsync_ServiceDoesNotExist_ReturnsNull()
        {
            var serviceName = "NonExistentService";

            _mockDapper
                .Setup(e => e.QueryFirstOrDefaultAsync<int?>(
                    It.IsAny<string>(),
                    It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int?)null);

            var repo = CreateRepository();
            var result = await repo.GetServicePidAsync(serviceName, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetServicePidAsync_PassesCancellationToken()
        {
            var serviceName = "TestService";

            var repo = CreateRepository();
            await repo.GetServicePidAsync(serviceName, CancellationToken.None);

            _mockDapper.Verify(e => e.QuerySingleOrDefaultAsync<int?>(
                It.IsAny<string>(),
                It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetServiceConsoleStateAsync_ServiceExists_ReturnsLightweightDto()
        {
            var serviceName = "ConsoleTestService";
            var expectedState = new ServiceConsoleStateDto
            {
                Pid = 5678,
                ActiveStdoutPath = @"C:\Logs\stdout.log",
                ActiveStderrPath = @"C:\Logs\stderr.log"
            };

            _mockDapper
                .Setup(e => e.QuerySingleOrDefaultAsync<ServiceConsoleStateDto>(
                    It.Is<string>(sql => sql.Contains("SELECT Pid, ActiveStdoutPath, ActiveStderrPath")),
                    It.Is<object>(p => p.GetType().GetProperty("Name").GetValue(p).ToString() == serviceName), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedState);

            var repo = CreateRepository();
            var result = await repo.GetServiceConsoleStateAsync(serviceName, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(expectedState.Pid, result.Pid);
            Assert.Equal(expectedState.ActiveStdoutPath, result.ActiveStdoutPath);
            Assert.Equal(expectedState.ActiveStderrPath, result.ActiveStderrPath);
            _mockDapper.VerifyAll();
        }

        [Fact]
        public async Task GetServiceConsoleStateAsync_ServiceDoesNotExist_ReturnsNull()
        {
            var serviceName = "MissingService";

            _mockDapper
                .Setup(e => e.QueryFirstOrDefaultAsync<ServiceConsoleStateDto>(
                    It.IsAny<string>(),
                    It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ServiceConsoleStateDto)null);

            var repo = CreateRepository();
            var result = await repo.GetServiceConsoleStateAsync(serviceName, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetServiceConsoleStateAsync_VerifiesSqlParametersAndStructure()
        {
            var serviceName = "SqlVerifyService";

            _mockDapper
                .Setup(e => e.QuerySingleOrDefaultAsync<ServiceConsoleStateDto>(
                    It.Is<string>(sql =>
                        sql.Contains("FROM Services") &&
                        sql.Contains("WHERE Name = @Name") &&
                        sql.Contains("LIMIT 1")),
                    It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServiceConsoleStateDto());

            var repo = CreateRepository();
            await repo.GetServiceConsoleStateAsync(serviceName, CancellationToken.None);

            _mockDapper.Verify(e => e.QuerySingleOrDefaultAsync<ServiceConsoleStateDto>(
                It.IsAny<string>(),
                It.Is<object>(p => p.GetType().GetProperty("Name") != null), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetAllAsync_DecryptsAll()
        {
            var list = new List<ServiceDto>
            {
                new ServiceDto {
                    Id = 1,
                    Parameters = "encrypted_params1",
                    FailureProgramParameters = "encrypted_failure_prog_params1",
                    PreLaunchParameters = "encrypted_pre_launch_params1",
                    PostLaunchParameters = "encrypted_post_launch_params1",
                    Password = "e1",
                    EnvironmentVariables = "encrypted_vars1",
                    PreLaunchEnvironmentVariables = "encrypted_pre_vars1",
                    PreStopParameters = "encrypted_pre_stop_params1",
                    PostStopParameters = "encrypted_post_stop_params1",
                },
                new ServiceDto {
                    Id = 2,
                    Parameters = "encrypted_params2",
                    FailureProgramParameters = "encrypted_failure_prog_params2",
                    PreLaunchParameters = "encrypted_pre_launch_params2",
                    PostLaunchParameters = "encrypted_post_launch_params2",
                    Password = "e2",
                    EnvironmentVariables = "encrypted_vars2",
                    PreLaunchEnvironmentVariables = "encrypted_pre_vars2",
                    PreStopParameters = "encrypted_pre_stop_params2",
                    PostStopParameters = "encrypted_post_stop_params2",
                }
            };

            _mockDapper
                .Setup(d => d.QueryAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);

            _mockSecureData.Setup(s => s.Decrypt("e1")).Returns("p1");
            _mockSecureData.Setup(s => s.Decrypt("e2")).Returns("p2");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_vars1")).Returns("vars1");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_vars2")).Returns("vars2");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_vars1")).Returns("pre_vars1");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_vars2")).Returns("pre_vars2");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_params1")).Returns("params1");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_failure_prog_params1")).Returns("failure-prog-params1");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_launch_params1")).Returns("pre-launch-params1");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_post_launch_params1")).Returns("post-launch-params1");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_params2")).Returns("params2");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_failure_prog_params2")).Returns("failure-prog-params2");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_launch_params2")).Returns("pre-launch-params2");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_post_launch_params2")).Returns("post-launch-params2");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_stop_params1")).Returns("pre_stop_params1");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_stop_params2")).Returns("pre_stop_params2");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_post_stop_params1")).Returns("post_stop_params1");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_post_stop_params2")).Returns("post_stop_params2");

            var repo = CreateRepository();
            var result = (await repo.GetAllAsync(true, CancellationToken.None)).ToList();

            Assert.Collection(result,
                r =>
                {
                    Assert.Equal("params1", r.Parameters);
                    Assert.Equal("failure-prog-params1", r.FailureProgramParameters);
                    Assert.Equal("pre-launch-params1", r.PreLaunchParameters);
                    Assert.Equal("post-launch-params1", r.PostLaunchParameters);
                    Assert.Equal("p1", r.Password);
                    Assert.Equal("vars1", r.EnvironmentVariables);
                    Assert.Equal("pre_vars1", r.PreLaunchEnvironmentVariables);
                    Assert.Equal("pre_stop_params1", r.PreStopParameters);
                    Assert.Equal("post_stop_params1", r.PostStopParameters);
                },
                r =>
                {
                    Assert.Equal("params2", r.Parameters);
                    Assert.Equal("failure-prog-params2", r.FailureProgramParameters);
                    Assert.Equal("pre-launch-params2", r.PreLaunchParameters);
                    Assert.Equal("post-launch-params2", r.PostLaunchParameters);
                    Assert.Equal("p2", r.Password);
                    Assert.Equal("vars2", r.EnvironmentVariables);
                    Assert.Equal("pre_vars2", r.PreLaunchEnvironmentVariables);
                    Assert.Equal("pre_stop_params2", r.PreStopParameters);
                    Assert.Equal("post_stop_params2", r.PostStopParameters);
                }
            );
        }

        [Fact]
        public async Task Search_DecryptsPasswords()
        {
            var list = new List<ServiceDto> { new ServiceDto { Name = "A", Password = "e1" } };
            _mockDapper
                .Setup(d => d.QueryAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);
            _mockSecureData.Setup(s => s.Decrypt("e1")).Returns("p1");

            var repo = CreateRepository();
            var result = (await repo.SearchAsync("A", true, CancellationToken.None)).ToList();

            Assert.Single(result);
            Assert.Equal("p1", result[0].Password);
        }

        [Fact]
        public async Task Search_NullKeyword()
        {
            var list = new List<ServiceDto>
            {
                new ServiceDto {
                    Name = "A",
                    Parameters = "encrypted_params",
                    FailureProgramParameters = "encrypted_failure_prog_params",
                    PreLaunchParameters = "encrypted_pre_launch_params",
                    PostLaunchParameters = "encrypted_post_launch_params",
                    Password = "e1",
                    EnvironmentVariables = "encrypted_vars",
                    PreLaunchEnvironmentVariables = "encrypted_pre_vars",
                    PreStopParameters = "encrypted_pre_stop_params",
                    PostStopParameters = "encrypted_post_stop_params",
                }
            };
            _mockDapper
                .Setup(d => d.QueryAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);
            _mockSecureData.Setup(s => s.Decrypt("e1")).Returns("p1");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_vars")).Returns("vars");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_vars")).Returns("pre_vars");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_params")).Returns("params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_failure_prog_params")).Returns("failure-prog-params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_launch_params")).Returns("pre-launch-params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_post_launch_params")).Returns("post-launch-params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_pre_stop_params")).Returns("pre_stop_params");
            _mockSecureData.Setup(s => s.Decrypt("encrypted_post_stop_params")).Returns("post_stop_params");

            var repo = CreateRepository();
            var result = (await repo.SearchAsync(null, true, CancellationToken.None)).ToList();

            Assert.Single(result);
            Assert.Equal("params", result[0].Parameters);
            Assert.Equal("failure-prog-params", result[0].FailureProgramParameters);
            Assert.Equal("pre-launch-params", result[0].PreLaunchParameters);
            Assert.Equal("post-launch-params", result[0].PostLaunchParameters);
            Assert.Equal("p1", result[0].Password);
            Assert.Equal("vars", result[0].EnvironmentVariables);
            Assert.Equal("pre_vars", result[0].PreLaunchEnvironmentVariables);
            Assert.Equal("pre_stop_params", result[0].PreStopParameters);
            Assert.Equal("post_stop_params", result[0].PostStopParameters);
        }

        #region Import/Export Tests

        [Fact]
        public async Task ExportXML_ReturnsEmptyString()
        {
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>()))
                .ReturnsAsync((ServiceDto)null);

            var repo = CreateRepository();

            var xml = await repo.ExportXmlAsync("A", CancellationToken.None);

            Assert.Empty(xml);
        }

        [Fact]
        public async Task ExportXML_ReturnsSerializedService()
        {
            var dto = new ServiceDto { Name = "A", Password = "p1" };

            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            _mockSecureData
                .Setup(s => s.Decrypt("p1"))
                .Returns("p1");

            _mockXmlServiceSerializer
                .Setup(s => s.Serialize(dto))
                .Returns((ServiceDto d) => $"<ServiceDto><Name>{d.Name}</Name></ServiceDto>");

            var repo = CreateRepository();
            var xml = await repo.ExportXmlAsync("A", CancellationToken.None);

            Assert.Contains("<ServiceDto", xml);
            Assert.DoesNotContain("p1", xml);
        }

        [Fact]
        public async Task ImportXML_ValidXml_ReturnsTrue()
        {
            var dto = new ServiceDto { Name = "A" };
            var repo = CreateRepository();
            var xml = $"<ServiceDto><Name>{dto.Name}</Name></ServiceDto>";

            _mockXmlServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Returns(dto);
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<int>(It.IsAny<CommandDefinition>())).ReturnsAsync(0);
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var result = await repo.ImportXmlAsync(xml, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task ImportXML_EmptyXml_ReturnsFalse()
        {
            var repo = CreateRepository();
            var xml = string.Empty;
            var result = await repo.ImportXmlAsync(xml, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task ImportXML_InvalidXml_ReturnsFalse()
        {
            var repo = CreateRepository();
            var xml = "<ServiceDto><Name></Invalid></ServiceDto>";
            _mockXmlServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Throws<Exception>();
            var result = await repo.ImportXmlAsync(xml, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task ImportXML_ServiceNull_ReturnsFalse()
        {
            var repo = CreateRepository();
            var xml = "<ServiceDto></ServiceDto>";

            _mockXmlServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Returns((ServiceDto)null);

            var result = await repo.ImportXmlAsync(xml, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task ExportJSON_ReturnsEmptyString()
        {
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>()))
                .ReturnsAsync((ServiceDto)null);

            var repo = CreateRepository();

            var json = await repo.ExportJsonAsync("A", CancellationToken.None);

            Assert.Empty(json);
        }

        [Fact]
        public async Task ExportJSON_ReturnsSerializedService()
        {
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

            var json = await repo.ExportJsonAsync(name, CancellationToken.None);

            Assert.Equal(expectedJson, json);
            Assert.Contains("\"Name\": \"A\"", json);
        }

        [Fact]
        public async Task ImportJSON_ValidJson_ReturnsTrue()
        {
            var dto = new ServiceDto { Name = "A" };
            var repo = CreateRepository();
            var json = "{\"Name\":\"A\"}";

            _mockJsonServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Returns(dto);
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<int>(It.IsAny<CommandDefinition>())).ReturnsAsync(0);
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var result = await repo.ImportJsonAsync(json, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task ImportJSON_EmptyJson_ReturnsFalse()
        {
            var repo = CreateRepository();
            var json = string.Empty;

            var result = await repo.ImportJsonAsync(json, CancellationToken.None);

            Assert.False(result);
        }

        [Fact]
        public async Task ImportJSON_NullObject_ReturnsFalse()
        {
            var repo = CreateRepository();
            var json = "null";

            var result = await repo.ImportJsonAsync(json, CancellationToken.None);

            Assert.False(result);
        }

        [Fact]
        public async Task ImportJSON_InvalidJson_ReturnsFalse()
        {
            var repo = CreateRepository();
            var json = "{ invalid json }";

            var result = await repo.ImportJsonAsync(json, CancellationToken.None);

            Assert.False(result);
        }

        [Fact]
        public async Task ImportJSON_Throws_ReturnsFalse()
        {
            var repo = CreateRepository();
            var xml = "{ invalid json }";
            _mockJsonServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Throws<Exception>();
            var result = await repo.ImportJsonAsync(xml, CancellationToken.None);
            Assert.False(result);
        }

        #endregion

        #region Newly Added Targeted Coverage Branch Tests

        [Fact]
        public async Task PatchRuntimeStateAsync_ExistingNotNull_ExecutesApplyRuntimeState()
        {
            // Arrange
            var repo = CreateRepository();
            var incoming = new ServiceDto { Name = "TargetService", Pid = 0 };
            var databaseMatch = new ServiceDto { Name = "TargetService", Pid = 9999, ActiveStdoutPath = "db.log" };

            // Satisfies incoming.Name checks and forces "existing != null" path branch
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(databaseMatch);

            // Act
            await repo.UpdateAsync(incoming, preserveExistingRuntimeState: true, preserveExistingCredentials: false, CancellationToken.None);

            // Assert: Verify state from databaseMatch applied successfully to clone sent to executor
            _mockDapper.Verify(d => d.ExecuteAsync(It.IsAny<string>(), It.Is<ServiceDto>(s => s.Pid == 9999 && s.ActiveStdoutPath == "db.log"), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void PatchRuntimeState_ExistingNotNull_ExecutesApplyRuntimeState()
        {
            // Arrange
            var repo = CreateRepository();
            var incoming = new ServiceDto { Name = "TargetSyncService", Pid = 0 };
            var databaseMatch = new ServiceDto { Name = "TargetSyncService", Pid = 8888, ActiveStderrPath = "err.log" };

            // Forces "existing != null" synchronous baseline track path branch
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
        [InlineData(false, false)] // Covers neither (early exit path branch verification)
        public void ApplyRuntimeState_AllCombinationsAndBranches_Covered(bool preserveState, bool preserveCredentials)
        {
            // Arrange
            var incoming = new ServiceDto { Name = "Test", Pid = 0, Password = "new_password" };
            var existing = new ServiceDto { Name = "Test", Pid = 555, Password = "old_password", ActiveStdoutPath = "active.log" };

            // Accessing internal private static method via Reflection to cover all combinations explicitly
            var methodInfo = typeof(ServiceRepository).GetMethod("ApplyRuntimeState",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            // Act
            methodInfo.Invoke(null, new object[] { incoming, existing, preserveState, preserveCredentials });

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

            // Force internal loop inside CreateEncryptedClone to crash during field encryption mapping execution
            _mockSecureData.Setup(s => s.Encrypt(It.IsAny<string>()))
                           .Throws(new CryptographicException("Hardware key missing"));

            // Act & Assert
            var wrapperEx = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await repo.AddAsync(dto, CancellationToken.None));

            Assert.Contains("Encryption failed for field", wrapperEx.Message);
            Assert.NotNull(wrapperEx.InnerException);
            Assert.IsType<CryptographicException>(wrapperEx.InnerException);
        }

        [Fact]
        public async Task DecryptDto_CatchBlock_BubblesUpDescriptiveException()
        {
            // Arrange
            var repo = CreateRepository();
            var corruptDto = new ServiceDto { Id = 1, Password = "corrupt-payload" };

            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>()))
                       .ReturnsAsync(corruptDto);

            // Force DecryptDto internal line block loop invocation to crash on field evaluation mapping
            _mockSecureData.Setup(s => s.Decrypt(It.IsAny<string>()))
                           .Throws(new FormatException("Invalid base64 string layout"));

            // Act & Assert
            // Since GetByIdAsync calls SafeDecrypt (which catches InvalidOperationException), we bypass that hook
            // by hitting DecryptDto directly via reflection or mocking inside an automated pipeline array context
            var methodInfo = typeof(ServiceRepository).GetMethod("DecryptDto",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            var invocationEx = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                methodInfo.Invoke(repo, new object[] { corruptDto }));

            var baseEx = invocationEx.InnerException;
            Assert.IsType<InvalidOperationException>(baseEx);
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

            // Throw a TimeoutException directly from the secure data engine mock.
            // When DecryptDto catches this, it wraps it inside the single InvalidOperationException
            // that HandleCorruptServiceDecryption expects to unwrap via ex.InnerException.
            _mockSecureData.Setup(s => s.Decrypt(It.IsAny<string>()))
                           .Throws(new TimeoutException("Cryptographic subsystem timed out."));

            // Act
            var result = await repo.GetByIdAsync(77, decrypt: true, CancellationToken.None);

            // Assert: Verify row isolation handling scrub logic successfully kicked off instead of crashing query
            Assert.NotNull(result);
            Assert.Contains("[DECRYPTION FAILED: TimeoutException]", result.Description);

            // Verify all sensitive properties are scrubbed/wiped to null following recovery sequence optimization rules
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

            var methodInfo = typeof(ServiceRepository).GetMethod("HandleCorruptServiceDecryption",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            // 1. Branch path verification: Guard tracking on null DTO elements
            methodInfo.Invoke(repo, new object[] { null, targetExWithInner });

            // 2. Branch path verification: Inner Exception evaluates to Null fallback logic mapping
            methodInfo.Invoke(repo, new object[] { dto, targetExNoInner });
            Assert.Contains("[DECRYPTION FAILED: CryptographicException]", dto.Description);

            // 3. Branch path verification: Inner Exception matches concrete reference mapping layout rules
            var freshDto = new ServiceDto { Name = "Row2", Description = "Meta", Password = "ABC" };
            methodInfo.Invoke(repo, new object[] { freshDto, targetExWithInner });

            Assert.Contains("[DECRYPTION FAILED: UnauthorizedAccessException]", freshDto.Description);
            Assert.Null(freshDto.Password);
            Assert.Null(freshDto.Parameters);
        }

        #endregion
    }
}
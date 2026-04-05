using Dapper;
using Moq;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Domain;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;

namespace Servy.Infrastructure.UnitTests.Data
{
    public class ServiceRepositoryTests
    {
        private readonly Mock<IDapperExecutor> _mockDapper;
        private readonly Mock<ISecureData> _mockSecureData;
        private readonly Mock<IXmlServiceSerializer> _mockXmlServiceSerializer;
        private readonly IServiceRepository _serviceRepository;
        private readonly ServiceRepository _repository;
        private readonly Mock<IServiceManager> _serviceManagerMock;

        public ServiceRepositoryTests()
        {
            _mockDapper = new Mock<IDapperExecutor>();
            _mockSecureData = new Mock<ISecureData>(MockBehavior.Loose);
            _mockXmlServiceSerializer = new Mock<IXmlServiceSerializer>();
            _serviceRepository = new ServiceRepositoryStub();

            _repository = new ServiceRepository(_mockDapper.Object, _mockSecureData.Object, _mockXmlServiceSerializer.Object); // ignore dependencies for this test
            _serviceManagerMock = new Mock<IServiceManager>();
        }

        private ServiceRepository CreateRepository()
        {
            return new ServiceRepository(_mockDapper.Object, _mockSecureData.Object, _mockXmlServiceSerializer.Object);
        }

        private string? GetPropertyValue(object obj, string propName)
        {
            return obj.GetType().GetProperty(propName)?.GetValue(obj, null)?.ToString();
        }

        [Fact]
        public void Constructor_NullDapper_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(null!, _mockSecureData.Object, _mockXmlServiceSerializer.Object));
        }

        [Fact]
        public void Constructor_NullSecureData_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(_mockDapper.Object, null!, _mockXmlServiceSerializer.Object));
        }

        [Fact]
        public void Constructor_NullXmlServiceSerializer_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(_mockDapper.Object, _mockSecureData.Object, null!));
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

            // AddAsync typically uses ExecuteScalarAsync to return the new Row ID
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync(99);

            var repo = CreateRepository();

            // 3. Act
            var result = await repo.AddAsync(dto, TestContext.Current.CancellationToken);

            // 4. Assert - Functional Result
            Assert.Equal(99, result);
            Assert.Equal(99, dto.Id);

            // 5. Assert - Side-Effect Protection
            // Ensure the original DTO was NOT mutated (remains plain-text for the UI/User)
            Assert.Equal("p_plain", dto.Password);
            Assert.Equal("env_plain", dto.EnvironmentVariables);
            Assert.Equal("pre_stop_plain", dto.PreStopParameters);

            // 6. Verify - Ensure the object sent to Dapper was the encrypted clone
            _mockDapper.Verify(d => d.ExecuteScalarAsync<int>(
                It.IsAny<string>(),
                It.Is<object>(obj =>
                    GetPropertyValue(obj, "Password") == "p_enc" &&
                    GetPropertyValue(obj, "Parameters") == "args_enc" &&
                    GetPropertyValue(obj, "EnvironmentVariables") == "env_enc" &&
                    GetPropertyValue(obj, "FailureProgramParameters") == "fail_args_enc" &&
                    GetPropertyValue(obj, "PreLaunchParameters") == "pre_args_enc" &&
                    GetPropertyValue(obj, "PreLaunchEnvironmentVariables") == "pre_env_enc" &&
                    GetPropertyValue(obj, "PostLaunchParameters") == "post_args_enc" &&
                    GetPropertyValue(obj, "PreStopParameters") == "pre_stop_enc" &&
                    GetPropertyValue(obj, "PostStopParameters") == "post_stop_enc"
                )), Times.Once);
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

            // ExecuteAsync returns the number of rows affected (usually 1)
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
                       .ReturnsAsync(1);

            var repo = CreateRepository();

            // 3. Act
            var rowsAffected = await repo.UpdateAsync(dto, TestContext.Current.CancellationToken);

            // 4. Assert - Functional Results
            Assert.Equal(1, rowsAffected);

            // 5. Assert - Side-Effect Protection
            // The original object MUST NOT be mutated (remains plain-text for the UI)
            Assert.Equal("p_plain", dto.Password);
            Assert.Equal("env_plain", dto.EnvironmentVariables);
            Assert.Equal("pre_stop_plain", dto.PreStopParameters);

            // 6. Verify - Ensure the object sent to Dapper was the encrypted clone
            _mockDapper.Verify(d => d.ExecuteAsync(
                It.IsAny<string>(),
                It.Is<object>(obj =>
                    GetPropertyValue(obj, "Id")!.Equals("123") &&
                    GetPropertyValue(obj, "Password") == "p_enc" &&
                    GetPropertyValue(obj, "Parameters") == "args_enc" &&
                    GetPropertyValue(obj, "EnvironmentVariables") == "env_enc" &&
                    GetPropertyValue(obj, "FailureProgramParameters") == "fail_args_enc" &&
                    GetPropertyValue(obj, "PreLaunchParameters") == "pre_args_enc" &&
                    GetPropertyValue(obj, "PreLaunchEnvironmentVariables") == "pre_env_enc" &&
                    GetPropertyValue(obj, "PostLaunchParameters") == "post_args_enc" &&
                    GetPropertyValue(obj, "PreStopParameters") == "pre_stop_enc" &&
                    GetPropertyValue(obj, "PostStopParameters") == "post_stop_enc"
                )), Times.Once);
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

            // ExecuteAsync returns the number of rows affected (usually 1)
            _mockDapper.Setup(d => d.Execute(It.IsAny<string>(), It.IsAny<object>()))
                       .Returns(1);

            var repo = CreateRepository();

            // 3. Act
            var rowsAffected = repo.Update(dto);

            // 4. Assert - Functional Results
            Assert.Equal(1, rowsAffected);

            // 5. Assert - Side-Effect Protection
            // The original object MUST NOT be mutated (remains plain-text for the UI)
            Assert.Equal("p_plain", dto.Password);
            Assert.Equal("env_plain", dto.EnvironmentVariables);
            Assert.Equal("pre_stop_plain", dto.PreStopParameters);

            // 6. Verify - Ensure the object sent to Dapper was the encrypted clone
            _mockDapper.Verify(d => d.Execute(
                It.IsAny<string>(),
                It.Is<object>(obj =>
                    GetPropertyValue(obj, "Id")!.Equals("123") &&
                    GetPropertyValue(obj, "Password") == "p_enc" &&
                    GetPropertyValue(obj, "Parameters") == "args_enc" &&
                    GetPropertyValue(obj, "EnvironmentVariables") == "env_enc" &&
                    GetPropertyValue(obj, "FailureProgramParameters") == "fail_args_enc" &&
                    GetPropertyValue(obj, "PreLaunchParameters") == "pre_args_enc" &&
                    GetPropertyValue(obj, "PreLaunchEnvironmentVariables") == "pre_env_enc" &&
                    GetPropertyValue(obj, "PostLaunchParameters") == "post_args_enc" &&
                    GetPropertyValue(obj, "PreStopParameters") == "pre_stop_enc" &&
                    GetPropertyValue(obj, "PostStopParameters") == "post_stop_enc"
                )), Times.Once);
        }

        [Fact]
        public async Task UpsertAsync_ExistingService_UpdatesAndReturnsId()
        {
            // Arrange
            var dto = new ServiceDto { Name = "S1" };
            const int expectedId = 5;

            // We now mock ExecuteScalarAsync because that's what the atomic UPSERT calls.
            // It returns the ID of the inserted or updated row.
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(
                    It.IsAny<string>(),
                    It.IsAny<object>()))
                .ReturnsAsync(expectedId);

            _mockSecureData.Setup(s => s.Encrypt(It.IsAny<string>())).Returns<string>(s => s);

            var repo = CreateRepository();

            // Act
            var resultId = await repo.UpsertAsync(dto, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(expectedId, resultId);
            Assert.Equal(expectedId, dto.Id);

            // Note: dto.Pid will NOT be 123 here unless your Upsert SQL 
            // explicitly selects it and you assign it to the DTO.
            // In Servy, Pid is volatile/runtime data, so usually, we don't 
            // sync it during a configuration 'Upsert'.
        }

        [Fact]
        public async Task UpsertAsync_NewService_Adds()
        {
            var dto = new ServiceDto { Name = "NewService" };
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto?>(It.IsAny<CommandDefinition>())).ReturnsAsync((ServiceDto?)null);
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(7);
            _mockSecureData.Setup(s => s.Encrypt(It.IsAny<string>())).Returns<string>(s => s);

            var repo = CreateRepository();
            var rows = await repo.UpsertAsync(dto, TestContext.Current.CancellationToken);

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

            // Mock ExecuteScalarAsync (The Atomic Upsert)
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(
                It.IsAny<string>(),
                It.IsAny<object>()))
                .ReturnsAsync(generatedId);

            var repo = CreateRepository();

            // Act
            var result = await repo.UpsertAsync(dto, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(generatedId, result);
            Assert.Equal(generatedId, dto.Id);

            // IMPORTANT: The original DTO password remains "plain" because we 
            // encrypted a CLONE for the DB. This is safer for the UI/caller.
            Assert.Equal("plain", dto.Password);

            // Verify that the object passed to Dapper actually contained the encrypted value
            _mockDapper.Verify(d => d.ExecuteScalarAsync<int>(
                It.IsAny<string>(),
                It.Is<object>(obj => GetPropertyValue(obj, "Password") == encryptedValue)
                ), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ById_ReturnsAffectedRows()
        {
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);

            var repo = CreateRepository();
            var rows = await repo.DeleteAsync(10, TestContext.Current.CancellationToken);

            Assert.Equal(1, rows);
        }

        [Fact]
        public async Task DeleteAsync_ByName_ReturnsAffectedRows()
        {
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);

            var repo = CreateRepository();
            var rows = await repo.DeleteAsync("ServiceName", TestContext.Current.CancellationToken);

            Assert.Equal(1, rows);
        }

        [Fact]
        public async Task DeleteAsync_ByName_ReturnsZero()
        {
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);

            var repo = CreateRepository();
            var rows = await repo.DeleteAsync(string.Empty, TestContext.Current.CancellationToken);

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
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(dto);
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
            var result = await repo.GetByIdAsync(1, true, TestContext.Current.CancellationToken);

            Assert.Equal("params", result!.Parameters);
            Assert.Equal("failure-prog-params", result!.FailureProgramParameters);
            Assert.Equal("pre-launch-params", result!.PreLaunchParameters);
            Assert.Equal("post-launch-params", result!.PostLaunchParameters);
            Assert.Equal("plain", result!.Password);
            Assert.Equal("v1=val1;v2=val2", result!.EnvironmentVariables);
            Assert.Equal("v3=val3", result!.PreLaunchEnvironmentVariables);
            Assert.Equal("pre-stop-params", result!.PreStopParameters);
            Assert.Equal("post-stop-params", result!.PostStopParameters);
        }

        [Fact]
        public async Task GetByIdAsync_EmptyPassword()
        {
            var dto = new ServiceDto { Id = 1, Password = null };
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(dto);

            var repo = CreateRepository();
            var result = await repo.GetByIdAsync(1, true, TestContext.Current.CancellationToken);

            Assert.Null(result!.Password);
        }

        [Fact]
        public async Task GetByIdAsync_NullDto()
        {
            ServiceDto? dto = null;
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(dto);

            var repo = CreateRepository();
            var result = await repo.GetByIdAsync(1, true, TestContext.Current.CancellationToken);

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
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(dto);
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
            var result = await repo.GetByNameAsync("S", true, TestContext.Current.CancellationToken);

            Assert.Equal("params", result!.Parameters);
            Assert.Equal("failure-prog-params", result!.FailureProgramParameters);
            Assert.Equal("pre-launch-params", result!.PreLaunchParameters);
            Assert.Equal("post-launch-params", result!.PostLaunchParameters);
            Assert.Equal("plain", result!.Password);
            Assert.Equal("v1=val1;v2=val2", result!.EnvironmentVariables);
            Assert.Equal("v3=val3", result!.PreLaunchEnvironmentVariables);
            Assert.Equal("pre-stop-params", result!.PreStopParameters);
            Assert.Equal("post-stop-params", result!.PostStopParameters);
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
            _mockDapper.Setup(d => d.QuerySingleOrDefault<ServiceDto>(It.IsAny<string>(), It.IsAny<object>())).Returns(dto);
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

            Assert.Equal("params", result!.Parameters);
            Assert.Equal("failure-prog-params", result!.FailureProgramParameters);
            Assert.Equal("pre-launch-params", result!.PreLaunchParameters);
            Assert.Equal("post-launch-params", result!.PostLaunchParameters);
            Assert.Equal("plain", result!.Password);
            Assert.Equal("v1=val1;v2=val2", result!.EnvironmentVariables);
            Assert.Equal("v3=val3", result!.PreLaunchEnvironmentVariables);
            Assert.Equal("pre-stop-params", result!.PreStopParameters);
            Assert.Equal("post-stop-params", result!.PostStopParameters);
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
                .Setup(d => d.QueryAsync<ServiceDto>(It.IsAny<CommandDefinition>()))
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
            var result = (await repo.GetAllAsync(true, TestContext.Current.CancellationToken)).ToList();

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
            _mockDapper.Setup(d => d.QueryAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(list);
            _mockSecureData.Setup(s => s.Decrypt("e1")).Returns("p1");

            var repo = CreateRepository();
            var result = (await repo.SearchAsync("A", true, TestContext.Current.CancellationToken)).ToList();

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
            _mockDapper.Setup(d => d.QueryAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(list);
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
            var result = (await repo.SearchAsync(null!, true, TestContext.Current.CancellationToken)).ToList();

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

        [Fact]
        public async Task ExportXML_ReturnsEmptyString()
        {
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>()))
                .ReturnsAsync((ServiceDto?)null);

            var repo = CreateRepository();

            var xml = await repo.ExportXmlAsync("A", TestContext.Current.CancellationToken);

            Assert.Empty(xml);
        }

        [Fact]
        public async Task ExportXML_ReturnsSerializedService()
        {
            var dto = new ServiceDto { Name = "A", Password = "p1" };

            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>()))
                .ReturnsAsync(dto);

            _mockSecureData
                .Setup(s => s.Decrypt("p1"))
                .Returns("p1"); // return the same value or "plain" if you prefer

            var repo = CreateRepository();
            var xml = await repo.ExportXmlAsync("A", TestContext.Current.CancellationToken);

            Assert.Contains("<ServiceDto", xml);
            Assert.Contains("p1", xml);
        }

        [Fact]
        public async Task ImportXML_ValidXml_ReturnsTrue()
        {
            var dto = new ServiceDto { Name = "A" };
            var repo = CreateRepository();
            var xml = $"<ServiceDto><Name>{dto.Name}</Name></ServiceDto>";

            _mockXmlServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Returns(dto);
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<int>(It.IsAny<CommandDefinition>())).ReturnsAsync(0);
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);

            var result = await repo.ImportXmlAsync(xml, TestContext.Current.CancellationToken);
            Assert.True(result);
        }

        [Fact]
        public async Task ImportXML_EmptyXml_ReturnsFalse()
        {
            var repo = CreateRepository();
            var xml = string.Empty;
            var result = await repo.ImportXmlAsync(xml, TestContext.Current.CancellationToken);
            Assert.False(result);
        }

        [Fact]
        public async Task ImportXML_InvalidXml_ReturnsFalse()
        {
            var repo = CreateRepository();
            var xml = "<ServiceDto><Name></Invalid></ServiceDto>";
            _mockXmlServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Throws<Exception>();
            var result = await repo.ImportXmlAsync(xml, TestContext.Current.CancellationToken);
            Assert.False(result);
        }

        [Fact]
        public async Task ImportXML_ServiceNull_ReturnsFalse()
        {
            var repo = CreateRepository();
            var xml = "<ServiceDto></ServiceDto>";

            _mockXmlServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Returns((ServiceDto?)null);

            var result = await repo.ImportXmlAsync(xml, TestContext.Current.CancellationToken);
            Assert.False(result);
        }

        [Fact]
        public async Task ExportJSON_ReturnsEmptyString()
        {
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>()))
                .ReturnsAsync((ServiceDto?)null);

            var repo = CreateRepository();

            var json = await repo.ExportJsonAsync("A", TestContext.Current.CancellationToken);

            Assert.Empty(json);
        }

        [Fact]
        public async Task ExportJSON_ReturnsSerializedService()
        {
            var dto = new ServiceDto { Name = "A" };
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(dto);

            var repo = CreateRepository();
            var json = await repo.ExportJsonAsync("A", TestContext.Current.CancellationToken);

            Assert.Contains("\"Name\": \"A\"", json);
        }

        [Fact]
        public async Task ImportJSON_ValidJson_ReturnsTrue()
        {
            var repo = CreateRepository();
            var json = "{\"Name\":\"A\"}";

            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<int>(It.IsAny<CommandDefinition>())).ReturnsAsync(0);
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);

            var result = await repo.ImportJsonAsync(json, TestContext.Current.CancellationToken);
            Assert.True(result);
        }

        [Fact]
        public async Task ImportJSON_EmptyJson_ReturnsFalse()
        {
            var repo = CreateRepository();
            var json = string.Empty;

            var result = await repo.ImportJsonAsync(json, TestContext.Current.CancellationToken);

            Assert.False(result);
        }

        [Fact]
        public async Task ImportJSON_NullObject_ReturnsFalse()
        {
            var repo = CreateRepository();
            var json = "null";

            var result = await repo.ImportJsonAsync(json, TestContext.Current.CancellationToken);

            Assert.False(result);
        }

        [Fact]
        public async Task ImportJSON_InvalidJson_ReturnsFalse()
        {
            var repo = CreateRepository();
            var json = "{ invalid json }";

            var result = await repo.ImportJsonAsync(json, TestContext.Current.CancellationToken);

            Assert.False(result);
        }

        [Fact]
        public async Task AddDomainServiceAsync_CallsAddAsync()
        {
            var service = new Service(null!) { Name = "TestService" };
            var result = await _serviceRepository.AddDomainServiceAsync(service, TestContext.Current.CancellationToken);
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task AddDomainServiceAsync_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _serviceRepository.AddDomainServiceAsync(null!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task UpdateDomainServiceAsync_CallsUpdateAsync()
        {
            var service = new Service(null!) { Name = "TestService" };
            var result = await _serviceRepository.UpdateDomainServiceAsync(service, TestContext.Current.CancellationToken);
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task UpsertDomainServiceAsync_CallsUpsertAsync()
        {
            var service = new Service(null!) { Name = "TestService" };
            var result = await _serviceRepository.UpsertDomainServiceAsync(service, TestContext.Current.CancellationToken);
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task DeleteDomainServiceAsync_ById_CallsDeleteAsync()
        {
            var result = await _serviceRepository.DeleteDomainServiceAsync(1, TestContext.Current.CancellationToken);
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task DeleteDomainServiceAsync_ByName_CallsDeleteAsync()
        {
            var result = await _serviceRepository.DeleteDomainServiceAsync("TestService", TestContext.Current.CancellationToken);
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task GetDomainServiceByIdAsync_ReturnsMappedService()
        {
            var serviceManager = new Mock<IServiceManager>().Object;
            var result = await _serviceRepository.GetDomainServiceByIdAsync(serviceManager, 1, true, TestContext.Current.CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("StubService", result.Name);
            Assert.Equal(@"C:\logs\stub_active_stdout.log", result.ActiveStdoutPath);
            Assert.Equal(@"C:\logs\stub_active_stderr.log", result.ActiveStderrPath);
        }

        [Fact]
        public async Task GetDomainServiceByIdAsync_ReturnsNull_WhenDtoNull()
        {
            var serviceRepository = new ServiceRepositoryStub(returnNullDto: true);
            var serviceManager = new Mock<IServiceManager>().Object;
            var result = await serviceRepository.GetDomainServiceByIdAsync(serviceManager, 1, true, TestContext.Current.CancellationToken);
            Assert.Null(result);
        }

        [Fact]
        public async Task GetDomainServiceByNameAsync_ReturnsMappedService()
        {
            var serviceManager = new Mock<IServiceManager>().Object;
            var result = await _serviceRepository.GetDomainServiceByNameAsync(serviceManager, "TestService", true, TestContext.Current.CancellationToken);
            Assert.NotNull(result);
            Assert.Equal("TestService", result.Name);
        }

        [Fact]
        public async Task GetDomainServiceByNameAsync_ReturnsNull_WhenDtoNull()
        {
            var serviceRepository = new ServiceRepositoryStub(returnNullDto: true);
            var serviceManager = new Mock<IServiceManager>().Object;
            var result = await serviceRepository.GetDomainServiceByNameAsync(serviceManager, "TestService", true, TestContext.Current.CancellationToken);
            Assert.Null(result);
        }

        [Fact]
        public async Task GetAllDomainServicesAsync_ReturnsMappedServices()
        {
            var serviceManager = new Mock<IServiceManager>().Object;
            var result = (await _serviceRepository.GetAllDomainServicesAsync(serviceManager, true, TestContext.Current.CancellationToken)).ToList();
            Assert.Single(result);
            Assert.Equal("StubService", result[0].Name);
        }

        [Fact]
        public async Task SearchDomainServicesAsync_ReturnsMappedServices()
        {
            var serviceManager = new Mock<IServiceManager>().Object;
            var result = (await _serviceRepository.SearchDomainServicesAsync(serviceManager, "StubService", true, TestContext.Current.CancellationToken)).ToList();
            Assert.Single(result);
            Assert.Equal("StubService", result[0].Name);
        }

        [Fact]
        public async Task ExportDomainServiceXMLAsync_ReturnsString()
        {
            var result = await _serviceRepository.ExportDomainServiceXMLAsync("Service1", TestContext.Current.CancellationToken);
            Assert.Equal("<xml></xml>", result);
        }

        [Fact]
        public async Task ImportDomainServiceXMLAsync_ReturnsBool()
        {
            var result = await _serviceRepository.ImportDomainServiceXMLAsync("<xml></xml>", TestContext.Current.CancellationToken);
            Assert.True(result);
        }

        [Fact]
        public async Task ExportDomainServiceJSONAsync_ReturnsString()
        {
            var result = await _serviceRepository.ExportDomainServiceJSONAsync("Service1", TestContext.Current.CancellationToken);
            Assert.Equal("{ }", result);
        }

        [Fact]
        public async Task ImportDomainServiceJSONAsync_ReturnsBool()
        {
            var result = await _serviceRepository.ImportDomainServiceJSONAsync("{ }", TestContext.Current.CancellationToken);
            Assert.True(result);
        }

        private Service InvokeMapToDomain(ServiceDto dto)
        {
            var method = typeof(ServiceRepository)
                .GetMethod("MapToDomain", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return (Service)method!.Invoke(_repository, new object[] { _serviceManagerMock.Object, dto })!;
        }

        [Fact]
        public void MapToDomain_ThrowsArgumentNullException_WhenDtoIsNull()
        {
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                InvokeMapToDomain(null!));

            Assert.IsType<ArgumentNullException>(ex.InnerException);
        }

        [Fact]
        public void MapToDomain_MapsAllPropertiesCorrectly()
        {
            var dto = new ServiceDto
            {
                Name = "MyService",
                Description = "Test service",
                ExecutablePath = "C:\\service.exe",
                StartupDirectory = "C:\\",
                Parameters = "-arg",
                StartupType = (int)ServiceStartType.Automatic,
                Priority = (int)ProcessPriority.High,
                StdoutPath = "out.log",
                StderrPath = "err.log",
                EnableRotation = true,
                RotationSize = 2048,
                EnableDateRotation = true,
                DateRotationType = (int)DateRotationType.Weekly,
                MaxRotations = 5,
                EnableDebugLogs = true,
                EnableHealthMonitoring = true,
                HeartbeatInterval = 60,
                MaxFailedChecks = 5,
                RecoveryAction = (int)RecoveryAction.RestartService,
                MaxRestartAttempts = 7,
                FailureProgramPath = @"C:\apps\failure_prog.exe",
                FailureProgramParameters = "--param1",
                FailureProgramStartupDirectory = @"C:\apps",
                EnvironmentVariables = "ENV=1",
                ServiceDependencies = "Dep1,Dep2",
                RunAsLocalSystem = false,
                UserAccount = "user",
                Password = "secret",
                PreLaunchExecutablePath = "pre.exe",
                PreLaunchStartupDirectory = "C:\\pre",
                PreLaunchParameters = "-prearg",
                PreLaunchEnvironmentVariables = "PRE_ENV=1",
                PreLaunchStdoutPath = "preout.log",
                PreLaunchStderrPath = "preerr.log",
                PreLaunchTimeoutSeconds = 120,
                PreLaunchRetryAttempts = 2,
                PreLaunchIgnoreFailure = true,
                PostLaunchExecutablePath = @"C:\apps\post_launch\post_launch.exe",
                PostLaunchParameters = "--post-param1",
                PostLaunchStartupDirectory = @"C:\apps\post_launch\",

                PreStopExecutablePath = @"C:\apps\pre_stop\pre_stop.exe",
                PreStopParameters = "--pre-stop-param1",
                PreStopStartupDirectory = @"C:\apps\pre_stop\",
                PreStopTimeoutSeconds = 90,
                PreStopLogAsError = true,

                PostStopExecutablePath = @"C:\apps\post_stop\post_stop.exe",
                PostStopParameters = "--post-stop-param1",
                PostStopStartupDirectory = @"C:\apps\post_stop\",
            };

            var domain = InvokeMapToDomain(dto);

            Assert.Equal(dto.Name, domain.Name);
            Assert.Equal(dto.Description, domain.Description);
            Assert.Equal(dto.ExecutablePath, domain.ExecutablePath);
            Assert.Equal(dto.StartupDirectory, domain.StartupDirectory);
            Assert.Equal(dto.Parameters, domain.Parameters);
            Assert.Equal(ServiceStartType.Automatic, domain.StartupType);
            Assert.Equal(ProcessPriority.High, domain.Priority);
            Assert.Equal(dto.StdoutPath, domain.StdoutPath);
            Assert.Equal(dto.StderrPath, domain.StderrPath);
            Assert.True(domain.EnableRotation);
            Assert.Equal(2048, domain.RotationSize);
            Assert.Equal(dto.EnableDateRotation, domain.EnableDateRotation);
            Assert.Equal(dto.DateRotationType, (int)domain.DateRotationType);
            Assert.Equal(dto.MaxRotations, domain.MaxRotations);
            Assert.Equal(dto.EnableDebugLogs, domain.EnableDebugLogs);
            Assert.True(domain.EnableHealthMonitoring);
            Assert.Equal(60, domain.HeartbeatInterval);
            Assert.Equal(5, domain.MaxFailedChecks);
            Assert.Equal(RecoveryAction.RestartService, domain.RecoveryAction);
            Assert.Equal(7, domain.MaxRestartAttempts);
            Assert.Equal(dto.FailureProgramPath, domain.FailureProgramPath);
            Assert.Equal(dto.FailureProgramStartupDirectory, domain.FailureProgramStartupDirectory);
            Assert.Equal(dto.FailureProgramParameters, domain.FailureProgramParameters);
            Assert.Equal("ENV=1", domain.EnvironmentVariables);
            Assert.Equal("Dep1,Dep2", domain.ServiceDependencies);
            Assert.False(domain.RunAsLocalSystem);
            Assert.Equal("user", domain.UserAccount);
            Assert.Equal("secret", domain.Password);
            Assert.Equal("pre.exe", domain.PreLaunchExecutablePath);
            Assert.Equal("C:\\pre", domain.PreLaunchStartupDirectory);
            Assert.Equal("-prearg", domain.PreLaunchParameters);
            Assert.Equal("PRE_ENV=1", domain.PreLaunchEnvironmentVariables);
            Assert.Equal("preout.log", domain.PreLaunchStdoutPath);
            Assert.Equal("preerr.log", domain.PreLaunchStderrPath);
            Assert.Equal(120, domain.PreLaunchTimeoutSeconds);
            Assert.Equal(2, domain.PreLaunchRetryAttempts);
            Assert.True(domain.PreLaunchIgnoreFailure);
            Assert.Equal(dto.PostLaunchExecutablePath, domain.PostLaunchExecutablePath);
            Assert.Equal(dto.PostLaunchStartupDirectory, domain.PostLaunchStartupDirectory);
            Assert.Equal(dto.PostLaunchParameters, domain.PostLaunchParameters);

            Assert.Equal(dto.PreStopExecutablePath, domain.PreStopExecutablePath);
            Assert.Equal(dto.PreStopStartupDirectory, domain.PreStopStartupDirectory);
            Assert.Equal(dto.PreStopParameters, domain.PreStopParameters);
            Assert.Equal(dto.PreStopTimeoutSeconds, domain.PreStopTimeoutSeconds);
            Assert.Equal(dto.PreStopLogAsError, domain.PreStopLogAsError);

            Assert.Equal(dto.PostStopExecutablePath, domain.PostStopExecutablePath);
            Assert.Equal(dto.PostStopStartupDirectory, domain.PostStopStartupDirectory);
            Assert.Equal(dto.PostStopParameters, domain.PostStopParameters);
        }

        [Fact]
        public void MapToDomain_UsesDefaultValues_WhenDtoValuesAreNull()
        {
            var dto = new ServiceDto(); // all nulls

            var domain = InvokeMapToDomain(dto);

            Assert.Equal(ServiceStartType.Manual, domain.StartupType);
            Assert.Equal(ProcessPriority.Normal, domain.Priority);
            Assert.False(domain.EnableRotation);
            Assert.False(domain.EnableDateRotation);
            Assert.Equal(DateRotationType.Daily, domain.DateRotationType);
            Assert.Equal(AppConfig.DefaultMaxRotations, domain.MaxRotations);
            Assert.Equal(AppConfig.DefaultRotationSize, domain.RotationSize);
            Assert.False(domain.EnableHealthMonitoring);
            Assert.Equal(30, domain.HeartbeatInterval);
            Assert.Equal(3, domain.MaxFailedChecks);
            Assert.Equal(RecoveryAction.None, domain.RecoveryAction);
            Assert.Equal(3, domain.MaxRestartAttempts);
            Assert.True(domain.RunAsLocalSystem);
            Assert.Equal(30, domain.PreLaunchTimeoutSeconds);
            Assert.Equal(0, domain.PreLaunchRetryAttempts);
            Assert.False(domain.PreLaunchIgnoreFailure);
        }

        private ServiceDto InvokeMapToDto(Service domain)
        {
            var method = typeof(ServiceRepository)
                .GetMethod("MapToDto", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return (ServiceDto)method!.Invoke(_repository, new object[] { domain })!;
        }

        [Fact]
        public void MapToDto_ThrowsArgumentNullException_WhenDomainIsNull()
        {
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                InvokeMapToDto(null!));

            Assert.IsType<ArgumentNullException>(ex.InnerException);
        }

        [Fact]
        public void MapToDto_MapsAllPropertiesCorrectly()
        {
            var domain = new Service(_serviceManagerMock.Object)
            {
                Name = "MyService",
                Description = "Test service",
                ExecutablePath = "C:\\service.exe",
                StartupDirectory = "C:\\",
                Parameters = "-arg",
                StartupType = ServiceStartType.Automatic,
                Priority = ProcessPriority.High,
                StdoutPath = "out.log",
                StderrPath = "err.log",
                EnableRotation = true,
                RotationSize = 2048,
                EnableHealthMonitoring = true,
                HeartbeatInterval = 60,
                MaxFailedChecks = 5,
                RecoveryAction = RecoveryAction.RestartService,
                MaxRestartAttempts = 7,
                EnvironmentVariables = "ENV=1",
                ServiceDependencies = "Dep1,Dep2",
                RunAsLocalSystem = false,
                UserAccount = "user",
                Password = "secret",
                PreLaunchExecutablePath = "pre.exe",
                PreLaunchStartupDirectory = "C:\\pre",
                PreLaunchParameters = "-prearg",
                PreLaunchEnvironmentVariables = "PRE_ENV=1",
                PreLaunchStdoutPath = "preout.log",
                PreLaunchStderrPath = "preerr.log",
                PreLaunchTimeoutSeconds = 120,
                PreLaunchRetryAttempts = 2,
                PreLaunchIgnoreFailure = true
            };

            var dto = InvokeMapToDto(domain);

            Assert.Equal(domain.Name, dto.Name);
            Assert.Equal(domain.Description, dto.Description);
            Assert.Equal(domain.ExecutablePath, dto.ExecutablePath);
            Assert.Equal(domain.StartupDirectory, dto.StartupDirectory);
            Assert.Equal(domain.Parameters, dto.Parameters);
            Assert.Equal((int)domain.StartupType, dto.StartupType);
            Assert.Equal((int)domain.Priority, dto.Priority);
            Assert.Equal(domain.StdoutPath, dto.StdoutPath);
            Assert.Equal(domain.StderrPath, dto.StderrPath);
            Assert.True(dto.EnableRotation);
            Assert.Equal(2048, dto.RotationSize);
            Assert.True(dto.EnableHealthMonitoring);
            Assert.Equal(60, dto.HeartbeatInterval);
            Assert.Equal(5, dto.MaxFailedChecks);
            Assert.Equal((int)domain.RecoveryAction, dto.RecoveryAction);
            Assert.Equal(7, dto.MaxRestartAttempts);
            Assert.Equal("ENV=1", dto.EnvironmentVariables);
            Assert.Equal("Dep1,Dep2", dto.ServiceDependencies);
            Assert.False(dto.RunAsLocalSystem);
            Assert.Equal("user", dto.UserAccount);
            Assert.Equal("secret", dto.Password);
            Assert.Equal("pre.exe", dto.PreLaunchExecutablePath);
            Assert.Equal("C:\\pre", dto.PreLaunchStartupDirectory);
            Assert.Equal("-prearg", dto.PreLaunchParameters);
            Assert.Equal("PRE_ENV=1", dto.PreLaunchEnvironmentVariables);
            Assert.Equal("preout.log", dto.PreLaunchStdoutPath);
            Assert.Equal("preerr.log", dto.PreLaunchStderrPath);
            Assert.Equal(120, dto.PreLaunchTimeoutSeconds);
            Assert.Equal(2, dto.PreLaunchRetryAttempts);
            Assert.True(dto.PreLaunchIgnoreFailure);
        }
    }
}

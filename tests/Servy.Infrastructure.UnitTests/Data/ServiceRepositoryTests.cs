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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Infrastructure.UnitTests.Data
{
    public class ServiceRepositoryTests
    {
        private readonly Mock<IDapperExecutor> _mockDapper;
        private readonly Mock<ISecureData> _mockSecureData;
        private readonly Mock<IXmlServiceSerializer> _mockXmlServiceSerializer;

        public ServiceRepositoryTests()
        {
            _mockDapper = new Mock<IDapperExecutor>();
            _mockSecureData = new Mock<ISecureData>(MockBehavior.Loose);
            _mockXmlServiceSerializer = new Mock<IXmlServiceSerializer>();
        }

        private ServiceRepository CreateRepository()
        {
            return new ServiceRepository(_mockDapper.Object, _mockSecureData.Object, _mockXmlServiceSerializer.Object);
        }

        private string GetPropertyValue(object obj, string propName)
        {
            return obj.GetType().GetProperty(propName)?.GetValue(obj, null)?.ToString();
        }

        [Fact]
        public void Constructor_NullDapper_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(null, _mockSecureData.Object, _mockXmlServiceSerializer.Object));
        }

        [Fact]
        public void Constructor_NullSecureData_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(_mockDapper.Object, null, _mockXmlServiceSerializer.Object));
        }

        [Fact]
        public void Constructor_NullXmlServiceSerializer_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(_mockDapper.Object, _mockSecureData.Object, null));
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
            var result = await repo.AddAsync(dto);

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
            var rowsAffected = await repo.UpdateAsync(dto);

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
                    GetPropertyValue(obj, "Id").Equals("123") &&
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
                    GetPropertyValue(obj, "Id").Equals("123") &&
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
            var resultId = await repo.UpsertAsync(dto);

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
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync((ServiceDto)null);
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(7);
            _mockSecureData.Setup(s => s.Encrypt(It.IsAny<string>())).Returns<string>(s => s);

            var repo = CreateRepository();
            var rows = await repo.UpsertAsync(dto);

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
            var result = await repo.UpsertAsync(dto);

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
        public async Task UpsertBatchAsync_NullServices_ReturnsZero()
        {
            // Arrange
            var repo = CreateRepository();

            // Act
            var result = await repo.UpsertBatchAsync(null);

            // Assert
            Assert.Equal(0, result);
            _mockDapper.Verify(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public async Task UpsertBatchAsync_EmptyServices_ReturnsZero()
        {
            // Arrange
            var repo = CreateRepository();
            var services = new List<ServiceDto>();

            // Act
            var result = await repo.UpsertBatchAsync(services);

            // Assert
            Assert.Equal(0, result);
            _mockDapper.Verify(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public async Task UpsertBatchAsync_ValidServices_EncryptsDataAndCallsDapper()
        {
            // Arrange
            var repo = CreateRepository();
            var services = new List<ServiceDto>
            {
                new ServiceDto { Name = "Service1", Password = "plain_password_1" },
                new ServiceDto { Name = "Service2", Password = "plain_password_2" }
            };

            const string encryptedPass = "encrypted_password";
            const int expectedRows = 2;

            _mockSecureData.Setup(s => s.Encrypt(It.IsAny<string>())).Returns(encryptedPass);
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<IEnumerable<ServiceDto>>()))
                       .ReturnsAsync(expectedRows);

            // Act
            var result = await repo.UpsertBatchAsync(services);

            // Assert
            Assert.Equal(expectedRows, result);

            // Verify encryption was called for each service with a password
            _mockSecureData.Verify(s => s.Encrypt("plain_password_1"), Times.Once);
            _mockSecureData.Verify(s => s.Encrypt("plain_password_2"), Times.Once);

            // Verify Dapper received the SQL and the collection
            _mockDapper.Verify(d => d.ExecuteAsync(
                It.Is<string>(s => s.Contains("INSERT INTO Services") && s.Contains("ON CONFLICT")),
                It.Is<IEnumerable<ServiceDto>>(list => list.All(dto => dto.Password == encryptedPass))
            ), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ById_ReturnsAffectedRows()
        {
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);

            var repo = CreateRepository();
            var rows = await repo.DeleteAsync(10);

            Assert.Equal(1, rows);
        }

        [Fact]
        public async Task DeleteAsync_ByName_ReturnsAffectedRows()
        {
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);

            var repo = CreateRepository();
            var rows = await repo.DeleteAsync("ServiceName");

            Assert.Equal(1, rows);
        }

        [Fact]
        public async Task DeleteAsync_ByName_ReturnsZero()
        {
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);

            var repo = CreateRepository();
            var rows = await repo.DeleteAsync(string.Empty);

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
            var result = await repo.GetByIdAsync(1, true);

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
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(dto);

            var repo = CreateRepository();
            var result = await repo.GetByIdAsync(1, true);

            Assert.Null(result.Password);
        }

        [Fact]
        public async Task GetByIdAsync_NullDto()
        {
            ServiceDto dto = null;
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(dto);

            var repo = CreateRepository();
            var result = await repo.GetByIdAsync(1, true);

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
            var result = await repo.GetByNameAsync("S", true);

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
            var result = (await repo.GetAllAsync(true)).ToList();

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
            var result = (await repo.SearchAsync("A", true)).ToList();

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
            var result = (await repo.SearchAsync(null, true)).ToList();

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
                .ReturnsAsync((ServiceDto)null);

            var repo = CreateRepository();

            var xml = await repo.ExportXmlAsync("A");

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
            var xml = await repo.ExportXmlAsync("A");

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

            var result = await repo.ImportXmlAsync(xml);
            Assert.True(result);
        }

        [Fact]
        public async Task ImportXML_EmptyXml_ReturnsFalse()
        {
            var repo = CreateRepository();
            var xml = string.Empty;
            var result = await repo.ImportXmlAsync(xml);
            Assert.False(result);
        }

        [Fact]
        public async Task ImportXML_InvalidXml_ReturnsFalse()
        {
            var repo = CreateRepository();
            var xml = "<ServiceDto><Name></Invalid></ServiceDto>";
            _mockXmlServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Throws<Exception>();
            var result = await repo.ImportXmlAsync(xml);
            Assert.False(result);
        }

        [Fact]
        public async Task ImportXML_ServiceNull_ReturnsFalse()
        {
            var repo = CreateRepository();
            var xml = "<ServiceDto></ServiceDto>";

            _mockXmlServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Returns((ServiceDto)null);

            var result = await repo.ImportXmlAsync(xml);
            Assert.False(result);
        }

        [Fact]
        public async Task ExportJSON_ReturnsEmptyString()
        {
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>()))
                .ReturnsAsync((ServiceDto)null);

            var repo = CreateRepository();

            var json = await repo.ExportJsonAsync("A");

            Assert.Empty(json);
        }

        [Fact]
        public async Task ExportJSON_ReturnsSerializedService()
        {
            var dto = new ServiceDto { Name = "A" };
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(dto);

            var repo = CreateRepository();
            var json = await repo.ExportJsonAsync("A");

            Assert.Contains("\"Name\": \"A\"", json);
        }

        [Fact]
        public async Task ImportJSON_ValidJson_ReturnsTrue()
        {
            var repo = CreateRepository();
            var json = "{\"Name\":\"A\"}";

            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<int>(It.IsAny<CommandDefinition>())).ReturnsAsync(0);
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);

            var result = await repo.ImportJsonAsync(json);
            Assert.True(result);
        }

        [Fact]
        public async Task ImportJSON_EmptyJson_ReturnsFalse()
        {
            var repo = CreateRepository();
            var json = string.Empty;

            var result = await repo.ImportJsonAsync(json);

            Assert.False(result);
        }

        [Fact]
        public async Task ImportJSON_NullObject_ReturnsFalse()
        {
            var repo = CreateRepository();
            var json = "null";

            var result = await repo.ImportJsonAsync(json);

            Assert.False(result);
        }

        [Fact]
        public async Task ImportJSON_InvalidJson_ReturnsFalse()
        {
            var repo = CreateRepository();
            var json = "{ invalid json }";

            var result = await repo.ImportJsonAsync(json);

            Assert.False(result);
        }

    }
}

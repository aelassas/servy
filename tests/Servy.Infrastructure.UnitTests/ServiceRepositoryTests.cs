using Dapper;
using Moq;
using Servy.Core.Data;
using Servy.Core.Domain;
using Servy.Core.DTOs;
using Servy.Core.Enums;
using Servy.Core.Helpers;
using Servy.Core.Services;
using Servy.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Servy.Infrastructure.UnitTests
{
    public class ServiceRepositoryTests
    {
        private readonly Mock<IDapperExecutor> _mockDapper;
        private readonly Mock<ISecurePassword> _mockSecurePassword;
        private readonly Mock<IXmlServiceSerializer> _mockXmlServiceSerializer;
        private readonly IServiceRepository _serviceRepository;
        private readonly ServiceRepository _repository;
        private readonly Mock<IServiceManager> _serviceManagerMock;

        public ServiceRepositoryTests()
        {
            _mockDapper = new Mock<IDapperExecutor>();
            _mockSecurePassword = new Mock<ISecurePassword>(MockBehavior.Loose);
            _mockXmlServiceSerializer = new Mock<IXmlServiceSerializer>();
            _serviceRepository = new ServiceRepositoryStub();

            _repository = new ServiceRepository(_mockDapper.Object, _mockSecurePassword.Object, _mockXmlServiceSerializer.Object); // ignore dependencies for this test
            _serviceManagerMock = new Mock<IServiceManager>();
        }

        private ServiceRepository CreateRepository()
        {
            return new ServiceRepository(_mockDapper.Object, _mockSecurePassword.Object, _mockXmlServiceSerializer.Object);
        }

        [Fact]
        public void Constructor_NullDapper_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(null, _mockSecurePassword.Object, _mockXmlServiceSerializer.Object));
        }

        [Fact]
        public void Constructor_NullSecurePassword_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(_mockDapper.Object, null, _mockXmlServiceSerializer.Object));
        }

        [Fact]
        public void Constructor_NullXmlServiceSerializer_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(_mockDapper.Object, _mockSecurePassword.Object, null));
        }

        [Fact]
        public async Task AddAsync_EncryptsPasswordAndInserts_ReturnsId()
        {
            var dto = new ServiceDto { Name = "S1", Password = "plain" };
            _mockSecurePassword.Setup(s => s.Encrypt("plain")).Returns("encrypted");
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(42);

            var repo = CreateRepository();
            var result = await repo.AddAsync(dto);

            Assert.Equal(42, result);
            Assert.Equal("encrypted", dto.Password);
        }

        [Fact]
        public async Task UpdateAsync_EncryptsPasswordAndExecutes_ReturnsAffectedRows()
        {
            var dto = new ServiceDto { Id = 1, Password = "plain" };
            _mockSecurePassword.Setup(s => s.Encrypt("plain")).Returns("encrypted");
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);

            var repo = CreateRepository();
            var rows = await repo.UpdateAsync(dto);

            Assert.Equal(1, rows);
            Assert.Equal("encrypted", dto.Password);
        }

        [Fact]
        public async Task UpsertAsync_ExistingService_Updates()
        {
            var dto = new ServiceDto { Name = "S1" };
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<int>(It.IsAny<CommandDefinition>())).ReturnsAsync(5);
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);
            _mockSecurePassword.Setup(s => s.Encrypt(It.IsAny<string>())).Returns<string>(s => s);

            var repo = CreateRepository();
            var rows = await repo.UpsertAsync(dto);

            Assert.Equal(1, rows);
            Assert.Equal(5, dto.Id);
        }

        [Fact]
        public async Task UpsertAsync_NewService_Adds()
        {
            var dto = new ServiceDto { Name = "NewService" };
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<int>(It.IsAny<CommandDefinition>())).ReturnsAsync(0);
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(7);
            _mockSecurePassword.Setup(s => s.Encrypt(It.IsAny<string>())).Returns<string>(s => s);

            var repo = CreateRepository();
            var rows = await repo.UpsertAsync(dto);

            Assert.Equal(7, rows);
        }

        [Fact]
        public async Task UpsertAsync_WithPassword_EncryptsPassword()
        {
            // Arrange
            var dto = new ServiceDto { Name = "NewService", Password = "plain" };
            _mockSecurePassword.Setup(s => s.Encrypt("plain")).Returns("encrypted");

            // Service does not exist
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<int>(It.IsAny<CommandDefinition>())).ReturnsAsync(0);

            // AddAsync returns 7
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(
                It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync(7);

            var repo = CreateRepository();

            // Act
            var result = await repo.UpsertAsync(dto);

            // Assert
            Assert.Equal(7, result);
            Assert.Equal("encrypted", dto.Password); // DTO updated correctly
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
            var dto = new ServiceDto { Id = 1, Password = "encrypted" };
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(dto);
            _mockSecurePassword.Setup(s => s.Decrypt("encrypted")).Returns("plain");

            var repo = CreateRepository();
            var result = await repo.GetByIdAsync(1);

            Assert.Equal("plain", result.Password);
        }

        [Fact]
        public async Task GetByIdAsync_EmptyPassword()
        {
            var dto = new ServiceDto { Id = 1, Password = null };
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(dto);

            var repo = CreateRepository();
            var result = await repo.GetByIdAsync(1);

            Assert.Null(result.Password);
        }

        [Fact]
        public async Task GetByIdAsync_NullDto()
        {
            ServiceDto dto = null;
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(dto);

            var repo = CreateRepository();
            var result = await repo.GetByIdAsync(1);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetByNameAsync_DecryptsPassword()
        {
            var dto = new ServiceDto { Name = "S", Password = "encrypted" };
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(dto);
            _mockSecurePassword.Setup(s => s.Decrypt("encrypted")).Returns("plain");

            var repo = CreateRepository();
            var result = await repo.GetByNameAsync("S");

            Assert.Equal("plain", result.Password);
        }

        [Fact]
        public async Task GetAllAsync_DecryptsAllPasswords()
        {
            var list = new List<ServiceDto>
            {
                new ServiceDto { Id = 1, Password = "e1" },
                new ServiceDto { Id = 2, Password = "e2" }
            };
            _mockDapper.Setup(d => d.QueryAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(list);
            _mockSecurePassword.Setup(s => s.Decrypt("e1")).Returns("p1");
            _mockSecurePassword.Setup(s => s.Decrypt("e2")).Returns("p2");

            var repo = CreateRepository();
            var result = (await repo.GetAllAsync()).ToList();

            Assert.Collection(result,
                r => Assert.Equal("p1", r.Password),
                r => Assert.Equal("p2", r.Password));
        }

        [Fact]
        public async Task Search_DecryptsPasswords()
        {
            var list = new List<ServiceDto> { new ServiceDto { Name = "A", Password = "e1" } };
            _mockDapper.Setup(d => d.QueryAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(list);
            _mockSecurePassword.Setup(s => s.Decrypt("e1")).Returns("p1");

            var repo = CreateRepository();
            var result = (await repo.Search("A")).ToList();

            Assert.Single(result);
            Assert.Equal("p1", result[0].Password);
        }

        [Fact]
        public async Task Search_NullKeyword()
        {
            var list = new List<ServiceDto> { new ServiceDto { Name = "A", Password = "e1" } };
            _mockDapper.Setup(d => d.QueryAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(list);
            _mockSecurePassword.Setup(s => s.Decrypt("e1")).Returns("p1");

            var repo = CreateRepository();
            var result = (await repo.Search(null)).ToList();

            Assert.Single(result);
            Assert.Equal("p1", result[0].Password);
        }

        [Fact]
        public async Task ExportXML_ReturnsEmptyString()
        {
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>()))
                .ReturnsAsync((ServiceDto)null);

            var repo = CreateRepository();

            var xml = await repo.ExportXML("A");

            Assert.Empty(xml);
        }

        [Fact]
        public async Task ExportXML_ReturnsSerializedService()
        {
            var dto = new ServiceDto { Name = "A", Password = "p1" };

            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>()))
                .ReturnsAsync(dto);

            _mockSecurePassword
                .Setup(s => s.Decrypt("p1"))
                .Returns("p1"); // return the same value or "plain" if you prefer

            var repo = CreateRepository();
            var xml = await repo.ExportXML("A");

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

            var result = await repo.ImportXML(xml);
            Assert.True(result);
        }

        [Fact]
        public async Task ImportXML_EmptyXml_ReturnsFalse()
        {
            var repo = CreateRepository();
            var xml = string.Empty;
            var result = await repo.ImportXML(xml);
            Assert.False(result);
        }

        [Fact]
        public async Task ImportXML_InvalidXml_ReturnsFalse()
        {
            var repo = CreateRepository();
            var xml = "<ServiceDto><Name></Invalid></ServiceDto>";
            _mockXmlServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Throws<Exception>();
            var result = await repo.ImportXML(xml);
            Assert.False(result);
        }

        [Fact]
        public async Task ImportXML_ServiceNull_ReturnsFalse()
        {
            var repo = CreateRepository();
            var xml = "<ServiceDto></ServiceDto>";

            _mockXmlServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Returns((ServiceDto)null);

            var result = await repo.ImportXML(xml);
            Assert.False(result);
        }

        [Fact]
        public async Task ExportJSON_ReturnsEmptyString()
        {
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>()))
                .ReturnsAsync((ServiceDto)null);

            var repo = CreateRepository();

            var json = await repo.ExportJSON("A");

            Assert.Empty(json);
        }

        [Fact]
        public async Task ExportJSON_ReturnsSerializedService()
        {
            var dto = new ServiceDto { Name = "A" };
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<CommandDefinition>())).ReturnsAsync(dto);

            var repo = CreateRepository();
            var json = await repo.ExportJSON("A");

            Assert.Contains("\"Name\": \"A\"", json);
        }

        [Fact]
        public async Task ImportJSON_ValidJson_ReturnsTrue()
        {
            var repo = CreateRepository();
            var json = "{\"Name\":\"A\"}";

            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<int>(It.IsAny<CommandDefinition>())).ReturnsAsync(0);
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);

            var result = await repo.ImportJSON(json);
            Assert.True(result);
        }

        [Fact]
        public async Task ImportJSON_EmptyJson_ReturnsFalse()
        {
            var repo = CreateRepository();
            var json = string.Empty;

            var result = await repo.ImportJSON(json);

            Assert.False(result);
        }

        [Fact]
        public async Task ImportJSON_NullObject_ReturnsFalse()
        {
            var repo = CreateRepository();
            var json = "null";

            var result = await repo.ImportJSON(json);

            Assert.False(result);
        }

        [Fact]
        public async Task ImportJSON_InvalidJson_ReturnsFalse()
        {
            var repo = CreateRepository();
            var json = "{ invalid json }";

            var result = await repo.ImportJSON(json);

            Assert.False(result);
        }

        [Fact]
        public async Task AddDomainServiceAsync_CallsAddAsync()
        {
            var service = new Service(null) { Name = "TestService" };
            var result = await _serviceRepository.AddDomainServiceAsync(service);
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task AddDomainServiceAsync_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _serviceRepository.AddDomainServiceAsync(null));
        }

        [Fact]
        public async Task UpdateDomainServiceAsync_CallsUpdateAsync()
        {
            var service = new Service(null) { Name = "TestService" };
            var result = await _serviceRepository.UpdateDomainServiceAsync(service);
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task UpsertDomainServiceAsync_CallsUpsertAsync()
        {
            var service = new Service(null) { Name = "TestService" };
            var result = await _serviceRepository.UpsertDomainServiceAsync(service);
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task DeleteDomainServiceAsync_ById_CallsDeleteAsync()
        {
            var result = await _serviceRepository.DeleteDomainServiceAsync(1);
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task DeleteDomainServiceAsync_ByName_CallsDeleteAsync()
        {
            var result = await _serviceRepository.DeleteDomainServiceAsync("TestService");
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task GetDomainServiceByIdAsync_ReturnsMappedService()
        {
            var serviceManager = new Mock<IServiceManager>().Object;
            var result = await _serviceRepository.GetDomainServiceByIdAsync(serviceManager, 1);
            Assert.NotNull(result);
            Assert.Equal("StubService", result.Name);
        }

        [Fact]
        public async Task GetDomainServiceByIdAsync_ReturnsNull_WhenDtoNull()
        {
            var serviceRepository = new ServiceRepositoryStub(returnNullDto: true);
            var serviceManager = new Mock<IServiceManager>().Object;
            var result = await serviceRepository.GetDomainServiceByIdAsync(serviceManager, 1);
            Assert.Null(result);
        }

        [Fact]
        public async Task GetDomainServiceByNameAsync_ReturnsMappedService()
        {
            var serviceManager = new Mock<IServiceManager>().Object;
            var result = await _serviceRepository.GetDomainServiceByNameAsync(serviceManager, "TestService");
            Assert.NotNull(result);
            Assert.Equal("TestService", result.Name);
        }

        [Fact]
        public async Task GetDomainServiceByNameAsync_ReturnsNull_WhenDtoNull()
        {
            var serviceRepository = new ServiceRepositoryStub(returnNullDto: true);
            var serviceManager = new Mock<IServiceManager>().Object;
            var result = await serviceRepository.GetDomainServiceByNameAsync(serviceManager, "TestService");
            Assert.Null(result);
        }

        [Fact]
        public async Task GetAllDomainServicesAsync_ReturnsMappedServices()
        {
            var serviceManager = new Mock<IServiceManager>().Object;
            var result = (await _serviceRepository.GetAllDomainServicesAsync(serviceManager)).ToList();
            Assert.Single(result);
            Assert.Equal("StubService", result[0].Name);
        }

        [Fact]
        public async Task SearchDomainServicesAsync_ReturnsMappedServices()
        {
            var serviceManager = new Mock<IServiceManager>().Object;
            var result = (await _serviceRepository.SearchDomainServicesAsync(serviceManager, "StubService")).ToList();
            Assert.Single(result);
            Assert.Equal("StubService", result[0].Name);
        }

        [Fact]
        public async Task ExportDomainServiceXMLAsync_ReturnsString()
        {
            var result = await _serviceRepository.ExportDomainServiceXMLAsync("Service1");
            Assert.Equal("<xml></xml>", result);
        }

        [Fact]
        public async Task ImportDomainServiceXMLAsync_ReturnsBool()
        {
            var result = await _serviceRepository.ImportDomainServiceXMLAsync("<xml></xml>");
            Assert.True(result);
        }

        [Fact]
        public async Task ExportDomainServiceJSONAsync_ReturnsString()
        {
            var result = await _serviceRepository.ExportDomainServiceJSONAsync("Service1");
            Assert.Equal("{ }", result);
        }

        [Fact]
        public async Task ImportDomainServiceJSONAsync_ReturnsBool()
        {
            var result = await _serviceRepository.ImportDomainServiceJSONAsync("{ }");
            Assert.True(result);
        }

        private Service InvokeMapToDomain(ServiceDto dto)
        {
            var method = typeof(ServiceRepository)
                .GetMethod("MapToDomain", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return (Service)method.Invoke(_repository, new object[] { _serviceManagerMock.Object, dto });
        }

        [Fact]
        public void MapToDomain_ThrowsArgumentNullException_WhenDtoIsNull()
        {
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                InvokeMapToDomain(null));

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
                EnableHealthMonitoring = true,
                HeartbeatInterval = 60,
                MaxFailedChecks = 5,
                RecoveryAction = (int)RecoveryAction.RestartService,
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
            Assert.True(domain.EnableHealthMonitoring);
            Assert.Equal(60, domain.HeartbeatInterval);
            Assert.Equal(5, domain.MaxFailedChecks);
            Assert.Equal(RecoveryAction.RestartService, domain.RecoveryAction);
            Assert.Equal(7, domain.MaxRestartAttempts);
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
        }

        [Fact]
        public void MapToDomain_UsesDefaultValues_WhenDtoValuesAreNull()
        {
            var dto = new ServiceDto(); // all nulls

            var domain = InvokeMapToDomain(dto);

            Assert.Equal(ServiceStartType.Manual, domain.StartupType);
            Assert.Equal(ProcessPriority.Normal, domain.Priority);
            Assert.False(domain.EnableRotation);
            Assert.Equal(1_048_576, domain.RotationSize);
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

            return (ServiceDto)method.Invoke(_repository, new object[] { domain });
        }

        [Fact]
        public void MapToDto_ThrowsArgumentNullException_WhenDomainIsNull()
        {
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                InvokeMapToDto(null));

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

using Moq;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Infrastructure.Data;
using System.Collections.Generic;
using System.IO;
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

        public ServiceRepositoryTests()
        {
            _mockDapper = new Mock<IDapperExecutor>();
            _mockSecurePassword = new Mock<ISecurePassword>(MockBehavior.Loose);
            _mockXmlServiceSerializer = new Mock<IXmlServiceSerializer>();
        }

        private ServiceRepository CreateRepository()
        {
            return new ServiceRepository(_mockDapper.Object, _mockSecurePassword.Object, _mockXmlServiceSerializer.Object);
        }

        [Fact]
        public void Constructor_NullDapper_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(null!, _mockSecurePassword.Object, _mockXmlServiceSerializer.Object));
        }

        [Fact]
        public void Constructor_NullSecurePassword_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(_mockDapper.Object, null!, _mockXmlServiceSerializer.Object));
        }

        [Fact]
        public void Constructor_NullXmlServiceSerializer_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ServiceRepository(_mockDapper.Object, _mockSecurePassword.Object, null!));
        }

        [Fact]
        public async Task AddAsync_EncryptsPasswordAndInserts_ReturnsId()
        {
            var dto = new ServiceDto { Name = "S1", Password = "plain" };
            _mockSecurePassword.Setup(s => s.Encrypt("plain")).Returns("encrypted");
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync(42);

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
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync(1);

            var repo = CreateRepository();
            var rows = await repo.UpdateAsync(dto);

            Assert.Equal(1, rows);
            Assert.Equal("encrypted", dto.Password);
        }

        [Fact]
        public async Task UpsertAsync_ExistingService_Updates()
        {
            var dto = new ServiceDto { Name = "S1" };
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<int>(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync(5);
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync(1);
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
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<int>(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync(0);
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync(7);
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
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<int>(
                It.IsAny<string>(), It.IsAny<object?>()))
                .ReturnsAsync(0);

            // AddAsync returns 7
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(
                It.IsAny<string>(), It.IsAny<object?>()))
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
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync(1);

            var repo = CreateRepository();
            var rows = await repo.DeleteAsync(10);

            Assert.Equal(1, rows);
        }

        [Fact]
        public async Task DeleteAsync_ByName_ReturnsAffectedRows()
        {
            _mockDapper.Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync(1);

            var repo = CreateRepository();
            var rows = await repo.DeleteAsync("ServiceName");

            Assert.Equal(1, rows);
        }

        [Fact]
        public async Task GetByIdAsync_DecryptsPassword()
        {
            var dto = new ServiceDto { Id = 1, Password = "encrypted" };
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync(dto);
            _mockSecurePassword.Setup(s => s.Decrypt("encrypted")).Returns("plain");

            var repo = CreateRepository();
            var result = await repo.GetByIdAsync(1);

            Assert.Equal("plain", result!.Password);
        }

        [Fact]
        public async Task GetByNameAsync_DecryptsPassword()
        {
            var dto = new ServiceDto { Name = "S", Password = "encrypted" };
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync(dto);
            _mockSecurePassword.Setup(s => s.Decrypt("encrypted")).Returns("plain");

            var repo = CreateRepository();
            var result = await repo.GetByNameAsync("S");

            Assert.Equal("plain", result!.Password);
        }

        [Fact]
        public async Task GetAllAsync_DecryptsAllPasswords()
        {
            var list = new List<ServiceDto>
            {
                new ServiceDto { Id = 1, Password = "e1" },
                new ServiceDto { Id = 2, Password = "e2" }
            };
            _mockDapper.Setup(d => d.QueryAsync<ServiceDto>(It.IsAny<string>(), null)).ReturnsAsync(list);
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
            _mockDapper.Setup(d => d.QueryAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync(list);
            _mockSecurePassword.Setup(s => s.Decrypt("e1")).Returns("p1");

            var repo = CreateRepository();
            var result = (await repo.Search("A")).ToList();

            Assert.Single(result);
            Assert.Equal("p1", result[0].Password);
        }

        [Fact]
        public async Task ExportXML_ReturnsEmptyString()
        {
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object?>()))
                .ReturnsAsync((ServiceDto?)null);

            var repo = CreateRepository();

            var xml = await repo.ExportXML("A");

            Assert.Empty(xml);
        }

        [Fact]
        public async Task ExportXML_ReturnsSerializedService()
        {
            var dto = new ServiceDto { Name = "A", Password = "p1" };

            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object?>()))
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
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<int>(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync(0);
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync(1);

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

            _mockXmlServiceSerializer.Setup(d => d.Deserialize(It.IsAny<string>())).Returns((ServiceDto?)null);

            var result = await repo.ImportXML(xml);
            Assert.False(result);
        }

        [Fact]
        public async Task ExportJSON_ReturnsEmptyString()
        {
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object?>()))
                .ReturnsAsync((ServiceDto?)null);

            var repo = CreateRepository();

            var json = await repo.ExportJSON("A");

            Assert.Empty(json);
        }

        [Fact]
        public async Task ExportJSON_ReturnsSerializedService()
        {
            var dto = new ServiceDto { Name = "A" };
            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<ServiceDto>(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync(dto);

            var repo = CreateRepository();
            var json = await repo.ExportJSON("A");

            Assert.Contains("\"Name\": \"A\"", json);
        }

        [Fact]
        public async Task ImportJSON_ValidJson_ReturnsTrue()
        {
            var repo = CreateRepository();
            var json = "{\"Name\":\"A\"}";

            _mockDapper.Setup(d => d.QuerySingleOrDefaultAsync<int>(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync(0);
            _mockDapper.Setup(d => d.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object?>())).ReturnsAsync(1);

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
        public async Task ImportJSON_InvalidJson_ReturnsFalse()
        {
            var repo = CreateRepository();
            var json = "{ invalid json }";

            var result = await repo.ImportJSON(json);

            Assert.False(result);
        }
    }
}

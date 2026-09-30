using Moq;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using Moq;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Infrastructure.Data;
using System.Data;

namespace Servy.Infrastructure.UnitTests.Data
{
    public class ServiceStateRepositoryTests
    {
        private readonly Mock<IDapperExecutor> _mockDapper;

        public ServiceStateRepositoryTests()
        {
            _mockDapper = new Mock<IDapperExecutor>();
        }

        private ServiceStateRepository CreateRepository() => new ServiceStateRepository(_mockDapper.Object);

        [Fact]
        public void Constructor_NullExecutor_Throws()
        {
            // Arrange, Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ServiceStateRepository(null));
        }

        [Fact]
        public async Task GetAsync_ExistingRow_ReturnsIt()
        {
            // Arrange
            var stored = new ServiceStateDto { Name = "svc", Pid = 4242, ActiveStdoutPath = @"C:\logs\out.log" };
            string capturedSql = null;
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceStateDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .Callback<string, object, IDbTransaction, CancellationToken>((sql, prm, tx, ct) => capturedSql = sql)
                .ReturnsAsync(stored);

            // Act
            var result = await CreateRepository().GetAsync("svc");

            // Assert
            Assert.Same(stored, result);
            Assert.Contains(StateSqlConstants.ServiceStateTableName, capturedSql);
            Assert.Contains("WHERE Name = @Name", capturedSql);
        }

        [Fact]
        public async Task GetAsync_MissingRow_ReturnsNull()
        {
            // Arrange
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceStateDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ServiceStateDto)null);

            // Act
            var result = await CreateRepository().GetAsync("svc");

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetAsync_BlankName_Throws(string name)
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => CreateRepository().GetAsync(name));
        }

        [Fact]
        public async Task GetAsync_PaddedName_QueriesTheTrimmedKey()
        {
            // Arrange
            object captured = null;
            _mockDapper
                .Setup(d => d.QuerySingleOrDefaultAsync<ServiceStateDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .Callback<string, object, IDbTransaction, CancellationToken>((sql, prm, tx, ct) => captured = prm)
                .ReturnsAsync((ServiceStateDto)null);

            // Act
            await CreateRepository().GetAsync("  svc  ");

            // Assert
            Assert.Equal("svc", captured.GetType().GetProperty("Name").GetValue(captured));
        }

        [Fact]
        public async Task GetAllAsync_OrdersByName()
        {
            // Arrange
            string capturedSql = null;
            var rows = new[] { new ServiceStateDto { Name = "a" }, new ServiceStateDto { Name = "b" } };
            _mockDapper
                .Setup(d => d.QueryAsync<ServiceStateDto>(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .Callback<string, object, IDbTransaction, CancellationToken>((sql, prm, tx, ct) => capturedSql = sql)
                .ReturnsAsync(rows);

            // Act
            var result = await CreateRepository().GetAllAsync();

            // Assert
            Assert.Equal(2, result.Count());
            Assert.Contains("ORDER BY Name", capturedSql);
        }

        [Fact]
        public async Task UpsertAsync_WritesNameAndTheThreeRuntimeColumns()
        {
            // Arrange
            string capturedSql = null;
            object captured = null;
            _mockDapper
                .Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .Callback<string, object, IDbTransaction, CancellationToken>((sql, prm, tx, ct) => { capturedSql = sql; captured = prm; })
                .ReturnsAsync(1);

            var state = new ServiceStateDto
            {
                Name = "svc",
                Pid = 17,
                ActiveStdoutPath = @"C:\logs\out.log",
                ActiveStderrPath = @"C:\logs\err.log"
            };

            // Act
            var rows = await CreateRepository().UpsertAsync(state);

            // Assert
            Assert.Equal(1, rows);
            Assert.Contains("ON CONFLICT(Name) DO UPDATE SET", capturedSql);
            var type = captured.GetType();
            Assert.Equal("svc", type.GetProperty("Name").GetValue(captured));
            Assert.Equal(17, type.GetProperty("Pid").GetValue(captured));
            Assert.Equal(@"C:\logs\out.log", type.GetProperty("ActiveStdoutPath").GetValue(captured));
            Assert.Equal(@"C:\logs\err.log", type.GetProperty("ActiveStderrPath").GetValue(captured));
        }

        [Fact]
        public async Task UpsertAsync_NullState_Throws()
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => CreateRepository().UpsertAsync(null));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task UpsertAsync_BlankName_Throws(string name)
        {
            // Arrange
            var state = new ServiceStateDto { Name = name };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => CreateRepository().UpsertAsync(state));
        }

        [Fact]
        public async Task DeleteAsync_MissingRow_ReturnsZero()
        {
            // Arrange
            string capturedSql = null;
            _mockDapper
                .Setup(d => d.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
                .Callback<string, object, IDbTransaction, CancellationToken>((sql, prm, tx, ct) => capturedSql = sql)
                .ReturnsAsync(0);

            // Act
            var rows = await CreateRepository().DeleteAsync("svc");

            // Assert
            Assert.Equal(0, rows);
            Assert.Contains($"DELETE FROM {StateSqlConstants.ServiceStateTableName}", capturedSql);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task DeleteAsync_BlankName_Throws(string name)
        {
            // Arrange, Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => CreateRepository().DeleteAsync(name));
        }

        [Fact]
        public void StateSqlConstants_ColumnSetIsExactlyTheRuntimeState()
        {
            // Arrange
            var expected = new[] { "Pid", "ActiveStdoutPath", "ActiveStderrPath" };

            // Act
            var actual = StateSqlConstants.Columns;

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void StateSqlConstants_EveryColumnHasASqlColumnAttributeOnTheDto()
        {
            // Arrange
            var declared = typeof(ServiceStateDto).GetProperties()
                .Where(p => p.GetCustomAttributes(typeof(SqlColumnAttribute), inherit: false).Length > 0)
                .Select(p => p.Name);
            var declaredSet = new HashSet<string>(declared, StringComparer.OrdinalIgnoreCase);

            // Act
            var missing = StateSqlConstants.Columns.Where(c => !declaredSet.Contains(c)).ToList();

            // Assert
            Assert.Empty(missing);
            Assert.Equal(StateSqlConstants.Columns.Count, declaredSet.Count);
        }
    }
}

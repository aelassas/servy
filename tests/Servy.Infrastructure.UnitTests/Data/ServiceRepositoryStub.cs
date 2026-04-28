using Dapper;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Security;
using Servy.Core.Services;
using Servy.Infrastructure.Data;

namespace Servy.Infrastructure.UnitTests.Data
{
    /// <summary>
    /// A stub repository for unit testing domain methods.
    /// Overrides public DTO methods to simulate database behavior.
    /// </summary>
    public class ServiceRepositoryStub : ServiceRepository
    {
        private bool _returnNullDto;
        private const string EncryptedPrefix = "[ENC]";

        public ServiceRepositoryStub(bool returnNullDto = false)
            : base(
                new DapperExecutorStub(),
                new SecureDataStub(),
                new XmlServiceSerializerStub(),
                new JsonServiceSerializerStub()
            )
        {
            _returnNullDto = returnNullDto;
        }

        /// <summary>
        /// Generates a consistent ServiceDto for the stub to return, 
        /// applying simulated encryption if requested.
        /// </summary>
        private ServiceDto CreateConsistentDto(int id, string? name, bool decrypt)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));

            string FormatValue(string value) => decrypt ? value : $"{EncryptedPrefix}{value}";

            return new ServiceDto
            {
                Id = id,
                Name = name,
                // Simulate sensitive fields that would normally be encrypted
                ActiveStderrPath = FormatValue(@"C:\logs\stub_active_stderr.log"),
                ActiveStdoutPath = FormatValue(@"C:\logs\stub_active_stdout.log")
            };
        }

        // -------------------- DTO METHODS OVERRIDES --------------------
        public override Task<int> AddAsync(ServiceDto service, CancellationToken token = default)
        {
            service.Id = 1;
            return Task.FromResult(1);
        }

        public override Task<int> UpdateAsync(ServiceDto service, CancellationToken token = default) => Task.FromResult(1);
        public override Task<int> UpsertAsync(ServiceDto service, CancellationToken token = default) => Task.FromResult(1);
        public override Task<int> DeleteAsync(int id, CancellationToken token = default) => Task.FromResult(1);
        public override Task<int> DeleteAsync(string? name, CancellationToken token = default) => Task.FromResult(1);

        public override Task<ServiceDto?> GetByIdAsync(int id, bool decrypt = true, CancellationToken token = default)
        {
            return _returnNullDto
                ? Task.FromResult<ServiceDto?>(null)
                : Task.FromResult<ServiceDto?>(CreateConsistentDto(id, "StubService", decrypt));
        }

        public override Task<ServiceDto?> GetByNameAsync(string? name, bool decrypt = true, CancellationToken token = default)
        {
            // Fixes the asymmetry bug by returning the same shape as GetByIdAsync
            return _returnNullDto
                ? Task.FromResult<ServiceDto?>(null)
                : Task.FromResult<ServiceDto?>(CreateConsistentDto(1, name, decrypt));
        }

        public override Task<IEnumerable<ServiceDto>> GetAllAsync(bool decrypt = true, CancellationToken token = default)
        {
            return Task.FromResult<IEnumerable<ServiceDto>>(new List<ServiceDto> { CreateConsistentDto(1, "StubService", decrypt) });
        }

        public override Task<IEnumerable<ServiceDto>> SearchAsync(string keyword, bool decrypt = true, CancellationToken token = default)
        {
            return Task.FromResult<IEnumerable<ServiceDto>>(new List<ServiceDto> { CreateConsistentDto(1, "StubService", decrypt) });
        }

        public override Task<string> ExportXmlAsync(string? name, CancellationToken token = default) => Task.FromResult("<xml></xml>");
        public override Task<bool> ImportXmlAsync(string xml, CancellationToken token = default) => Task.FromResult(true);
        public override Task<string> ExportJsonAsync(string? name, CancellationToken token = default) => Task.FromResult("{ }");
        public override Task<bool> ImportJsonAsync(string json, CancellationToken token = default) => Task.FromResult(true);
    }

    // Dummy stubs for the constructor dependencies
    public class DapperExecutorStub : IDapperExecutor
    {
        public int Execute(string sql, object? param = null)
        {
            throw new NotImplementedException();
        }

        public Task<int> ExecuteAsync(string sql, object? param = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public T? ExecuteScalar<T>(string sql, object? param = null)
        {
            throw new NotImplementedException();
        }

        public Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<T> Query<T>(string sql, object? param = null)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> QueryAsync<T>(CommandDefinition command)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public T? QuerySingleOrDefault<T>(string sql, object? param = null)
        {
            throw new NotImplementedException();
        }

        public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<T?> QuerySingleOrDefaultAsync<T>(CommandDefinition command)
        {
            throw new NotImplementedException();
        }
    }

    public class SecureDataStub : ISecureData
    {
        public string Encrypt(string value) => value;
        public string Decrypt(string value) => value;

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    public class XmlServiceSerializerStub : IXmlServiceSerializer
    {
        public ServiceDto Deserialize(string? xml) => new ServiceDto { Name = "StubService" };
    }

    public class JsonServiceSerializerStub : IJsonServiceSerializer
    {
        public ServiceDto Deserialize(string? json) => new ServiceDto { Name = "StubService" };

        public string? Serialize(ServiceDto? dto)
        {
            return dto == null ? null : "{ \"Name\": \"" + dto.Name + "\" }";
        }
    }

}

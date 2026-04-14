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

        public ServiceRepositoryStub(bool returnNullDto = false)
            : base(
                new DapperExecutorStub(),       // replace with stub or mock
                new SecureDataStub(),           // replace with stub or mock
                new XmlServiceSerializerStub(), // replace with stub or mock
                new JsonServiceSerializerStub()
            )
        {
            _returnNullDto = returnNullDto;
        }

        // -------------------- DTO METHODS OVERRIDES --------------------
        public override Task<int> AddAsync(ServiceDto service, CancellationToken token = default)
        {
            // Simulate DB insert
            service.Id = 1;
            return Task.FromResult(1);
        }

        public override Task<int> UpdateAsync(ServiceDto service, CancellationToken token = default)
        {
            return Task.FromResult(1);
        }

        public override Task<int> UpsertAsync(ServiceDto service, CancellationToken token = default)
        {
            return Task.FromResult(1);
        }

        public override Task<int> DeleteAsync(int id, CancellationToken token = default)
        {
            return Task.FromResult(1);
        }

        public override Task<int> DeleteAsync(string name, CancellationToken token = default)
        {
            return Task.FromResult(1);
        }

        public override Task<ServiceDto?> GetByIdAsync(int id, bool decrypt = true, CancellationToken token = default)
        {
            return _returnNullDto 
                ? Task.FromResult<ServiceDto?>(null) 
                : Task.FromResult<ServiceDto?>(new ServiceDto 
                {
                    Id = id, 
                    Name = "StubService",
                    ActiveStderrPath = @"C:\logs\stub_active_stderr.log",
                    ActiveStdoutPath = @"C:\logs\stub_active_stdout.log"
                });
        }

        public override Task<ServiceDto?> GetByNameAsync(string name, bool decrypt = true, CancellationToken token = default)
        {
            return _returnNullDto ? Task.FromResult<ServiceDto?>(null) : Task.FromResult<ServiceDto?>(new ServiceDto { Name = name });
        }

        public override Task<IEnumerable<ServiceDto>> GetAllAsync(bool decrypt = true, CancellationToken token = default)
        {
            return Task.FromResult<IEnumerable<ServiceDto>>(new List<ServiceDto> { new ServiceDto { Name = "StubService" } });
        }

        public override Task<IEnumerable<ServiceDto>> SearchAsync(string keyword, bool decrypt = true, CancellationToken token = default)
        {
            return Task.FromResult<IEnumerable<ServiceDto>>(new List<ServiceDto> { new ServiceDto { Name = "StubService" } });
        }

        public override Task<string> ExportXmlAsync(string name, CancellationToken token = default)
        {
            return Task.FromResult("<xml></xml>");
        }

        public override Task<bool> ImportXmlAsync(string xml, CancellationToken token = default)
        {
            return Task.FromResult(true);
        }

        public override Task<string> ExportJsonAsync(string name, CancellationToken token = default)
        {
            return Task.FromResult("{ }");
        }

        public override Task<bool> ImportJsonAsync(string json, CancellationToken token = default)
        {
            return Task.FromResult(true);
        }
    }

    // Dummy stubs for the constructor dependencies
    public class DapperExecutorStub : IDapperExecutor
    {
        public int Execute(string sql, object? param = null)
        {
            throw new NotImplementedException();
        }

        public Task<int> ExecuteAsync(string sql, object? param = null)
        {
            throw new NotImplementedException();
        }

        public T? ExecuteScalar<T>(string sql, object? param = null)
        {
            throw new NotImplementedException();
        }

        public Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null)
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

        public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
        {
            throw new NotImplementedException();
        }

        public T? QuerySingleOrDefault<T>(string sql, object? param = null)
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
    }

}

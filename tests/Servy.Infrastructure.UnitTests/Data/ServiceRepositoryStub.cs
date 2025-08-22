using Dapper;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Helpers;
using Servy.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
                new SecurePasswordStub(),       // replace with stub or mock
                new XmlServiceSerializerStub()  // replace with stub or mock
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

        public override Task<ServiceDto> GetByIdAsync(int id, CancellationToken token = default)
        {
            return _returnNullDto ? Task.FromResult<ServiceDto>(null) : Task.FromResult<ServiceDto>(new ServiceDto { Id = id, Name = "StubService" });
        }

        public override Task<ServiceDto> GetByNameAsync(string name, CancellationToken token = default)
        {
            return _returnNullDto ? Task.FromResult<ServiceDto>(null) : Task.FromResult<ServiceDto>(new ServiceDto { Name = name });
        }

        public override Task<IEnumerable<ServiceDto>> GetAllAsync( CancellationToken token = default)
        {
            return Task.FromResult<IEnumerable<ServiceDto>>(new List<ServiceDto> { new ServiceDto { Name = "StubService" } });
        }

        public override Task<IEnumerable<ServiceDto>> Search(string keyword, CancellationToken token = default)
        {
            return Task.FromResult<IEnumerable<ServiceDto>>(new List<ServiceDto> { new ServiceDto { Name = "StubService" } });
        }

        public override Task<string> ExportXML(string name, CancellationToken token = default)
        {
            return Task.FromResult("<xml></xml>");
        }

        public override Task<bool> ImportXML(string xml, CancellationToken token = default)
        {
            return Task.FromResult(true);
        }

        public override Task<string> ExportJSON(string name, CancellationToken token = default)
        {
            return Task.FromResult("{ }");
        }

        public override Task<bool> ImportJSON(string json, CancellationToken token = default)
        {
            return Task.FromResult(true);
        }
    }

    // Dummy stubs for the constructor dependencies
    public class DapperExecutorStub : IDapperExecutor
    {
        public Task<int> ExecuteAsync(string sql, object param = null)
        {
            throw new NotImplementedException();
        }

        public Task<T> ExecuteScalarAsync<T>(string sql, object param = null)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> QueryAsync<T>(CommandDefinition command)
        {
            throw new NotImplementedException();
        }

        public Task<T> QuerySingleOrDefaultAsync<T>(CommandDefinition command)
        {
            throw new NotImplementedException();
        }
    }
    public class SecurePasswordStub : ISecurePassword
    {
        public string Encrypt(string value) => value;
        public string Decrypt(string value) => value;
    }
    public class XmlServiceSerializerStub : IXmlServiceSerializer
    {
        public ServiceDto Deserialize(string xml) => new ServiceDto { Name = "StubService" };
    }
}

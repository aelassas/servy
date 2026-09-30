using Servy.Core.Data;
using Servy.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Servy.Core.Data;
using Servy.Core.DTOs;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Reads and writes runtime-state rows in the runtime-state database (<c>Servy.state.db</c>).
    /// </summary>
    /// <remarks>
    /// Every statement is built from <see cref="StateSqlConstants"/>, so the column set has one home.
    /// The executor this repository is given must be bound to the runtime-state connection string, not
    /// to the configuration database: nothing here checks which file it is talking to.
    /// </remarks>
    public class ServiceStateRepository : IServiceStateRepository
    {
        private readonly IDapperExecutor _executor;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceStateRepository"/> class.
        /// </summary>
        /// <param name="executor">The executor bound to the runtime-state database connection.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="executor"/> is null.</exception>
        public ServiceStateRepository(IDapperExecutor executor)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        /// <inheritdoc />
        public async Task<ServiceStateDto> GetAsync(string name, CancellationToken cancellationToken = default)
        {
            var key = NormalizeName(name);

            var sql = $"SELECT {StateSqlConstants.SelectColumns} FROM {StateSqlConstants.ServiceStateTableName} WHERE Name = @Name;";

            return await _executor.QuerySingleOrDefaultAsync<ServiceStateDto>(
                sql,
                new { Name = key },
                cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<ServiceStateDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var sql = $"SELECT {StateSqlConstants.SelectColumns} FROM {StateSqlConstants.ServiceStateTableName} ORDER BY Name;";

            return await _executor.QueryAsync<ServiceStateDto>(sql, cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> UpsertAsync(ServiceStateDto state, CancellationToken cancellationToken = default)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var key = NormalizeName(state.Name);

            var sql = $@"
                INSERT INTO {StateSqlConstants.ServiceStateTableName} ({StateSqlConstants.InsertColumns})
                VALUES ({StateSqlConstants.InsertValues})
                ON CONFLICT(Name) DO UPDATE SET {StateSqlConstants.UpsertSet};";

            return await _executor.ExecuteAsync(
                sql,
                new
                {
                    Name = key,
                    state.Pid,
                    state.ActiveStdoutPath,
                    state.ActiveStderrPath
                },
                cancellationToken: cancellationToken);
        }

        /// <inheritdoc />
        public async Task<int> DeleteAsync(string name, CancellationToken cancellationToken = default)
        {
            var key = NormalizeName(name);

            var sql = $"DELETE FROM {StateSqlConstants.ServiceStateTableName} WHERE Name = @Name;";

            return await _executor.ExecuteAsync(
                sql,
                new { Name = key },
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Validates and trims a service name used as the row key.
        /// </summary>
        /// <remarks>
        /// The name is trimmed for the same reason the configuration repository trims it: a padded name
        /// would create a second, unreachable row for the same service.
        /// </remarks>
        /// <param name="name">The service name to normalize.</param>
        /// <returns>The trimmed name.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty or whitespace.</exception>
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Service name cannot be null, empty or whitespace.", nameof(name));
            }

            return name.Trim();
        }
    }
}

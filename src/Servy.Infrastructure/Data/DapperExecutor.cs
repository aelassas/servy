using Dapper;
using Servy.Core.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Executes SQL commands and queries using Dapper.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class DapperExecutor : IDapperExecutor
    {
        private readonly IAppDbContext _dbContext;

        /// <summary>
        /// Initializes a new instance of <see cref="DapperExecutor"/>.
        /// </summary>
        /// <param name="dbContext">The database context. Cannot be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="dbContext"/> is null.</exception>
        public DapperExecutor(IAppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        /// <summary>
        /// Executes a SQL command that returns a single scalar value.
        /// </summary>
        /// <typeparam name="T">The type of the scalar value.</typeparam>
        /// <param name="sql">The SQL query or command. Cannot be null.</param>
        /// <param name="param">Optional parameters for the command.</param>
        /// <returns>The scalar result of type <typeparamref name="T"/>.</returns>
        public Task<T> ExecuteScalarAsync<T>(string sql, object param = null)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            using (var connection = _dbContext.CreateConnection())
            {
                connection.Open();
                return connection.ExecuteScalarAsync<T>(sql, param);
            }
        }

        /// <summary>
        /// Executes a SQL command that does not return a result set.
        /// </summary>
        /// <param name="sql">The SQL query. Cannot be null.</param>
        /// <param name="param">Optional parameters for the command.</param>
        /// <returns>The number of affected rows.</returns>
        public Task<int> ExecuteAsync(string sql, object param = null)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            using (var connection = _dbContext.CreateConnection())
            {
                connection.Open();
                return connection.ExecuteAsync(sql, param);
            }
        }

        /// <summary>
        /// Executes a SQL query and returns a collection of results.
        /// </summary>
        /// <typeparam name="T">The type of the result objects.</typeparam>
        /// <param name="command">The SQL query. Cannot be null.</param>
        /// <returns>A collection of results.</returns>
        public Task<IEnumerable<T>> QueryAsync<T>(CommandDefinition command)
        {
            using (var connection = _dbContext.CreateConnection())
            {
                connection.Open();
                return connection.QueryAsync<T>(command);
            }
        }

        /// <summary>
        /// Executes a SQL query and returns a single result or default if no result exists.
        /// </summary>
        /// <typeparam name="T">The type of the result object.</typeparam>
        /// <param name="command">The SQL query. Cannot be null.</param>
        /// <returns>The single result or default value of <typeparamref name="T"/>.</returns>
        public Task<T> QuerySingleOrDefaultAsync<T>(CommandDefinition command)
        {
            using (var connection = _dbContext.CreateConnection())
            {
                connection.Open();
                return connection.QuerySingleOrDefaultAsync<T>(command);
            }
        }
    }
}

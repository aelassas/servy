using Dapper;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Servy.Core.Data
{
    /// <summary>
    /// Abstraction for executing database operations using Dapper.
    /// This allows mocking database calls in unit tests without directly
    /// mocking <see cref="System.Data.IDbConnection"/> extension methods.
    /// </summary>
    public interface IDapperExecutor
    {
        /// <summary>
        /// Executes a SQL command that returns a scalar value.
        /// </summary>
        /// <typeparam name="T">The type of the scalar result.</typeparam>
        /// <param name="sql">The SQL query or command.</param>
        /// <param name="param">Optional parameters for the SQL command.</param>
        /// <returns>A task representing the asynchronous operation, with the scalar result of type <typeparamref name="T"/>.</returns>
        Task<T> ExecuteScalarAsync<T>(string sql, object param = null);

        /// <summary>
        /// Executes a SQL command that does not return a result set (INSERT, UPDATE, DELETE).
        /// </summary>
        /// <param name="sql">The SQL query.</param>
        /// <param name="param">Optional parameters for the SQL command.</param>
        /// <returns>A task representing the asynchronous operation, with the number of affected rows.</returns>
        Task<int> ExecuteAsync(string sql, object param = null);

        /// <summary>
        /// Executes a SQL query that returns a collection of entities.
        /// </summary>
        /// <typeparam name="T">The type of entity returned by the query.</typeparam>
        /// <param name="command">The SQL command.</param>
        /// <returns>A task representing the asynchronous operation, with the resulting collection of <typeparamref name="T"/>.</returns>
        Task<IEnumerable<T>> QueryAsync<T>(CommandDefinition command);

        /// <summary>
        /// Executes a SQL query that returns a single entity or default if none found.
        /// </summary>
        /// <typeparam name="T">The type of entity returned by the query.</typeparam>
        /// <param name="command">The SQL command.</param>
        /// <returns>A task representing the asynchronous operation, with the resulting <typeparamref name="T"/> entity, or <c>null</c> if not found.</returns>
        Task<T> QuerySingleOrDefaultAsync<T>(CommandDefinition command);
    }
}

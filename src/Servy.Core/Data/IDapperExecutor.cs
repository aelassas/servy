using Dapper;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Core.Data
{
    /// <summary>
    /// Abstraction for executing database operations using Dapper.
    /// This allows mocking database calls in unit tests without directly
    /// mocking <see cref="System.Data.DbConnection"/> extension methods.
    /// </summary>
    public interface IDapperExecutor
    {
        /// <summary>
        /// Begins a database transaction. The caller is responsible for committing and disposing the transaction.
        /// </summary>
        /// <returns>An <see cref="IDbTransaction"/> that orchestrates the connection lifecycle.</returns>
        IDbTransaction BeginTransaction();

        /// <summary>
        /// Executes a SQL command synchronously that returns a scalar value.
        /// </summary>
        /// <typeparam name="T">The type of the scalar result.</typeparam>
        /// <param name="sql">The SQL query or command statement.</param>
        /// <param name="param">Optional parameters for the SQL command.</param>
        /// <param name="transaction">Optional transaction context.</param>
        /// <returns>The scalar result of type <typeparamref name="T"/>, or default value.</returns>
        T ExecuteScalar<T>(string sql, object param = null, IDbTransaction transaction = null);

        /// <summary>
        /// Executes a SQL command synchronously (INSERT, UPDATE, DELETE).
        /// </summary>
        /// <param name="sql">The SQL statement to execute.</param>
        /// <param name="param">Optional parameters for the SQL command.</param>
        /// <param name="transaction">Optional transaction context.</param>
        /// <returns>The number of rows affected by the command execution.</returns>
        int Execute(string sql, object param = null, IDbTransaction transaction = null);

        /// <summary>
        /// Executes a query synchronously that returns a collection of entities.
        /// </summary>
        /// <typeparam name="T">The type of entity returned by the query.</typeparam>
        /// <param name="sql">The SQL query statement.</param>
        /// <param name="param">Optional parameters for the SQL query.</param>
        /// <param name="transaction">Optional transaction context.</param>
        /// <returns>An enumerable collection of results of type <typeparamref name="T"/>.</returns>
        IEnumerable<T> Query<T>(string sql, object param = null, IDbTransaction transaction = null);

        /// <summary>
        /// Executes a query synchronously that returns a single entity or default.
        /// </summary>
        /// <typeparam name="T">The type of entity returned by the query.</typeparam>
        /// <param name="sql">The SQL query statement.</param>
        /// <param name="param">Optional parameters for the SQL query.</param>
        /// <param name="transaction">Optional transaction context.</param>
        /// <returns>The single mapped entity of type <typeparamref name="T"/>, or <c>null</c> if not found.</returns>
        T QuerySingleOrDefault<T>(string sql, object param = null, IDbTransaction transaction = null);

        /// <summary>
        /// Executes a SQL command asynchronously that returns a scalar value.
        /// </summary>
        /// <typeparam name="T">The type of the scalar result.</typeparam>
        /// <param name="sql">The SQL query or command.</param>
        /// <param name="param">Optional parameters for the SQL command.</param>
        /// <param name="transaction">Optional transaction context.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A task representing the asynchronous operation, with the scalar result of type <typeparamref name="T"/>.</returns>
        Task<T> ExecuteScalarAsync<T>(string sql, object param = null, IDbTransaction transaction = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a SQL command asynchronously that does not return a result set (INSERT, UPDATE, DELETE).
        /// </summary>
        /// <param name="sql">The SQL query.</param>
        /// <param name="param">Optional parameters for the SQL command.</param>
        /// <param name="transaction">Optional transaction context.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A task representing the asynchronous operation, with the number of affected rows.</returns>
        Task<int> ExecuteAsync(string sql, object param = null, IDbTransaction transaction = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a SQL query asynchronously that returns a collection of entities.
        /// </summary>
        /// <typeparam name="T">The type of entity returned by the query.</typeparam>
        /// <param name="command">The SQL command definition.</param>
        /// <returns>A task representing the asynchronous operation, with the resulting collection of <typeparamref name="T"/>.</returns>
        Task<IEnumerable<T>> QueryAsync<T>(CommandDefinition command);

        /// <summary>
        /// Executes a SQL query asynchronously that returns a collection of entities.
        /// </summary>
        /// <typeparam name="T">The type of entity returned by the query.</typeparam>
        /// <param name="sql">The SQL query statement.</param>
        /// <param name="param">Optional parameters for the SQL query.</param>
        /// <param name="transaction">Optional transaction context.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A task representing the asynchronous operation, with the resulting collection of <typeparamref name="T"/>.</returns>
        Task<IEnumerable<T>> QueryAsync<T>(string sql, object param = null, IDbTransaction transaction = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a SQL query asynchronously that returns a single entity or default.
        /// </summary>
        /// <typeparam name="T">The type of entity returned by the query.</typeparam>
        /// <param name="sql">The SQL query statement.</param>
        /// <param name="param">Optional parameters for the SQL query.</param>
        /// <param name="transaction">Optional transaction context.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A task representing the asynchronous operation, with the mapped entity of type <typeparamref name="T"/>, or <c>null</c> if not found.</returns>
        Task<T> QuerySingleOrDefaultAsync<T>(string sql, object param = null, IDbTransaction transaction = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a SQL query asynchronously that returns a single entity or default if none found.
        /// </summary>
        /// <typeparam name="T">The type of entity returned by the query.</typeparam>
        /// <param name="command">The SQL command definition.</param>
        /// <returns>A task representing the asynchronous operation, with the resulting <typeparamref name="T"/> entity, or <c>null</c> if not found.</returns>
        Task<T> QuerySingleOrDefaultAsync<T>(CommandDefinition command);

        /// <summary>
        /// Executes a query asynchronously and returns the first row, or a default value if the sequence contains no elements.
        /// </summary>
        /// <typeparam name="T">The type of entity returned by the query.</typeparam>
        /// <param name="sql">The SQL query statement.</param>
        /// <param name="param">Optional parameters for the SQL query.</param>
        /// <param name="transaction">Optional transaction context.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>A task representing the asynchronous operation, with the first mapped entity of type <typeparamref name="T"/>, or <c>null</c>.</returns>
        Task<T> QueryFirstOrDefaultAsync<T>(string sql, object param = null, IDbTransaction transaction = null, CancellationToken cancellationToken = default);
    }
}
using Dapper;

namespace Servy.Core.Data
{
    /// <summary>
    /// Abstraction for executing database operations using Dapper.
    /// This allows mocking database calls in unit tests without directly
    /// mocking <see cref="System.Data.DbConnection"/> extension methods.
    /// </summary>
    public interface IDapperExecutor
    {
        /// <summary>Executes a SQL command returning a scalar value.</summary>
        T? ExecuteScalar<T>(string sql, object? param = null);

        /// <summary>Executes a SQL command (INSERT, UPDATE, DELETE).</summary>
        int Execute(string sql, object? param = null);

        /// <summary>Executes a query returning a collection.</summary>
        IEnumerable<T> Query<T>(string sql, object? param = null);

        /// <summary>Executes a query returning a single entity or default.</summary>
        T? QuerySingleOrDefault<T>(string sql, object? param = null);

        /// <summary>
        /// Executes a SQL command that returns a scalar value.
        /// </summary>
        /// <typeparam name="T">The type of the scalar result.</typeparam>
        /// <param name="sql">The SQL query or command.</param>
        /// <param name="param">Optional parameters for the SQL command.</param>
        /// <returns>A task representing the asynchronous operation, with the scalar result of type <typeparamref name="T"/>.</returns>
        Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a SQL command that does not return a result set (INSERT, UPDATE, DELETE).
        /// </summary>
        /// <param name="sql">The SQL query.</param>
        /// <param name="param">Optional parameters for the SQL command.</param>
        /// <returns>A task representing the asynchronous operation, with the number of affected rows.</returns>
        Task<int> ExecuteAsync(string sql, object? param = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a SQL query that returns a collection of entities.
        /// </summary>
        /// <typeparam name="T">The type of entity returned by the query.</typeparam>
        /// <param name="command">The SQL command.</param>
        /// <returns>A task representing the asynchronous operation, with the resulting collection of <typeparamref name="T"/>.</returns>
        Task<IEnumerable<T>> QueryAsync<T>(CommandDefinition command);

        /// <summary>
        /// Executes a SQL query asynchronously that returns a collection of entities.
        /// </summary>
        Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a SQL query asynchronously that returns a single entity or default.
        /// </summary>
        Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a SQL query that returns a single entity or default if none found.
        /// </summary>
        /// <typeparam name="T">The type of entity returned by the query.</typeparam>
        /// <param name="command">The SQL command.</param>
        /// <returns>A task representing the asynchronous operation, with the resulting <typeparamref name="T"/> entity, or <c>null</c> if not found.</returns>
        Task<T?> QuerySingleOrDefaultAsync<T>(CommandDefinition command);

        /// <summary>
        /// Executes a query asynchronously and returns the first row, or a default value if the sequence contains no elements.
        /// </summary>
        Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default);
    }
}

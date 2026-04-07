using Dapper;
using Servy.Core.Data;
using System.Diagnostics.CodeAnalysis;

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

        #region Synchronous Methods

        ///<inheritdoc/>
        public T? ExecuteScalar<T>(string sql, object? param = null)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            using (var connection = _dbContext.CreateConnection())
            {
                connection.Open();
                return connection.ExecuteScalar<T>(sql, param);
            }
        }

        ///<inheritdoc/>
        public int Execute(string sql, object? param = null)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            using (var connection = _dbContext.CreateConnection())
            {
                connection.Open();
                return connection.Execute(sql, param);
            }
        }

        ///<inheritdoc/>
        public IEnumerable<T> Query<T>(string sql, object? param = null)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            using (var connection = _dbContext.CreateConnection())
            {
                connection.Open();
                return connection.Query<T>(sql, param);
            }
        }

        ///<inheritdoc/>
        public T? QuerySingleOrDefault<T>(string sql, object? param = null)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            using (var connection = _dbContext.CreateConnection())
            {
                connection.Open();
                return connection.QuerySingleOrDefault<T>(sql, param);
            }
        }

        #endregion

        #region Asynchronous Methods

        ///<inheritdoc/>
        public async Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            using (var connection = _dbContext.CreateConnection())
            {
                await connection.OpenAsync().ConfigureAwait(false);
                return await connection.ExecuteScalarAsync<T?>(sql, param).ConfigureAwait(false);
            }
        }

        ///<inheritdoc/>
        public async Task<int> ExecuteAsync(string sql, object? param = null)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            using (var connection = _dbContext.CreateConnection())
            {
                await connection.OpenAsync().ConfigureAwait(false);
                return await connection.ExecuteAsync(sql, param).ConfigureAwait(false);
            }
        }

        ///<inheritdoc/>
        public async Task<IEnumerable<T>> QueryAsync<T>(CommandDefinition command)
        {
            using (var connection = _dbContext.CreateConnection())
            {
                await connection.OpenAsync().ConfigureAwait(false);
                return await connection.QueryAsync<T>(command).ConfigureAwait(false);
            }
        }

        ///<inheritdoc/>
        public async Task<T?> QuerySingleOrDefaultAsync<T>(CommandDefinition command)
        {
            using (var connection = _dbContext.CreateConnection())
            {
                await connection.OpenAsync().ConfigureAwait(false);
                return await connection.QuerySingleOrDefaultAsync<T>(command).ConfigureAwait(false);
            }
        }

        #endregion

    }
}

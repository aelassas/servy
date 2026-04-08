using Dapper;
using Servy.Core.Data;
using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Provides a concrete implementation of <see cref="IDapperExecutor"/> using Dapper 
    /// and <see cref="System.Data.SQLite"/> for database operations.
    /// </summary>
    /// <remarks>
    /// This implementation includes automatic retry logic to handle <see cref="SQLiteErrorCode.Busy"/> 
    /// and <see cref="SQLiteErrorCode.Locked"/> errors, ensuring resilience in multi-process environments.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public class DapperExecutor : IDapperExecutor
    {
        private readonly IAppDbContext _dbContext;
        private const int MaxRetries = 3;
        private const int InitialDelayMs = 100;

        /// <summary>
        /// Initializes a new instance of the <see cref="DapperExecutor"/> class.
        /// </summary>
        /// <param name="dbContext">The database context used to manage SQLite connections.</param>
        public DapperExecutor(IAppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        #region Execution Wrappers

        /// <summary>
        /// Wraps a synchronous database action with a retry policy for SQLite busy/locked states.
        /// </summary>
        private T ExecuteWithRetry<T>(Func<T> action)
        {
            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    return action();
                }
                catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Busy || ex.ResultCode == SQLiteErrorCode.Locked)
                {
                    if (i == MaxRetries - 1) throw;
                    Logger.Warn(string.Format("Database busy (attempt {0}/{1}). Retrying...", i + 1, MaxRetries));
                    Thread.Sleep(InitialDelayMs * (i + 1));
                }
            }
            return default(T);
        }

        /// <summary>
        /// Wraps an asynchronous database action with a retry policy for SQLite busy/locked states.
        /// </summary>
        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action)
        {
            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    return await action().ConfigureAwait(false);
                }
                catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Busy || ex.ResultCode == SQLiteErrorCode.Locked)
                {
                    if (i == MaxRetries - 1) throw;
                    Logger.Warn(string.Format("Database busy (async attempt {0}/{1}). Retrying...", i + 1, MaxRetries));
                    await Task.Delay(InitialDelayMs * (i + 1)).ConfigureAwait(false);
                }
            }
            return default(T);
        }

        #endregion

        #region Synchronous Methods

        /// <inheritdoc/>
        public T ExecuteScalar<T>(string sql, object param = null)
        {
            return ExecuteWithRetry(() =>
            {
                if (sql == null) throw new ArgumentNullException(nameof(sql));
                using (var connection = _dbContext.CreateConnection())
                {
                    connection.Open();
                    return connection.ExecuteScalar<T>(sql, param);
                }
            });
        }

        /// <inheritdoc/>
        public int Execute(string sql, object param = null)
        {
            return ExecuteWithRetry(() =>
            {
                if (sql == null) throw new ArgumentNullException(nameof(sql));
                using (var connection = _dbContext.CreateConnection())
                {
                    connection.Open();
                    return connection.Execute(sql, param);
                }
            });
        }

        /// <inheritdoc/>
        public IEnumerable<T> Query<T>(string sql, object param = null)
        {
            return ExecuteWithRetry(() =>
            {
                if (sql == null) throw new ArgumentNullException(nameof(sql));
                using (var connection = _dbContext.CreateConnection())
                {
                    connection.Open();
                    return connection.Query<T>(sql, param);
                }
            }) ?? Enumerable.Empty<T>();
        }

        /// <inheritdoc/>
        public T QuerySingleOrDefault<T>(string sql, object param = null)
        {
            return ExecuteWithRetry(() =>
            {
                if (sql == null) throw new ArgumentNullException(nameof(sql));
                using (var connection = _dbContext.CreateConnection())
                {
                    connection.Open();
                    return connection.QuerySingleOrDefault<T>(sql, param);
                }
            });
        }

        #endregion

        #region Asynchronous Methods

        /// <inheritdoc/>
        public async Task<T> ExecuteScalarAsync<T>(string sql, object param = null)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                if (sql == null) throw new ArgumentNullException(nameof(sql));
                using (var connection = _dbContext.CreateConnection())
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                    return await connection.ExecuteScalarAsync<T>(sql, param).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<int> ExecuteAsync(string sql, object param = null)
        {
            var result = await ExecuteWithRetryAsync(async () =>
            {
                if (sql == null) throw new ArgumentNullException(nameof(sql));
                using (var connection = _dbContext.CreateConnection())
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                    return (int?)await connection.ExecuteAsync(sql, param).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);

            return result ?? 0;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<T>> QueryAsync<T>(CommandDefinition command)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                using (var connection = _dbContext.CreateConnection())
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                    return await connection.QueryAsync<T>(command).ConfigureAwait(false);
                }
            }).ConfigureAwait(false) ?? Enumerable.Empty<T>();
        }

        /// <inheritdoc/>
        public async Task<T> QuerySingleOrDefaultAsync<T>(CommandDefinition command)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                using (var connection = _dbContext.CreateConnection())
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                    return await connection.QuerySingleOrDefaultAsync<T>(command).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        #endregion
    }
}
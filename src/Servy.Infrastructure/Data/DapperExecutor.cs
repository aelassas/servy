using Dapper;
using Servy.Core.Config;
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

        // Thread-safe Random
        private static readonly ThreadLocal<Random> _random = new ThreadLocal<Random>(() => new Random());

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
        /// Wraps a synchronous database action with a retry policy using exponential backoff and jitter.
        /// Uses SpinWait and aggressive caps to prevent thread pool exhaustion.
        /// </summary>
        private T ExecuteWithRetry<T>(Func<T> action)
        {
            for (int i = 0; i < AppConfig.DbSyncMaxRetries; i++)
            {
                try
                {
                    return action();
                }
                catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Busy || ex.ResultCode == SQLiteErrorCode.Locked)
                {
                    if (i == AppConfig.DbSyncMaxRetries - 1)
                    {
                        Logger.Warn("Sync database operation exhausted bounded retry budget.");
                        throw;
                    }

                    // Shorter delay calculation specifically for the sync path
                    int backoff = AppConfig.DbSyncInitialDelayMs * (int)Math.Pow(2, i);
                    int jitter = _random.Value.Next(0, AppConfig.DbSyncMaxJitterMs + 1);
                    int delay = backoff + jitter;

                    Logger.Warn($"Database busy (sync attempt {i + 1}/{AppConfig.DbSyncMaxRetries}). Spinning for {delay}ms...");

                    Thread.Sleep(delay);
                }
            }

            throw new InvalidOperationException("Retry loop exited without returning or rethrowing - retry count must be > 0.");
        }

        /// <summary>
        /// Wraps an asynchronous database action with a retry policy using exponential backoff and jitter.
        /// </summary>
        private async Task<T> ExecuteWithRetryAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
        {
            for (int i = 0; i < AppConfig.DbAsyncMaxRetries; i++)
            {
                // Fail fast if the operation was cancelled before or during the retry loop
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    return await action(cancellationToken).ConfigureAwait(false);
                }
                catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Busy || ex.ResultCode == SQLiteErrorCode.Locked)
                {
                    if (i == AppConfig.DbAsyncMaxRetries - 1) throw;

                    int delay = CalculateBackoff(i);
                    Logger.Warn($"Database busy (async attempt {i + 1}/{AppConfig.DbAsyncMaxRetries}). Retrying in {delay}ms...");

                    // Critical: Pass the token to Task.Delay so we don't hang if cancelled during backoff
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException("Retry loop exited without returning or rethrowing - retry count must be > 0.");
        }

        /// <summary>
        /// Calculates the delay for the next async retry attempt using exponential backoff and jitter.
        /// </summary>
        /// <param name="attempt">The zero-based attempt index.</param>
        /// <returns>The delay in milliseconds.</returns>
        private int CalculateBackoff(int attempt)
        {
            // Exponential: 100, 200, 400...
            int backoff = AppConfig.DbAsyncInitialDelayMs * (int)Math.Pow(2, attempt);

            // Jitter: 0 to 50ms (Thread-safe and lock-free)
            int jitter = _random.Value.Next(0, AppConfig.DbAsyncMaxJitterMs + 1);

            return backoff + jitter;
        }

        #endregion

        #region Synchronous Methods

        /// <inheritdoc/>
        public T ExecuteScalar<T>(string sql, object param = null)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            return ExecuteWithRetry(() =>
            {
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
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            return ExecuteWithRetry(() =>
            {
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
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            return ExecuteWithRetry(() =>
            {
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
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            return ExecuteWithRetry(() =>
            {
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
        public async Task<T> ExecuteScalarAsync<T>(string sql, object param = null, CancellationToken cancellationToken = default)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            var command = new CommandDefinition(sql, param, cancellationToken: cancellationToken);

            return await ExecuteWithRetryAsync(async (ct) =>
            {
                using (var connection = _dbContext.CreateConnection())
                {
                    await connection.OpenAsync(ct).ConfigureAwait(false);
                    return await connection.ExecuteScalarAsync<T>(command).ConfigureAwait(false);
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<int> ExecuteAsync(string sql, object param = null, CancellationToken cancellationToken = default)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            var command = new CommandDefinition(sql, param, cancellationToken: cancellationToken);

            var result = await ExecuteWithRetryAsync(async (ct) =>
            {
                using (var connection = _dbContext.CreateConnection())
                {
                    await connection.OpenAsync(ct).ConfigureAwait(false);
                    return (int?)await connection.ExecuteAsync(command).ConfigureAwait(false);
                }
            }, cancellationToken).ConfigureAwait(false);

            return result ?? 0;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<T>> QueryAsync<T>(CommandDefinition command)
        {
            return await ExecuteWithRetryAsync(async (ct) =>
            {
                using (var connection = _dbContext.CreateConnection())
                {
                    await connection.OpenAsync(ct).ConfigureAwait(false);
                    return await connection.QueryAsync<T>(command).ConfigureAwait(false);
                }
            }, command.CancellationToken).ConfigureAwait(false) ?? Enumerable.Empty<T>();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object param = null, CancellationToken cancellationToken = default)
        {
            return await QueryAsync<T>(new CommandDefinition(sql, param, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<T> QuerySingleOrDefaultAsync<T>(string sql, object param = null, CancellationToken cancellationToken = default)
        {
            return await QuerySingleOrDefaultAsync<T>(new CommandDefinition(sql, param, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<T> QuerySingleOrDefaultAsync<T>(CommandDefinition command)
        {
            return await ExecuteWithRetryAsync(async (ct) =>
            {
                using (var connection = _dbContext.CreateConnection())
                {
                    await connection.OpenAsync(ct).ConfigureAwait(false);
                    return await connection.QuerySingleOrDefaultAsync<T>(command).ConfigureAwait(false);
                }
            }, command.CancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<T> QueryFirstOrDefaultAsync<T>(string sql, object param = null, CancellationToken cancellationToken = default)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            var command = new CommandDefinition(sql, param, cancellationToken: cancellationToken);

            return await ExecuteWithRetryAsync(async (ct) =>
            {
                using (var connection = _dbContext.CreateConnection())
                {
                    await connection.OpenAsync(ct).ConfigureAwait(false);
                    return await connection.QueryFirstOrDefaultAsync<T>(command).ConfigureAwait(false);
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        #endregion
    }
}
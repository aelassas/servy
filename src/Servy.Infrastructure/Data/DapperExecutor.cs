using Dapper;
using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.Logging;
using System.Data.SQLite;

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
    public class DapperExecutor : IDapperExecutor
    {
        private readonly IAppDbContext _dbContext;

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
        /// Uses Thread.Sleep with bounded retry budget to prevent thread pool exhaustion.
        /// </summary>
        private T? ExecuteWithRetry<T>(Func<T> action, string? sql = null)
        {
            for (int i = 0; i < AppConfig.DbSyncMaxRetries; i++)
            {
                try
                {
                    return action();
                }
                catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Busy || ex.ResultCode == SQLiteErrorCode.Locked)
                {
                    string queryContext = FormatSqlForLog(sql);

                    if (i == AppConfig.DbSyncMaxRetries - 1)
                    {
                        Logger.Warn($"Sync database operation exhausted bounded retry budget. Query: [{queryContext}]");
                        throw;
                    }

                    // Use the unified helper with Sync-specific configuration
                    int delay = CalculateBackoff(i, AppConfig.DbSyncInitialDelayMs, AppConfig.DbSyncMaxJitterMs);

                    Logger.Warn($"Database busy (sync attempt {i + 1}/{AppConfig.DbSyncMaxRetries}). Spinning for {delay}ms... Query: [{queryContext}]");

                    Thread.Sleep(delay);
                }
            }

            throw new InvalidOperationException("Retry loop exited without returning or rethrowing - retry count must be > 0.");
        }

        /// <summary>
        /// Wraps an asynchronous database action with a retry policy using exponential backoff and jitter.
        /// </summary>
        private async Task<T?> ExecuteWithRetryAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken, string? sql = null)
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
                    string queryContext = FormatSqlForLog(sql);

                    if (i == AppConfig.DbAsyncMaxRetries - 1)
                    {
                        Logger.Warn($"Async database operation exhausted bounded retry budget. Query: [{queryContext}]");
                        throw;
                    }

                    // Use the unified helper with Async-specific configuration
                    int delay = CalculateBackoff(i, AppConfig.DbAsyncInitialDelayMs, AppConfig.DbAsyncMaxJitterMs);
                    Logger.Warn($"Database busy (async attempt {i + 1}/{AppConfig.DbAsyncMaxRetries}). Retrying in {delay}ms... Query: [{queryContext}]");

                    // Critical: Pass the token to Task.Delay so we don't hang if cancelled during backoff
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException("Retry loop exited without returning or rethrowing - retry count must be > 0.");
        }

        /// <summary>
        /// Calculates the delay for a retry attempt using exponential backoff and jitter.
        /// </summary>
        /// <param name="attempt">The zero-based attempt index.</param>
        /// <param name="initialDelayMs">The base delay for the first retry.</param>
        /// <param name="maxJitterMs">The maximum random jitter to add.</param>
        /// <param name="maxBackoffMs">The absolute maximum delay allowed for the backoff, preventing infinite growth during repeated failures.</param>
        /// <returns>The calculated delay in milliseconds, representing the capped exponential backoff plus a random jitter component.</returns>
        private static int CalculateBackoff(int attempt, int initialDelayMs, int maxJitterMs, int maxBackoffMs = AppConfig.DbBackoffMaxMs)
        {
            // Use long math and cap to prevent silent overflow if MaxRetries is ever raised
            long shifted = (long)initialDelayMs << Math.Min(attempt, 30);
            int backoff = (int)Math.Min(shifted, maxBackoffMs);
            int jitter = Random.Shared.Next(0, maxJitterMs + 1);
            return backoff + jitter;
        }

        /// <summary>
        /// Flattens and truncates an SQL string for clean, single-line log output.
        /// </summary>
        private static string FormatSqlForLog(string? sql, int maxLength = 60)
        {
            if (string.IsNullOrWhiteSpace(sql)) return "Unknown Query";

            // Flatten line breaks for clean single-line logging
            string singleLine = sql.Replace("\r", " ").Replace("\n", " ").Trim();

            return singleLine.Length <= maxLength ? singleLine : singleLine.Substring(0, maxLength) + "...";
        }

        #endregion

        #region Synchronous Methods

        /// <inheritdoc/>
        public T? ExecuteScalar<T>(string sql, object? param = null)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            return ExecuteWithRetry(() =>
            {
                using (var connection = _dbContext.CreateConnection())
                {
                    connection.Open();
                    return connection.ExecuteScalar<T>(sql, param);
                }
            }, sql);
        }

        /// <inheritdoc/>
        public int Execute(string sql, object? param = null)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            return ExecuteWithRetry(() =>
            {
                using (var connection = _dbContext.CreateConnection())
                {
                    connection.Open();
                    return connection.Execute(sql, param);
                }
            }, sql);
        }

        /// <inheritdoc/>
        public IEnumerable<T> Query<T>(string sql, object? param = null)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            return ExecuteWithRetry(() =>
            {
                using (var connection = _dbContext.CreateConnection())
                {
                    connection.Open();
                    return connection.Query<T>(sql, param);
                }
            }, sql) ?? Enumerable.Empty<T>();
        }

        /// <inheritdoc/>
        public T? QuerySingleOrDefault<T>(string sql, object? param = null)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            return ExecuteWithRetry(() =>
            {
                using (var connection = _dbContext.CreateConnection())
                {
                    connection.Open();
                    return connection.QuerySingleOrDefault<T>(sql, param);
                }
            }, sql);
        }

        #endregion

        #region Asynchronous Methods

        /// <inheritdoc/>
        public async Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            var command = new CommandDefinition(sql, param, cancellationToken: cancellationToken);

            return await ExecuteWithRetryAsync(async (ct) =>
            {
                using (var connection = _dbContext.CreateConnection())
                {
                    await connection.OpenAsync(ct).ConfigureAwait(false);
                    return await connection.ExecuteScalarAsync<T?>(command).ConfigureAwait(false);
                }
            }, cancellationToken, sql).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<int> ExecuteAsync(string sql, object? param = null, CancellationToken cancellationToken = default)
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
            }, cancellationToken, sql).ConfigureAwait(false);

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
            }, command.CancellationToken, command.CommandText).ConfigureAwait(false) ?? Enumerable.Empty<T>();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));
            return await QueryAsync<T>(new CommandDefinition(sql, param, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));
            return await QuerySingleOrDefaultAsync<T>(new CommandDefinition(sql, param, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<T?> QuerySingleOrDefaultAsync<T>(CommandDefinition command)
        {
            return await ExecuteWithRetryAsync(async (ct) =>
            {
                using (var connection = _dbContext.CreateConnection())
                {
                    await connection.OpenAsync(ct).ConfigureAwait(false);
                    return await connection.QuerySingleOrDefaultAsync<T>(command).ConfigureAwait(false);
                }
            }, command.CancellationToken, command.CommandText).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, CancellationToken cancellationToken = default)
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
            }, cancellationToken, sql).ConfigureAwait(false);
        }

        #endregion
    }
}
using Dapper;
using Servy.Core.Data;
using Servy.Core.Logging;
using System.Data.SQLite;
using System.Diagnostics.CodeAnalysis;

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
        private const int MaxJitterMs = 50;

        // Thread-safe random for jitter calculation
        private static readonly Random _jitterer = new Random();

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
        /// </summary>
        private T? ExecuteWithRetry<T>(Func<T> action)
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

                    int delay = CalculateBackoff(i);
                    Logger.Warn($"Database busy (attempt {i + 1}/{MaxRetries}). Retrying in {delay}ms...");

                    // Note: Caller must ensure this is not executed on the UI thread to avoid freezes.
                    Thread.Sleep(delay);
                }
            }
            return default;
        }

        /// <summary>
        /// Wraps an asynchronous database action with a retry policy using exponential backoff and jitter.
        /// </summary>
        private async Task<T?> ExecuteWithRetryAsync<T>(Func<Task<T>> action)
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

                    int delay = CalculateBackoff(i);
                    Logger.Warn($"Database busy (async attempt {i + 1}/{MaxRetries}). Retrying in {delay}ms...");

                    await Task.Delay(delay).ConfigureAwait(false);
                }
            }
            return default;
        }

        /// <summary>
        /// Calculates the delay for the next retry attempt using exponential backoff and jitter.
        /// </summary>
        /// <param name="attempt">The zero-based attempt index.</param>
        /// <returns>The delay in milliseconds.</returns>
        private int CalculateBackoff(int attempt)
        {
            // Exponential: 100, 200, 400...
            int backoff = InitialDelayMs * (int)Math.Pow(2, attempt);

            // Jitter: 0 to 50ms
            int jitter;
            lock (_jitterer)
            {
                jitter = _jitterer.Next(0, MaxJitterMs + 1);
            }

            return backoff + jitter;
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
            });
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
            });
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
            }) ?? Enumerable.Empty<T>();
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
            });
        }

        #endregion

        #region Asynchronous Methods

        /// <inheritdoc/>
        public async Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            return await ExecuteWithRetryAsync(async () =>
            {
                using (var connection = _dbContext.CreateConnection())
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                    return await connection.ExecuteScalarAsync<T?>(sql, param).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<int> ExecuteAsync(string sql, object? param = null)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            var result = await ExecuteWithRetryAsync(async () =>
            {
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
        public async Task<T?> QuerySingleOrDefaultAsync<T>(CommandDefinition command)
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

        /// <inheritdoc/>
        public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
        {
            if (sql == null) throw new ArgumentNullException(nameof(sql));

            return await ExecuteWithRetryAsync(async () =>
            {
                using (var connection = _dbContext.CreateConnection())
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                    return await connection.QueryFirstOrDefaultAsync<T>(sql, param).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        #endregion
    }
}
using Servy.Core.Data;
using System;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Provides a database context for creating SQLite connections and Dapper executors.
    /// Implements the standard .NET disposal pattern.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class AppDbContext : IAppDbContext
    {
        private readonly string _connectionString;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of <see cref="AppDbContext"/> with the specified connection string.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string used to connect to the database.</param>
        public AppDbContext(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Creates a new <see cref="DbConnection"/> for the SQLite database.
        /// </summary>
        /// <returns>A new <see cref="SQLiteConnection"/> instance.</returns>
        public DbConnection CreateConnection()
        {
            ThrowIfDisposed();
            return new SQLiteConnection(_connectionString);
        }

        /// <summary>
        /// Creates a new <see cref="IDapperExecutor"/> instance using a fresh SQLite connection.
        /// </summary>
        /// <returns>An <see cref="IDapperExecutor"/> for executing SQL commands.</returns>
        public IDapperExecutor CreateDapperExecutor()
        {
            ThrowIfDisposed();
            return new DapperExecutor(this);
        }

        #region IDisposable Implementation

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="AppDbContext"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Release managed resources if any are added in the future 
                // (e.g., a shared DbConnection or a CancellationSource)
            }

            _disposed = true;
        }

        /// <summary>
        /// Ensures the context is still valid before performing operations.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AppDbContext));
            }
        }

        #endregion
    }
}
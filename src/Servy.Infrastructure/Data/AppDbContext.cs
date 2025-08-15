using Servy.Core.Data;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics.CodeAnalysis;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Provides a database context for creating SQLite connections and Dapper executors.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class AppDbContext : IAppDbContext
    {
        private readonly string _connectionString;

        /// <summary>
        /// Initializes a new instance of <see cref="AppDbContext"/> with the specified connection string.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string used to connect to the database.</param>
        public AppDbContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Creates a new <see cref="IDbConnection"/> for the SQLite database.
        /// </summary>
        /// <returns>A new <see cref="SqliteConnection"/> instance.</returns>
        public IDbConnection CreateConnection()
        {
            return new SQLiteConnection(_connectionString);
        }

        /// <summary>
        /// Creates a new <see cref="IDapperExecutor"/> instance using a fresh SQLite connection.
        /// </summary>
        /// <returns>An <see cref="IDapperExecutor"/> for executing SQL commands.</returns>
        public IDapperExecutor CreateDapperExecutor()
        {
            return new DapperExecutor(this);
        }
    }
}

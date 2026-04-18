using System.Data.Common;

namespace Servy.Core.Data
{
    /// <summary>
    /// Represents the application's database context abstraction.
    /// Supports deterministic cleanup of database resources.
    /// </summary>
    public interface IAppDbContext : IDisposable
    {
        /// <summary>
        /// Creates a new <see cref="DbConnection"/> for executing SQL commands.
        /// </summary>
        /// <returns>A new database connection instance.</returns>
        DbConnection CreateConnection();

        /// <summary>
        /// Creates a new <see cref="IDapperExecutor"/> for executing SQL commands via Dapper.
        /// </summary>
        /// <returns>An <see cref="IDapperExecutor"/> instance.</returns>
        IDapperExecutor CreateDapperExecutor();
    }
}
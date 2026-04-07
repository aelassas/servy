using System.Data.Common;

namespace Servy.Core.Data
{
    /// <summary>
    /// Represents the application's database context abstraction.
    /// </summary>
    public interface IAppDbContext
    {
        /// <summary>
        /// Creates a new <see cref="DbConnection"/> for executing SQL commands.
        /// </summary>
        DbConnection CreateConnection();

        /// <summary>
        /// Creates a new <see cref="IDapperExecutor"/> for executing SQL commands via Dapper.
        /// </summary>
        IDapperExecutor CreateDapperExecutor();
    }
}

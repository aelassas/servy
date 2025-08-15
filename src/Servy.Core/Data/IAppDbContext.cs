using System.Data;

namespace Servy.Core.Data
{
    /// <summary>
    /// Represents the application's database context abstraction.
    /// </summary>
    public interface IAppDbContext
    {
        /// <summary>
        /// Creates a new <see cref="IDbConnection"/> for executing SQL commands.
        /// </summary>
        IDbConnection CreateConnection();

        /// <summary>
        /// Creates a new <see cref="IDapperExecutor"/> for executing SQL commands via Dapper.
        /// </summary>
        IDapperExecutor CreateDapperExecutor();
    }
}

using System;
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
    }
}
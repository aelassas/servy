using Servy.Core.Data;
using System;
using System.Data;

namespace Servy.Infrastructure.Helpers
{
    /// <summary>
    /// Provides methods to initialize the Servy SQLite database.
    /// </summary>
    public static class DatabaseInitializer
    {
        /// <summary>
        /// Ensures that the database exists and is initialized.
        /// </summary>
        /// <param name="dbContext">The database context. Cannot be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> is null.</exception>
        public static void InitializeDatabase(IAppDbContext dbContext, Action<IDbConnection> initializer)
        {
            if (dbContext == null) throw new ArgumentNullException(nameof(dbContext));
            if (initializer == null) throw new ArgumentNullException(nameof(initializer));

            using (var connection = dbContext.CreateConnection())
            {
                connection.Open();
                initializer(connection);
            }
        }
    }
}

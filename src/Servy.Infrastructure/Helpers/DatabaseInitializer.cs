using Servy.Core.Data;
using System.Data.Common;
using System.Data.SQLite;

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
        /// <param name="initializer">A callback that receives the open <see cref="DbConnection"/> and applies schema/migration logic. Cannot be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="dbContext"/> or <paramref name="initializer"/> is null.</exception>
        public static void InitializeDatabase(IAppDbContext dbContext, Action<DbConnection> initializer)
        {
            if (dbContext == null) throw new ArgumentNullException(nameof(dbContext));
            if (initializer == null) throw new ArgumentNullException(nameof(initializer));

            // CRITICAL: Register custom collations process-wide BEFORE opening any connection.
            // System.Data.SQLite inspects _registeredFunctions during connection.Open() to bind C-handles.
            SQLiteFunction.RegisterFunction(typeof(Data.UnicodeNoCaseCollation));

            using (var connection = dbContext.CreateConnection())
            {
                connection.Open();
                initializer(connection);
            }
        }
    }
}

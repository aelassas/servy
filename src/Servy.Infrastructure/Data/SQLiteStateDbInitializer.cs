using Dapper;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Linq;
using System.Reflection;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Initializes the schema of the runtime-state database (<c>Servy.state.db</c>) and applies its
    /// sequential migrations.
    /// </summary>
    /// <remarks>
    /// This is the counterpart of <see cref="SQLiteDbInitializer"/> for the second database. It keeps
    /// its own <c>SchemaInfo</c> row, so the two files version independently: a migration added to one
    /// never touches the other. Column types come from the <c>[SqlColumn]</c> attributes on
    /// <see cref="ServiceStateDto"/>, and the column set from <see cref="StateSqlConstants"/>, so
    /// neither can drift from the DTO.
    /// </remarks>
    public static class SQLiteStateDbInitializer
    {
        /// <summary>
        /// Single source of truth for the latest schema version of the runtime-state database.
        /// </summary>
        public const int LatestSchemaVersion = 1;

        /// <summary>
        /// Cached <c>[SqlColumn]</c> mappings of <see cref="ServiceStateDto"/>, so the DDL below needs
        /// no reflection per call and cannot disagree with the DTO.
        /// </summary>
        private static readonly Dictionary<string, string> SqlTypeMap = BuildSqlTypeMap();

        /// <summary>
        /// Reflects over <see cref="ServiceStateDto"/> once to cache its <c>[SqlColumn]</c> mappings.
        /// </summary>
        /// <returns>A case-insensitive map of property name to upper-cased SQLite type.</returns>
        private static Dictionary<string, string> BuildSqlTypeMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in typeof(ServiceStateDto).GetProperties())
            {
                var attr = prop.GetCustomAttribute<SqlColumnAttribute>();
                if (attr != null)
                {
                    // Canonicalize to upper case so the generated DDL is stable.
                    map[prop.Name] = attr.SqlType.ToUpperInvariant();
                }
            }

            return map;
        }

        /// <summary>
        /// Creates or migrates the runtime-state schema on the given connection.
        /// </summary>
        /// <param name="connection">An open connection to the runtime-state database.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> is null.</exception>
        /// <exception cref="Exception">
        /// Rethrown from the migration transaction after it has been rolled back, so a partially
        /// migrated schema is never left behind.
        /// </exception>
        public static void Initialize(DbConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            // Register the collation sequence BEFORE any DDL or PRAGMA is sent to the SQLite engine,
            // exactly as SQLiteDbInitializer does for the configuration database. The Name key of the
            // runtime-state table is declared COLLATE UNICODE_NOCASE, so the sequence has to resolve
            // both when the table is created and when an existing file's index is parsed.
            SQLiteFunction.RegisterFunction(typeof(UnicodeNoCaseCollation));

            // The version tracking table is created outside the transaction, exactly as the
            // configuration database does it: the CHECK constraint guarantees a single row.
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS SchemaInfo (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1),
                    Version INTEGER NOT NULL
                );");

            using (var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    // Force an immediate write lock so two wrappers starting at once cannot both
                    // migrate. This is what BEGIN IMMEDIATE does, within the ADO.NET scope.
                    connection.Execute("INSERT OR IGNORE INTO SchemaInfo (Id, Version) VALUES (1, 0);", transaction: transaction);
                    connection.Execute("UPDATE SchemaInfo SET Version = Version WHERE Id = 1;", transaction: transaction);

                    int currentVersion = GetSchemaVersion(connection, transaction);

                    if (currentVersion < 1)
                    {
                        ApplyVersion1(connection, transaction);
                        UpdateSchemaVersion(connection, 1, transaction);
                        currentVersion = 1;
                    }

                    // A file written by a newer Servy is left alone rather than downgraded, and the
                    // reconciliation pass is skipped with it: it would ADD COLUMN for every name in
                    // StateSqlConstants.Columns that the newer file does not carry, so an older build
                    // would put back a column a later version had renamed or dropped. The runtime
                    // state is rebuilt by the wrapper, so continuing read-only is enough here, where
                    // SQLiteDbInitializer refuses outright for the configuration database.
                    if (currentVersion > LatestSchemaVersion)
                    {
                        Logger.Warn($"Runtime-state database reports schema version {currentVersion}, which is newer than the {LatestSchemaVersion} this build knows. No migration was applied.");
                    }
                    else
                    {
                        ReconcileSchema(connection, transaction);
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    Logger.Error("Runtime-state database initialization failed; the migration transaction was rolled back.", ex);
                    transaction.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// Creates the runtime-state table as version 1 of the schema.
        /// </summary>
        /// <param name="connection">The active connection.</param>
        /// <param name="transaction">The active migration transaction.</param>
        private static void ApplyVersion1(DbConnection connection, DbTransaction transaction)
        {
            connection.Execute(BuildCreateTableSql(), transaction: transaction);
            Logger.Info($"Runtime-state database: created table {StateSqlConstants.ServiceStateTableName} (schema version 1).");
        }

        /// <summary>
        /// Adds any column named by <see cref="StateSqlConstants"/> that the table on disk does not
        /// carry yet.
        /// </summary>
        /// <remarks>
        /// This is the same safety net the configuration database has: a column added to the DTO and to
        /// <see cref="StateSqlConstants"/> without its own migration step still reaches an existing
        /// file, instead of failing every read with "no such column".
        /// </remarks>
        /// <param name="connection">The active connection.</param>
        /// <param name="transaction">The active migration transaction.</param>
        private static void ReconcileSchema(DbConnection connection, DbTransaction transaction)
        {
            var existing = GetExistingColumnNames(connection, transaction);

            foreach (var column in StateSqlConstants.Columns)
            {
                if (existing.Contains(column))
                {
                    continue;
                }

                connection.Execute(
                    $"ALTER TABLE {StateSqlConstants.ServiceStateTableName} ADD COLUMN {column} {GetSqlType(column)};",
                    transaction: transaction);

                Logger.Info($"Runtime-state database: added missing column {column}.");
            }
        }

        /// <summary>
        /// Builds the CREATE TABLE statement for the runtime-state table from the DTO and the column
        /// list, so the DDL has no hand-maintained copy of either.
        /// </summary>
        /// <remarks>
        /// The <c>Name</c> key carries <c>COLLATE UNICODE_NOCASE</c>, the same collation the
        /// configuration database keys service names with (<see cref="SQLiteDbInitializer"/> version 6
        /// and every lookup in <c>ServiceRepository</c>). The SCM treats <c>MyService</c> and
        /// <c>myservice</c> as one service, so the two files have to agree on which names are equal:
        /// with a binary key, one service could own two runtime-state rows and a lookup in another
        /// case would find none. Declaring it on the column makes <c>WHERE Name = @Name</c>,
        /// <c>ON CONFLICT(Name)</c> and <c>ORDER BY Name</c> use it without repeating it per statement.
        /// </remarks>
        /// <returns>The CREATE TABLE statement.</returns>
        private static string BuildCreateTableSql()
        {
            var columns = StateSqlConstants.Columns.Select(c => $"    {c} {GetSqlType(c)}");

            return $@"
                CREATE TABLE IF NOT EXISTS {StateSqlConstants.ServiceStateTableName} (
                    Name TEXT NOT NULL PRIMARY KEY COLLATE UNICODE_NOCASE,
                {string.Join(",\r\n                ", columns)}
                );";
        }

        /// <summary>
        /// Gets the SQLite type declared for a column by <see cref="ServiceStateDto"/>.
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <returns>The declared type, or <c>TEXT</c> when the DTO declares none.</returns>
        private static string GetSqlType(string columnName)
        {
            return SqlTypeMap.TryGetValue(columnName, out var type) ? type : "TEXT";
        }

        /// <summary>
        /// Reads the column names the runtime-state table carries on disk.
        /// </summary>
        /// <param name="connection">The active connection.</param>
        /// <param name="transaction">The active migration transaction.</param>
        /// <returns>A case-insensitive set of column names, empty when the table does not exist.</returns>
        private static HashSet<string> GetExistingColumnNames(DbConnection connection, DbTransaction transaction)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rows = connection.Query(
                $"PRAGMA table_info({StateSqlConstants.ServiceStateTableName});",
                transaction: transaction);

            foreach (var row in rows)
            {
                names.Add((string)row.name);
            }

            return names;
        }

        /// <summary>
        /// Reads the recorded schema version of the runtime-state database.
        /// </summary>
        /// <param name="connection">The active connection.</param>
        /// <param name="transaction">The active migration transaction.</param>
        /// <returns>The recorded version, or 0 when none is recorded.</returns>
        private static int GetSchemaVersion(DbConnection connection, DbTransaction transaction)
        {
            return connection.QueryFirstOrDefault<int>("SELECT Version FROM SchemaInfo WHERE Id = 1;", transaction: transaction);
        }

        /// <summary>
        /// Records the schema version of the runtime-state database, keeping the single-row invariant.
        /// </summary>
        /// <param name="connection">The active connection.</param>
        /// <param name="version">The version to record.</param>
        /// <param name="transaction">The active migration transaction.</param>
        private static void UpdateSchemaVersion(DbConnection connection, int version, DbTransaction transaction)
        {
            connection.Execute(@"
                INSERT INTO SchemaInfo (Id, Version) VALUES (1, @Version)
                ON CONFLICT(Id) DO UPDATE SET Version = excluded.Version;",
                new { Version = version },
                transaction: transaction);
        }
    }
}

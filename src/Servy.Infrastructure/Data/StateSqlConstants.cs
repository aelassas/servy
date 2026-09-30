namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Centralized source of truth for the table and column set of the runtime-state database
    /// (<c>Servy.state.db</c>): every SQL clause below is built from the one list, so the clauses
    /// cannot diverge from each other.
    /// </summary>
    /// <remarks>
    /// It owns which columns exist, not what type they get - the column types come from the
    /// <c>[SqlColumn]</c> attributes on <see cref="Core.DTOs.ServiceStateDto"/>, exactly as
    /// <see cref="SqlConstants"/> takes them from <c>ServiceDto</c>.
    /// </remarks>
    public static class StateSqlConstants
    {
        /// <summary>
        /// Name of the table every statement in this class targets.
        /// </summary>
        public const string ServiceStateTableName = "ServiceState";

        // SINGLE SOURCE OF TRUTH: add runtime-state columns here.
        // Do NOT add 'Name' here: it is the primary key and is handled separately below.
        private static readonly string[] StateColumns =
        {
            "Pid",
            "ActiveStdoutPath",
            "ActiveStderrPath",
            "PreviousStopTimeout"
        };

        /// <summary>
        /// The runtime-state column names, without the <c>Name</c> key column.
        /// </summary>
        public static IReadOnlyList<string> Columns => StateColumns;

        /// <summary>
        /// Column list for an INSERT: Name followed by every runtime-state column.
        /// </summary>
        public static readonly string InsertColumns =
            "Name, " + string.Join(", ", StateColumns);

        /// <summary>
        /// Parameter list matching <see cref="InsertColumns"/>, in the same order.
        /// </summary>
        public static readonly string InsertValues =
            "@Name, " + string.Join(", ", StateColumns.Select(c => $"@{c}"));

        /// <summary>
        /// SET clause for the ON CONFLICT upsert. Excludes Name, which is the conflict target.
        /// </summary>
        public static readonly string UpsertSet =
            string.Join(", ", StateColumns.Select(c => $"{c} = excluded.{c}"));

        /// <summary>
        /// Column list for a SELECT: Name followed by every runtime-state column.
        /// </summary>
        public static readonly string SelectColumns = InsertColumns;
    }
}

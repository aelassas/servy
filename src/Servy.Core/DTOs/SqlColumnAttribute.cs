namespace Servy.Core.DTOs
{
    /// <summary>
    /// Defines the SQLite column affinity and constraints for a mapped property.
    /// Used by the database initializer to dynamically build and migrate the schema.
    /// </summary>
    /// <remarks>
    /// This attribute supplies the column's TYPE only; it does not add the column to the table.
    /// Which columns exist is decided by the list in <c>Servy.Infrastructure.Data.SqlConstants</c>,
    /// so a property decorated here but absent from that list is created by no migration and written
    /// by no statement. The two lists are hand-maintained and kept in step by the anti-drift tests in
    /// <c>SqlColumnContractAntiDriftTests</c>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class SqlColumnAttribute : Attribute
    {
        /// <summary>
        /// Gets the raw SQLite column type, affinity, and constraints (e.g., <c>"INTEGER PRIMARY KEY AUTOINCREMENT"</c> or <c>"TEXT NOT NULL"</c>).
        /// </summary>
        public string SqlType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlColumnAttribute"/> class.
        /// </summary>
        /// <param name="sqlType">The raw SQL type string to be applied to the column.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="sqlType"/> is null, empty, or consists only of whitespace.</exception>
        public SqlColumnAttribute(string sqlType)
        {
            if (string.IsNullOrWhiteSpace(sqlType))
                throw new ArgumentException("SQL type cannot be null, empty or whitespace.", nameof(sqlType));

            SqlType = sqlType;
        }
    }
}

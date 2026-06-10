namespace Servy.Core.DTOs
{
    /// <summary>
    /// Defines the SQLite column affinity and constraints for a mapped property.
    /// Used by the database initializer to dynamically build and migrate the schema.
    /// </summary>
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
                throw new ArgumentException("SQL type cannot be null or empty.", nameof(sqlType));

            SqlType = sqlType;
        }
    }
}
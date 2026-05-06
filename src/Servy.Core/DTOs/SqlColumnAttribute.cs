using System;

namespace Servy.Core.DTOs
{
    /// <summary>
    /// Defines the SQLite column affinity and constraints for a mapped property.
    /// Used by the database initializer to dynamically build and migrate the schema.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class SqlColumnAttribute : Attribute
    {
        public string SqlType { get; }

        public SqlColumnAttribute(string sqlType)
        {
            if (string.IsNullOrWhiteSpace(sqlType))
                throw new ArgumentException("SQL type cannot be null or empty.", nameof(sqlType));

            SqlType = sqlType;
        }
    }
}
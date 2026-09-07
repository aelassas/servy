using Servy.Core.DTOs;
using Servy.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Servy.Infrastructure.UnitTests.Data
{
    /// <summary>
    /// Anti-drift tests for the two hand-maintained lists that together describe the Services table:
    /// the <c>[SqlColumn]</c> attributes on <see cref="ServiceDto"/>, which decide what type each
    /// column gets, and <see cref="SqlConstants.InsertColumns"/>, which decides which columns exist
    /// at all. Neither list is derived from the other.
    /// </summary>
    /// <remarks>
    /// The initializer already fails at first database open when a name in <see cref="SqlConstants"/>
    /// has no <c>[SqlColumn]</c> attribute. The opposite direction has no runtime guard: a decorated
    /// property that nobody added to the column list is never created, never written and read back as
    /// its CLR default, silently. Both directions are pinned here so either kind of drift fails the
    /// build instead.
    /// </remarks>
    public class SqlColumnContractAntiDriftTests
    {
        /// <summary>
        /// Autoincrement primary key: the initializer creates it explicitly and no DML statement lists it.
        /// </summary>
        private const string PrimaryKeyColumn = "Id";

        /// <summary>
        /// Every <see cref="ServiceDto"/> property carrying <c>[SqlColumn]</c>, minus the primary key.
        /// </summary>
        private static List<string> DtoColumns() =>
            typeof(ServiceDto)
                .GetProperties()
                .Where(p => p.GetCustomAttribute<SqlColumnAttribute>() != null)
                .Select(p => p.Name)
                .Where(n => !string.Equals(n, PrimaryKeyColumn, StringComparison.OrdinalIgnoreCase))
                .ToList();

        /// <summary>
        /// Every column name the Services DML statements are built from.
        /// </summary>
        private static List<string> DmlColumns() =>
            SqlConstants.InsertColumns
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .ToList();

        [Fact]
        public void EverySqlColumnProperty_IsListedInSqlConstants()
        {
            // Arrange
            var dtoColumns = DtoColumns();
            var dmlColumns = DmlColumns();

            // Guard against a silent zero: a reflection change that stops matching would otherwise
            // leave this test asserting over an empty set and passing for the wrong reason.
            Assert.NotEmpty(dtoColumns);
            Assert.NotEmpty(dmlColumns);

            // Act
            var missing = dtoColumns.Except(dmlColumns, StringComparer.OrdinalIgnoreCase).ToList();

            // Assert
            Assert.True(
                missing.Count == 0,
                "These ServiceDto properties carry [SqlColumn] but are absent from SqlConstants, so the column is " +
                "never created, never written by any statement and always read back as its CLR default: " +
                string.Join(", ", missing));
        }

        [Fact]
        public void EverySqlConstantsColumn_HasASqlColumnProperty()
        {
            // Arrange
            var dtoColumns = DtoColumns();
            var dmlColumns = DmlColumns();

            Assert.NotEmpty(dtoColumns);
            Assert.NotEmpty(dmlColumns);

            // Act
            var undeclared = dmlColumns.Except(dtoColumns, StringComparer.OrdinalIgnoreCase).ToList();

            // Assert
            Assert.True(
                undeclared.Count == 0,
                "These SqlConstants column names have no [SqlColumn] property on ServiceDto, which the schema " +
                "initializer only reports when a database is first opened: " +
                string.Join(", ", undeclared));
        }
    }
}

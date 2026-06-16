using System.Data.SQLite;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Provides a truly Unicode-aware, case-insensitive string collation sequence for SQLite.
    /// Overrides the default ASCII-only constraints of the built-in SQLite NOCASE collation
    /// by leveraging .NET's culture-invariant, case-insensitive linguistic rules
    /// (<see cref="StringComparison.InvariantCultureIgnoreCase"/>).
    /// </summary>
    [SQLiteFunction(Name = "UNICODE_NOCASE", FuncType = FunctionType.Collation)]
    public class UnicodeNoCaseCollation : SQLiteFunction
    {
        /// <summary>
        /// Performs a linguistically accurate, case-insensitive, and culture-invariant comparison of two string segments.
        /// Leveraging <see cref="StringComparison.InvariantCultureIgnoreCase"/> handles full Unicode character expansions 
        /// (e.g., German 'ß' to 'SS') and consistent casing normalization uniformly across all operating system environments.
        /// </summary>
        /// <param name="param1">The first string component to compare.</param>
        /// <param name="param2">The second string component to compare.</param>
        /// <returns>
        /// A signed integer indicating the relative values of <paramref name="param1"/> and <paramref name="param2"/>:
        /// <list type="table">
        ///   <listheader>
        ///     <term>Value</term>
        ///     <description>Condition</description>
        ///   </listheader>
        ///   <item>
        ///     <term>Less than zero</term>
        ///     <description><paramref name="param1"/> precedes <paramref name="param2"/> in linguistic sort order.</description>
        ///   </item>
        ///   <item>
        ///     <term>Zero</term>
        ///     <description><paramref name="param1"/> matches <paramref name="param2"/> under invariant case-insensitive rules.</description>
        ///   </item>
        ///   <item>
        ///     <term>Greater than zero</term>
        ///     <description><paramref name="param1"/> follows <paramref name="param2"/> in linguistic sort order.</description>
        ///   </item>
        /// </list>
        /// </returns>
        public override int Compare(string? param1, string? param2)
        {
            // Handle null propagation safely to prevent runtime comparison exceptions
            if (param1 == null && param2 == null) return 0;
            if (param1 == null) return -1;
            if (param2 == null) return 1;

            // InvariantCultureIgnoreCase applies full Unicode linguistic casing rules
            // and structural normalization maps uniformly across all operating system locales.
            return string.Compare(param1, param2, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
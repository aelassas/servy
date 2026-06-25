using System;
using System.Data.SQLite;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Provides a version-stable, Unicode-aware, case-insensitive string collation sequence for SQLite.
    /// Overrides the default ASCII-only constraints of the built-in SQLite NOCASE collation
    /// by leveraging .NET's deterministic ordinal case-folding rules (<see cref="StringComparison.OrdinalIgnoreCase"/>).
    /// </summary>
    /// <remarks>
    /// <b>CRITICAL DATABASE SAFETY NOTE:</b> This collation backs persistent disk-bound unique indices 
    /// (e.g., <c>idx_services_name_unique</c>). It must prioritize strict binary case-folding over locale-specific or 
    /// linguistic sorting logic. Using linguistic matches (like InvariantCultureIgnoreCase) breaks physical B-Tree ordering 
    /// invariants across runtime transitions (e.g., .NET 5+'s ICU vs .NET 4.8's NLS layouts), leading to index corruption.
    /// </remarks>
    [SQLiteFunction(Name = "UNICODE_NOCASE", FuncType = FunctionType.Collation)]
    public class UnicodeNoCaseCollation : SQLiteFunction
    {
        /// <summary>
        /// Performs a deterministic, version-stable, case-insensitive ordinal comparison of two string segments.
        /// Handles accented Unicode casing structures (e.g., 'ä' matches 'Ä') identically across all platforms and OS architectures.
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
        ///     <description><paramref name="param1"/> precedes <paramref name="param2"/> in strict binary case-insensitive order.</description>
        ///   </item>
        ///   <item>
        ///     <term>Zero</term>
        ///     <description><paramref name="param1"/> matches <paramref name="param2"/> under version-stable case-insensitive ordinal rules.</description>
        ///   </item>
        ///   <item>
        ///     <term>Greater than zero</term>
        ///     <description><paramref name="param1"/> follows <paramref name="param2"/> in strict binary case-insensitive order.</description>
        ///   </item>
        /// </list>
        /// </returns>
        public override int Compare(string param1, string param2)
        {
            // Handle null propagation safely to prevent runtime comparison exceptions
            if (param1 == null && param2 == null) return 0;
            if (param1 == null) return -1;
            if (param2 == null) return 1;

            // OrdinalIgnoreCase performs simple binary Unicode case folding.
            // This guarantees an unchanging, total order regardless of whether the process executes under 
            // a .NET Framework NLS platform or a modern .NET 5+ ICU system deployment.
            return string.Compare(param1, param2, StringComparison.OrdinalIgnoreCase);
        }
    }
}
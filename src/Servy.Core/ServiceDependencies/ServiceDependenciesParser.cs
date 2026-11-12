using System;
using System.Collections.Generic;
using System.Linq;

namespace Servy.Core.ServiceDependencies
{
    /// <summary>
    /// Provides methods to parse and format Windows service dependency strings
    /// for use with Windows Service APIs that require double-null-terminated dependency lists.
    /// </summary>
    public static class ServiceDependenciesParser
    {
        /// <summary>
        /// Represents the MULTI_SZ value that indicates a service has no dependencies.
        /// This is a double-null terminator ("\0\0") required by the Windows Service Control Manager (SCM).
        /// </summary>
        public const string NoDependencies = "\0\0";

        /// <summary>
        /// Splits a raw textual dependency listing into trimmed, non-empty service name tokens based on canonical formatting separators.
        /// </summary>
        /// <param name="input">The raw configuration text string containing service dependencies.</param>
        /// <returns>An enumerable sequence of normalized, clean service name tokens.</returns>
        /// <remarks>Drops case-insensitive duplicate dependency names.</remarks>
        public static IEnumerable<string> Tokenize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return Enumerable.Empty<string>();

            return input
                .Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses a textual dependency list into the Windows MULTI_SZ format required by the Service Control Manager,
        /// and makes sure <paramref name="requiredDependency"/> is part of it.
        /// </summary>
        /// <param name="input">The configured dependencies, separated by semicolons or newlines.</param>
        /// <param name="requiredDependency">A dependency every service must have, appended when the list does not
        /// already name it (compared case-insensitively).</param>
        /// <returns>A MULTI_SZ string that always names <paramref name="requiredDependency"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="requiredDependency"/> is null or blank.</exception>
        public static string ParseWithRequired(string input, string requiredDependency)
        {
            if (string.IsNullOrWhiteSpace(requiredDependency))
                throw new ArgumentException("The required dependency cannot be null or blank.", nameof(requiredDependency));

            var parts = Tokenize(input).ToList();
            var required = requiredDependency.Trim();
            if (!parts.Contains(required, StringComparer.OrdinalIgnoreCase))
                parts.Add(required);

            return string.Join("\0", parts) + NoDependencies;
        }
    }
}

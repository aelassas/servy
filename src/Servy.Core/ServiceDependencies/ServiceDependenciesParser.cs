using System;
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
        /// Parses a textual dependency list into the Windows MULTI_SZ format required by the Service Control Manager.
        /// </summary>
        /// <param name="input">
        /// A string containing service names separated by semicolons (;) or newlines.
        /// If the input is <c>null</c>, empty, or contains no valid entries, the service is configured with no dependencies.
        /// </param>
        /// <returns>
        /// A MULTI_SZ string suitable for the <c>lpDependencies</c> parameter of 
        /// <c>ChangeServiceConfig</c>. The format is:
        /// <list type="bullet">
        /// <item><description>Each dependency is separated by a single null character ('\0').</description></item>
        /// <item><description>The list is terminated by an additional null character, resulting in a double-null termination ("\0\0").</description></item>
        /// <item><description>If no dependencies are specified, returns <c>"\0\0"</c> to explicitly clear dependencies.</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// - Duplicate dependency names are removed (case-insensitive).  
        /// - Passing <c>null</c> or an empty string will clear all dependencies.  
        /// </remarks>
        public static string Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return NoDependencies;

            var parts = input
                .Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (parts.Length == 0)
                return NoDependencies;

            // Windows API compatibility: When working with Windows service dependencies,
            // the Service Control Manager expects dependency lists as a multi-string (MULTI_SZ),
            // which is a sequence of null-terminated strings ending with an additional null 
            // terminator (i.e., strings separated by \0 and double \0 at the end).
            return string.Join("\0", parts) + "\0\0";
        }

    }
}

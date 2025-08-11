using System;
using System.Linq;

namespace Servy.Core.ServiceDependencies
{
    /// <summary>
    /// Provides methods to parse and format Windows service dependency strings
    /// for use with Windows Service APIs that require double-null-terminated dependency lists.
    /// </summary>
    public class ServiceDependenciesParser
    {
        /// <summary>
        /// Parses a string of service dependencies separated by semicolons or new lines
        /// into a double-null-terminated string suitable for Windows service API calls.
        /// </summary>
        /// <param name="input">
        /// The raw input string containing one or more service names separated by semicolons (';') or new lines.
        /// Each service name should be the internal service name (no spaces or special characters).
        /// </param>
        /// <returns>
        /// A double-null-terminated string with service names separated by single null characters,
        /// or <c>null</c> if the input is null or empty.
        /// </returns>
        public static string Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var parts = input
                .Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();

            if (parts.Length == 0)
                return null;

            // Windows API compatibility: When working with Windows service dependencies,
            // the Service Control Manager expects dependency lists as a multi-string (MULTI_SZ),
            // which is a sequence of null-terminated strings ending with an additional null 
            // terminator (i.e., strings separated by \0 and double \0 at the end).
            return string.Join("\0", parts) + "\0\0";
        }

    }
}

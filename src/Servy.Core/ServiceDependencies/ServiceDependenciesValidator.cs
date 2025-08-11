using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Servy.Core.ServiceDependencies
{
    public static class ServiceDependenciesValidator
    {
        // Allowed characters: letters, digits, hyphen, underscore
        private static readonly Regex ValidServiceNameRegex = new Regex(@"^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

        /// <summary>
        /// Validates the input string containing service dependencies.
        /// Service names must be separated by semicolons or new lines.
        /// Each service name must contain only letters, digits, hyphens, or underscores.
        /// </summary>
        /// <param name="input">Raw input string with service dependencies.</param>
        /// <param name="errors">List of validation error messages.</param>
        /// <returns>True if all service names are valid; otherwise false.</returns>
        public static bool Validate(string input, out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrWhiteSpace(input))
            {
                // No dependencies is valid (empty)
                return true;
            }

            // Split by semicolons or new lines (handle both \r\n and \n)
            var separators = new[] { ';', '\r', '\n' };
            var parts = input.Split(separators, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                string serviceName = parts[i].Trim();

                if (string.IsNullOrEmpty(serviceName))
                {
                    continue; // skip empty entries
                }

                if (!ValidServiceNameRegex.IsMatch(serviceName))
                {
                    errors.Add($"Invalid service name '{serviceName}'. Only letters, digits, hyphens, and underscores are allowed.");
                }
            }

            return errors.Count == 0;
        }
    }
}

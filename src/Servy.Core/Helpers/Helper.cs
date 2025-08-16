using System.Globalization;
using System.IO;

namespace Servy.Core.Helpers
{
    public class Helper
    {
        /// <summary>
        /// Checks if the provided path is valid.
        /// </summary>
        /// <param name="path">The path to validate.</param>
        /// <returns>True if the path is valid, otherwise false.</returns>
        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if (path.Contains("..")) // no directory traversal
            {
                return false;
            }

            try
            {
                // Check for invalid characters
                if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                {
                    return false;
                }

                // Check if the path is absolute
                if (!Path.IsPathRooted(path))
                {
                    return false;
                }

                // Try to normalize the path (throws if invalid)
                string fullPath = Path.GetFullPath(path);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Ensures the parent directory of the given file path exists, creating it if necessary.
        /// </summary>
        /// <param name="path">The full file path.</param>
        /// <returns>True if the directory exists or was created successfully; false otherwise.</returns>
        public static bool CreateParentDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                var directory = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return false;
                }

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Quotes a string.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string Quote(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "\"\"";

            input = input.Replace("\"", "\"\"").TrimStart('"').TrimEnd('"').TrimEnd('\\');

            return $"\"{input}\"";
        }

        /// <summary>
        /// Helper method to convert "v1.2.3" or "1.2.3" into a comparable double, e.g., 1.23
        /// </summary>
        /// <param name="version">Version in the following format  "v1.2.3" or "1.2.3".</param>
        /// <returns>Version as double.</returns>
        public static double ParseVersion(string version)
        {
            version = version.TrimStart('v', 'V');
            var parts = version.Split('.');
            if (parts.Length < 2) return 0;

            var major = parts[0];
            var minor = parts[1];
            return double.TryParse($"{major}.{minor}", NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result) ? result : 0;
        }
    }
}

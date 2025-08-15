using System;
using System.IO;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods for initializing required application folders.
    /// </summary>
    public static class AppFoldersHelper
    {
        /// <summary>
        /// Ensures that the database and security folders exist.
        /// Extracts folder paths from the SQLite connection string and AES file paths,
        /// and creates them if they do not exist.
        /// </summary>
        /// <param name="connectionString">
        /// SQLite connection string, e.g., "Data Source=C:\ProgramData\Servy\db\Servy.db;".
        /// The folder containing the database file will be created if missing.
        /// </param>
        /// <param name="aesKeyFilePath">
        /// Full path to the AES key file. The folder containing the file will be created if missing.
        /// </param>
        /// <param name="aesIVFilePath">
        /// Full path to the AES IV file. The folder containing the file will be created if missing.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any argument is null or whitespace.
        /// </exception>
        public static void EnsureFolders(string connectionString, string aesKeyFilePath, string aesIVFilePath)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));
            if (string.IsNullOrWhiteSpace(aesKeyFilePath))
                throw new ArgumentNullException(nameof(aesKeyFilePath));
            if (string.IsNullOrWhiteSpace(aesIVFilePath))
                throw new ArgumentNullException(nameof(aesIVFilePath));

            // Extract database folder from connection string
            var dataSourcePrefix = "Data Source=";
            var startIndex = connectionString.IndexOf(dataSourcePrefix, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0)
                throw new InvalidOperationException("Connection string does not contain 'Data Source='.");

            startIndex += dataSourcePrefix.Length;
            var endIndex = connectionString.IndexOf(';', startIndex);
            var dbFilePath = endIndex < 0
                ? connectionString.Substring(startIndex).Trim()
                : connectionString.Substring(startIndex, endIndex - startIndex).Trim();

            var dbFolder = Path.GetDirectoryName(dbFilePath);

            if (string.IsNullOrWhiteSpace(dbFolder))
                throw new InvalidOperationException("Cannot determine database folder path.");

            var aesKeyFolder = Path.GetDirectoryName(aesKeyFilePath);

            if (string.IsNullOrWhiteSpace(aesKeyFolder))
                throw new InvalidOperationException("Cannot determine AES key folder path.");

            var aesIVFolder = Path.GetDirectoryName(aesIVFilePath);

            if (string.IsNullOrWhiteSpace(aesIVFolder))
                throw new InvalidOperationException("Cannot determine AES IV folder path.");

            // Ensure folders exist
            Directory.CreateDirectory(dbFolder);
            Directory.CreateDirectory(aesKeyFolder);
            Directory.CreateDirectory(aesIVFolder);
        }

    }
}

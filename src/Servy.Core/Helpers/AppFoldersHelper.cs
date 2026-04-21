using Servy.Core.Config;
using Servy.Core.Security;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods for initializing and securing required application folders.
    /// </summary>
    public static class AppFoldersHelper
    {
        /// <summary>
        /// Retrieves the absolute path to the directory containing the application's entry-point executable.
        /// </summary>
        /// <returns>The directory path where the application is located.</returns>
        /// <remarks>
        /// <para>
        /// This method provides a robust "Source of Truth" for the application's home folder, specifically
        /// addressing the "Shell Game" found in .NET Single-File and Self-Contained deployments.
        /// </para>
        /// <para>
        /// It prioritizes <see cref="Environment.ProcessPath"/> to locate the actual native executable on disk. 
        /// This ensures that even if the runtime has extracted assemblies to a temporary directory (the behavior 
        /// of <see cref="AppContext.BaseDirectory"/> in some bundled modes), the path returned points to the 
        /// folder where the user actually placed the application.
        /// </para>
        /// </remarks>
        public static string GetAppDirectory()
        {
            // 1. Priority: The actual .exe on disk
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath))
            {
                return Path.GetDirectoryName(processPath)!;
            }

            // 2. Fallback: The directory where assemblies are located
            return AppContext.BaseDirectory;
        }

        /// <summary>
        /// Ensures that the database, security, and operational folders exist and are configured with correct security descriptors.
        /// </summary>
        /// <param name="connectionString">
        /// The SQLite connection string (e.g., <c>Data Source=C:\Path\To\Servy.db;</c>). 
        /// Used to determine the database directory.
        /// </param>
        /// <param name="aesKeyFilePath">Full filesystem path to the AES master key file.</param>
        /// <param name="aesIVFilePath">Full filesystem path to the legacy AES Initialization Vector (IV) file.</param>
        /// <remarks>
        /// <para>
        /// This method follows a hierarchical security approach:
        /// <list type="number">
        /// <item>
        /// <description>
        /// **Root Vault:** The primary data path defined in <see cref="AppConfig.ProgramDataPath"/> is secured first 
        /// by breaking inheritance to block standard users.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// **Operational Folders:** Subfolders (db, security, logs) are processed. If they reside within the Root Vault, 
        /// inheritance is preserved to allow manually granted service account permissions to cascade down.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// **External Paths:** If a folder is located outside the primary data path, it is treated as a new Root Vault 
        /// and inheritance is broken for safety.
        /// </description>
        /// </item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if any of the provided paths or connection strings are null or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the connection string format is invalid or directory names cannot be parsed.</exception>
        public static void EnsureFolders(string connectionString, string aesKeyFilePath, string aesIVFilePath)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));
            if (string.IsNullOrWhiteSpace(aesKeyFilePath))
                throw new ArgumentNullException(nameof(aesKeyFilePath));
            if (string.IsNullOrWhiteSpace(aesIVFilePath))
                throw new ArgumentNullException(nameof(aesIVFilePath));

            // Extract paths
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

            // 1. Establish the Root Vault FIRST so its ACLs exist for children to inherit
            SecurityHelper.CreateSecureDirectory(AppConfig.ProgramDataPath, breakInheritance: true);

            // 2. Process operational folders
            string[] subFolders = { dbFolder, aesKeyFolder, aesIVFolder };
            var normalizedRoot = AppConfig.ProgramDataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            foreach (var folder in subFolders)
            {
                // Skip if it exactly matches the root we just secured
                if (folder.Equals(AppConfig.ProgramDataPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                // If a folder is nested inside the master vault, we KEEP inheritance so custom service accounts cascade down.
                // If a folder is stored externally (e.g., D:\CustomDb), it acts as its own root vault and MUST break inheritance.
                bool isChildOfRoot = folder.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);

                SecurityHelper.CreateSecureDirectory(folder, breakInheritance: !isChildOfRoot);
            }
        }
    }
}
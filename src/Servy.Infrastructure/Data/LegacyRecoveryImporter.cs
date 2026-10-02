using Dapper;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Servy.Infrastructure.Data
{
    /// <summary>
    /// Imports the restart attempts counters that earlier versions of the wrapper kept in one file per service in the
    /// legacy recovery folder (<c>%ProgramData%\Servy\recovery\&lt;safe name&gt;_restartAttempts.dat</c>) into the
    /// <c>RestartAttempts</c> and <c>RestartAttemptsUpdatedAtTicks</c> columns of <c>Servy.db</c>.
    /// </summary>
    internal static class LegacyRecoveryImporter
    {
        /// <summary>The suffix of every legacy counter file.</summary>
        internal const string CounterFileSuffix = "_restartAttempts.dat";

        /// <summary>
        /// Imports every counter file of <paramref name="folderPath"/> whose service has a row and whose counter was never
        /// written through the database.
        /// </summary>
        /// <param name="connection">The open connection.</param>
        /// <param name="transaction">The migration's transaction.</param>
        /// <param name="folderPath">The legacy recovery folder; <see langword="null"/> or missing imports nothing.</param>
        /// <returns>
        /// <see langword="true"/> when the folder exists and every counter file in it was handled, so it can be deleted
        /// once the transaction is committed; <see langword="false"/> when there is no folder, or a file could not be
        /// read and the folder must stay for the next attempt.
        /// </returns>
        internal static bool Import(DbConnection connection, DbTransaction transaction, string? folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                return false;

            var services = connection.Query<(long Id, string Name, long? UpdatedAtTicks)>(
                $"SELECT Id, Name, RestartAttemptsUpdatedAtTicks FROM {SqlConstants.ServicesTableName};",
                transaction: transaction).ToList();

            var allHandled = true;
            var imported = 0;
            var matchedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var service in services)
            {
                if (string.IsNullOrEmpty(service.Name))
                    continue;

                var fileName = GetCounterFileName(service.Name);
                var path = Path.Combine(folderPath, fileName);
                if (!File.Exists(path))
                    continue;

                matchedFiles.Add(fileName);

                // Already written through the database (an earlier import, or the host): the file is stale
                if (service.UpdatedAtTicks.HasValue)
                    continue;

                try
                {
                    var attempts = ParseCounter(File.ReadAllText(path));
                    var updatedAtTicks = File.GetLastWriteTimeUtc(path).Ticks;

                    connection.Execute(
                        $"UPDATE {SqlConstants.ServicesTableName} SET RestartAttempts = @Attempts, RestartAttemptsUpdatedAtTicks = @UpdatedAtTicks WHERE Id = @Id;",
                        new { Attempts = attempts, UpdatedAtTicks = updatedAtTicks, service.Id },
                        transaction: transaction);
                    imported++;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Logger.Warn($"Could not import the legacy restart attempts file '{path}' of service '{service.Name}'; the recovery folder is kept and the import is retried at the next start.", ex);
                    allHandled = false;
                }
            }

            foreach (var orphan in Directory.EnumerateFiles(folderPath, "*" + CounterFileSuffix))
            {
                if (!matchedFiles.Contains(Path.GetFileName(orphan)))
                    Logger.Info($"Legacy restart attempts file '{orphan}' belongs to no installed service and was not imported.");
            }

            if (imported > 0)
                Logger.Info($"Imported {imported} restart attempts counter(s) from the legacy recovery folder '{folderPath}' into the database.");

            return allHandled;
        }

        /// <summary>
        /// Deletes the legacy recovery folder and everything in it.
        /// </summary>
        /// <param name="folderPath">The legacy recovery folder.</param>
        /// <returns><see langword="true"/> when the folder no longer exists.</returns>
        internal static bool DeleteFolder(string folderPath)
        {
            try
            {
                if (Directory.Exists(folderPath))
                {
                    // Never follow a link out of the vault: delete the link itself, not its target's content
                    var info = new DirectoryInfo(folderPath);
                    if ((info.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                        info.Delete();
                    else
                        Directory.Delete(folderPath, recursive: true);

                    Logger.Info($"Deleted the legacy recovery folder '{folderPath}' after importing its restart attempts counters.");
                }

                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Logger.Warn($"Could not delete the legacy recovery folder '{folderPath}'; it is retried at the next start.", ex);
                return false;
            }
        }

        /// <summary>
        /// Parses a legacy counter file's content the way the wrapper read it: a non-negative invariant integer, anything
        /// else (an empty or truncated file) being 0.
        /// </summary>
        /// <param name="content">The file content.</param>
        /// <returns>The counter.</returns>
        internal static int ParseCounter(string? content)
        {
            return int.TryParse((content ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var attempts) && attempts >= 0
                ? attempts
                : 0;
        }

        /// <summary>
        /// Builds the name of a service's legacy counter file, exactly as the wrapper named it.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <returns>The file name, <c>&lt;safe name&gt;_restartAttempts.dat</c>.</returns>
        internal static string GetCounterFileName(string serviceName) => MakeFilenameSafe(serviceName) + CounterFileSuffix;

        /// <summary>
        /// The wrapper's former <c>Service.MakeFilenameSafe</c>, kept verbatim so the files it wrote can be found: invalid
        /// file name characters become underscores, trailing spaces, tabs and periods are stripped, a reserved DOS device
        /// name gets one more leading underscore, and a short hash of the original name is appended.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <returns>The sanitized name.</returns>
        internal static string MakeFilenameSafe(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return $"_{ComputeShortHash(string.Empty)}";
            }

            string sanitized = name.TrimEnd(' ', '.', '\t');

            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "_";
            }

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(c, '_');
            }

            int firstDotIndex = sanitized.IndexOf('.');
            string leadingSegment = firstDotIndex >= 0 ? sanitized.Substring(0, firstDotIndex) : sanitized;
            string baseSegment = leadingSegment.TrimStart('_');

            if (ReservedNames.IsReservedDeviceName(baseSegment))
            {
                int existingUnderscores = leadingSegment.Length - baseSegment.Length;
                string protectionPrefix = new string('_', existingUnderscores + 1);
                sanitized = protectionPrefix + baseSegment + (firstDotIndex >= 0 ? sanitized.Substring(firstDotIndex) : string.Empty);
            }

            return $"{sanitized}_{ComputeShortHash(name)}";
        }

        /// <summary>
        /// Computes the deterministic 6-character hex hash the wrapper appended to the sanitized name.
        /// </summary>
        /// <param name="input">The original service name.</param>
        /// <returns>The first three bytes of the name's SHA-256, as lowercase hex.</returns>
        private static string ComputeShortHash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(bytes, 0, 3).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}

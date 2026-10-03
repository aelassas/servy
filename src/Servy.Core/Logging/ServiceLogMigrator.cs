using Servy.Core.Config;
using Servy.Core.Security;

namespace Servy.Core.Logging
{
    /// <summary>
    /// Moves the wrappers' log from its former place, <c>%ProgramData%\Servy\logs\Servy.Service.log</c>, to
    /// <c>%ProgramData%\Servy\logs\service\Servy.Service.log</c>, where it stays.
    /// </summary>
    /// <remarks>
    /// The content is copied into the new file rather than the file being renamed, so the new file gets the ACL of
    /// <c>logs\service\</c> instead of carrying the old folder's entries along. A non-empty former log is appended to
    /// the new one (which, at the first start after the upgrade, does not exist yet); an empty one is just deleted.
    /// The moved log is kept in <c>logs\service\</c> for the administrators: each wrapper now writes its own log in
    /// <c>logs\service\&lt;ServiceName&gt;\</c> (<see cref="ServiceLogPaths"/>), and no service account can read or
    /// write the moved log, which may hold the entries of every service.
    /// </remarks>
    public static class ServiceLogMigrator
    {
        /// <summary>
        /// Migrates the wrappers' log in the vault's logs folder.
        /// </summary>
        /// <returns><see langword="true"/> when there is no former log left to migrate.</returns>
        public static bool Migrate()
            => Migrate(AppConfig.LogsFolderPath, AppConfig.ServiceLogsFolderPath, AppConfig.ServyServiceLogFileName);

        /// <summary>
        /// Migrates <paramref name="fileName"/> from <paramref name="legacyFolder"/> to <paramref name="serviceFolder"/>.
        /// </summary>
        /// <param name="legacyFolder">The folder the log was written to before.</param>
        /// <param name="serviceFolder">The folder the log is written to now; created when it does not exist.</param>
        /// <param name="fileName">The log's file name.</param>
        /// <returns><see langword="true"/> when there is no former log left to migrate.</returns>
        /// <remarks>
        /// The content is copied first and the former log deleted afterwards, so the two steps can fail independently.
        /// When the delete fails once the content is already in the target, the former log is emptied in place instead,
        /// which is what keeps the retry at the next start from appending the same content a second time.
        /// </remarks>
        public static bool Migrate(string legacyFolder, string serviceFolder, string fileName)
        {
            if (string.IsNullOrWhiteSpace(legacyFolder)) throw new ArgumentException("The legacy folder is required.", nameof(legacyFolder));
            if (string.IsNullOrWhiteSpace(serviceFolder)) throw new ArgumentException("The service folder is required.", nameof(serviceFolder));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("The file name is required.", nameof(fileName));

            var legacyPath = Path.Combine(legacyFolder, fileName);
            if (!File.Exists(legacyPath))
                return true;

            try
            {
                var legacy = new FileInfo(legacyPath);

                // A link would make the copy read, and the delete remove, a file outside the vault
                if ((legacy.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    Logger.Warn($"The former service log '{legacyPath}' is a reparse point (symlink/junction) and was not migrated.");
                    return false;
                }

                if (legacy.Length > 0)
                {
                    if (!Directory.Exists(serviceFolder))
                        SecurityHelper.CreateSecureDirectory(serviceFolder, breakInheritance: false);

                    var targetPath = Path.Combine(serviceFolder, fileName);
                    using (var source = new FileStream(legacyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var target = new FileStream(targetPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    {
                        source.CopyTo(target);
                        target.Flush(flushToDisk: true);
                    }

                    Logger.Info($"Moved the content of the former service log '{legacyPath}' to '{targetPath}'.");
                }

                try
                {
                    File.Delete(legacyPath);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // The content is already in the target, so a start that found the former log again would append it
                    // a second time. Emptying it in place leaves the next start nothing to copy and only the file to
                    // delete. The other handle has to grant write access for that to be possible; when it does not,
                    // nothing is emptied and the rethrow reports the failure exactly as it did before.
                    using (var stream = new FileStream(legacyPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                    {
                        stream.SetLength(0);
                    }

                    throw;
                }

                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Logger.Warn($"Could not migrate the former service log '{legacyPath}'; the migration is retried at the next start.", ex);
                return false;
            }
        }
    }
}

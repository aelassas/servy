using Servy.Core.Config;
using Servy.Core.Security;
using System;
using System.IO;

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
    /// The former log is renamed inside <c>logs\</c> before its content is read, so the step that can fail on a
    /// sharing violation runs before anything is written to the new file.
    /// </remarks>
    public static class ServiceLogMigrator
    {
        /// <summary>
        /// The suffix the former log is renamed with before its content is copied. A start that finds a file with
        /// this suffix is resuming a migration an earlier start could not finish.
        /// </summary>
        private const string StagingSuffix = ".migrating";

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
        /// The former log is renamed to <see cref="StagingSuffix"/> inside <paramref name="legacyFolder"/> first, and
        /// only the renamed file is read and deleted. A rename needs the same <c>DELETE</c> access as a delete, so a
        /// foreign handle that does not grant it fails the migration before anything has been appended to the new log
        /// and the retry at the next start finds the former log untouched. A start that finds a file left with the
        /// staging suffix resumes that earlier migration before it moves a former log written since.
        /// </remarks>
        public static bool Migrate(string legacyFolder, string serviceFolder, string fileName)
        {
            if (string.IsNullOrWhiteSpace(legacyFolder)) throw new ArgumentException("The legacy folder is required.", nameof(legacyFolder));
            if (string.IsNullOrWhiteSpace(serviceFolder)) throw new ArgumentException("The service folder is required.", nameof(serviceFolder));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("The file name is required.", nameof(fileName));

            var legacyPath = Path.Combine(legacyFolder, fileName);
            var stagingPath = legacyPath + StagingSuffix;
            if (!File.Exists(legacyPath) && !File.Exists(stagingPath))
                return true;

            try
            {
                // A staging file is the former log of a start that renamed it and could not finish the move. Its
                // entries are older than anything written since, so it is moved before the former log below.
                if (File.Exists(stagingPath))
                {
                    var staging = new FileInfo(stagingPath);

                    // A link would make the copy read, and the delete remove, a file outside the vault
                    if ((staging.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    {
                        Logger.Warn($"The staged service log '{stagingPath}' is a reparse point (symlink/junction) and was not migrated.");
                        return false;
                    }

                    MoveStagedLog(stagingPath, legacyPath, serviceFolder, fileName);
                }

                if (!File.Exists(legacyPath))
                    return true;

                var legacy = new FileInfo(legacyPath);

                // A link would make the copy read, and the delete remove, a file outside the vault
                if ((legacy.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    Logger.Warn($"The former service log '{legacyPath}' is a reparse point (symlink/junction) and was not migrated.");
                    return false;
                }

                // The step that can fail on a sharing violation, run before anything is read or written: a rename
                // needs the DELETE access the delete needs, so a handle without it fails here with nothing copied.
                File.Move(legacyPath, stagingPath);

                MoveStagedLog(stagingPath, legacyPath, serviceFolder, fileName);

                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Logger.Warn($"Could not migrate the former service log '{legacyPath}'; the migration is retried at the next start.", ex);
                return false;
            }
        }

        /// <summary>
        /// Appends the staged former log to the log in <paramref name="serviceFolder"/> and deletes the staged file.
        /// </summary>
        /// <param name="stagingPath">The staged former log, which is the file that is read and then removed.</param>
        /// <param name="formerPath">
        /// The path the staged file was renamed from, used in the log entries so that they name the file an
        /// administrator knows rather than the staging name.
        /// </param>
        /// <param name="serviceFolder">The folder the log is written to now; created when it does not exist.</param>
        /// <param name="fileName">The log's file name.</param>
        /// <exception cref="IOException">
        /// The staged file could not be read, the new log could not be appended to, or the staged file could not be
        /// deleted. When the content was already copied, the staged file is emptied in place before the throw is
        /// passed on, so the next start finds nothing left to append.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        /// Access to the staged file or to the new log was denied. The staged file is emptied in place first, on the
        /// same condition and for the same reason as for <see cref="IOException"/>.
        /// </exception>
        private static void MoveStagedLog(string stagingPath, string formerPath, string serviceFolder, string fileName)
        {
            if (new FileInfo(stagingPath).Length > 0)
            {
                if (!Directory.Exists(serviceFolder))
                    SecurityHelper.CreateSecureDirectory(serviceFolder, breakInheritance: false);

                var targetPath = Path.Combine(serviceFolder, fileName);
                using (var source = new FileStream(stagingPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var target = new FileStream(targetPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                {
                    source.CopyTo(target);
                    target.Flush(flushToDisk: true);
                }

                Logger.Info($"Moved the content of the former service log '{formerPath}' to '{targetPath}'.");
            }

            try
            {
                File.Delete(stagingPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // The content is already in the target, so a start that found the staged file again would append it
                // a second time. Emptying it in place leaves the next start nothing to copy and only the file to
                // delete. The other handle has to grant write access for that to be possible; when it does not,
                // nothing is emptied, and the exception from opening the file for the write propagates in place of
                // the delete's, so the rethrow below is not reached.
                using (var stream = new FileStream(stagingPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                    stream.SetLength(0);
                }

                throw;
            }
        }
    }
}

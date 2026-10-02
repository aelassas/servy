using Servy.Core.Config;
using Servy.Core.Data;
using Servy.Core.DTOs;
using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Security
{
    /// <summary>
    /// Hardens Servy's vault for a service account with the least privilege the service needs: Read &amp; Execute on
    /// Servy's binaries, Read on its settings files, and Read, Write, Delete on the files in <c>logs\service\</c>, the
    /// only folder it writes. The account gets nothing on the vault root, <c>%ProgramData%\Servy</c>, itself, and
    /// nothing at all on <c>db\</c>, <c>security\</c> and <c>logs\</c> or anything in them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the C# port of the former <c>Set-ServyExePermissions.ps1</c>, so Servy applies the hardening itself
    /// wherever its executables run. It is run when a service is installed under an account other than Local System
    /// (<see cref="Services.ServiceManager"/>), and again for every such account after the desktop app, the Manager or
    /// the CLI has extracted a binary into the vault, because a newly written file carries no grant for the service
    /// accounts and would be unreadable to them.
    /// </para>
    /// <para>
    /// Each hardened file stops inheriting from the vault, is owned by Builtin Administrators, and keeps Full Control
    /// for SYSTEM and Administrators (well-known SIDs, so the result is language-agnostic). Explicit grants for
    /// Users, Authenticated Users and Everyone are purged; explicit entries for other principals are kept, so
    /// hardening one account never removes another's access. A file that is a reparse point or has more than one hard
    /// link is not touched and is reported as failed.
    /// </para>
    /// <para>
    /// The grants are taken back when the last service using an account goes away: after an uninstall, and after an
    /// install moves a service to another account, <see cref="Services.ServiceManager"/> calls
    /// <see cref="RevokeIfUnusedAsync"/>, which removes the account's explicit entries from the vault root, the
    /// writable folders and every hardened file unless a remaining service still runs under it (#7161).
    /// </para>
    /// <para>
    /// The wrapper never opens <c>Servy.db</c> or the encryption key: it reads its own configuration and writes its own
    /// runtime state and restart attempts counter through the Servy host service, over a named pipe the host serves
    /// only to the process of the service a request is about (#7224, #7248). So the account gets no entry on
    /// <c>db\</c> and <c>security\</c> or on any file in them, and every entry a previous version gave it there (Read and
    /// Write on <c>Servy.db</c>, the folder grant that covered the <c>-wal</c>/<c>-shm</c> files, Read on the key) is
    /// removed when it is hardened again. The same goes for <c>logs\</c>, which holds the logs of the administrative
    /// tools and the host.
    /// </para>
    /// <para>
    /// The service writes in one folder only, <c>logs\service\</c>, where the wrapper and the restarter log and rotate
    /// their files. The account gets List Folder and Create Files on that folder (creating the log, and the new file of a
    /// rotation, needs Create Files) and Read, Write and Delete on the files in it (<see cref="GetWritableFolderFileRights"/>).
    /// It never gets Delete on a folder: it can neither rename nor delete a folder, and outside <c>logs\service\</c> it
    /// can write or delete nothing. An account that a previous version granted Modify on the vault root loses that grant
    /// when it is hardened again.
    /// </para>
    /// <para>
    /// The process must be elevated, as every Servy process that installs a service already is.
    /// </para>
    /// </remarks>
    public class ServyExePermissionsHardener : IServyExePermissionsHardener
    {
        private static readonly SecurityIdentifier AdministratorsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        private static readonly SecurityIdentifier LocalSystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

        /// <summary>
        /// Initializes a new instance of the <see cref="ServyExePermissionsHardener"/> class for the vault at
        /// <see cref="AppConfig.ProgramDataPath"/>.
        /// </summary>
        public ServyExePermissionsHardener() : this(AppConfig.ProgramDataPath)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServyExePermissionsHardener"/> class for the vault at
        /// <paramref name="vaultDirectory"/>.
        /// </summary>
        /// <param name="vaultDirectory">The vault directory to harden; tests point it at a temporary directory.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="vaultDirectory"/> is null or blank.</exception>
        public ServyExePermissionsHardener(string vaultDirectory)
        {
            if (string.IsNullOrWhiteSpace(vaultDirectory))
                throw new ArgumentException("The vault directory is required.", nameof(vaultDirectory));

            VaultDirectory = vaultDirectory;
        }

        /// <summary>
        /// Gets the vault directory this instance hardens.
        /// </summary>
        public string VaultDirectory { get; }

        /// <summary>
        /// Determines whether an account needs the hardening: any account other than Local System.
        /// </summary>
        /// <param name="account">The account a service runs under.</param>
        /// <returns><see langword="true"/> for a non-blank account that is not an alias of Local System.</returns>
        public static bool IsHardeningCandidate(string account)
            => !string.IsNullOrWhiteSpace(account) && !ServiceAccounts.LocalSystemAliases.Contains(account.Trim());

        /// <inheritdoc />
        public virtual async Task<bool> HardenAsync(string targetAccount, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(targetAccount))
            {
                Logger.Warn("Executable permission hardening skipped: no target account was given.");
                return false;
            }

            var account = targetAccount.Trim();
            if (!IsHardeningCandidate(account))
            {
                Logger.Debug($"Executable permission hardening skipped for '{account}': Local System keeps Full Control.");
                return true;
            }

            ExePermissionsHardeningResult result;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                result = await Task.Run(() => Harden(account, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"Executable permission hardening for '{account}' was cancelled before it completed.");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Executable permission hardening for '{account}' failed.", ex);
                return false;
            }

            return ReportResult(result);
        }

        /// <inheritdoc />
        public virtual async Task HardenServiceAccountsAsync(IServiceRepository serviceRepository, CancellationToken cancellationToken)
        {
            if (serviceRepository == null)
                throw new ArgumentNullException(nameof(serviceRepository));

            List<string> accounts;
            try
            {
                if (!IsProcessElevated())
                {
                    Logger.Debug("Executable permission hardening of the service accounts skipped: the process is not elevated.");
                    return;
                }

                var services = await serviceRepository.GetAllAsync(decrypt: false, cancellationToken);
                accounts = GetServiceAccounts(services);
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("Executable permission hardening of the service accounts was cancelled.");
                return;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to read the service accounts to re-apply the executable permission hardening.", ex);
                return;
            }

            foreach (var account in accounts)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                await HardenAsync(account, cancellationToken);
            }
        }

        /// <inheritdoc />
        public virtual async Task<bool> RevokeIfUnusedAsync(string targetAccount, IServiceRepository serviceRepository, CancellationToken cancellationToken)
        {
            if (serviceRepository == null)
                throw new ArgumentNullException(nameof(serviceRepository));

            if (!IsHardeningCandidate(targetAccount))
            {
                Logger.Debug($"Vault access revocation skipped for '{targetAccount}': Local System was never granted anything.");
                return true;
            }

            var account = targetAccount.Trim();
            ExePermissionsHardeningResult result;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remaining = GetServiceAccounts(await serviceRepository.GetAllAsync(decrypt: false, cancellationToken));
                result = await Task.Run(() => RevokeIfUnused(account, remaining, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"Revoking the vault access of '{account}' was cancelled before it completed.");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Revoking the vault access of '{account}' failed.", ex);
                return false;
            }

            return ReportRevokeResult(result);
        }

        /// <summary>
        /// Removes <paramref name="account"/>'s explicit entries from the vault root, the writable folders and every
        /// hardened file, unless one of <paramref name="remainingAccounts"/> is the same account.
        /// </summary>
        /// <param name="account">The trimmed account whose access is revoked.</param>
        /// <param name="remainingAccounts">The accounts the remaining services run under.</param>
        /// <param name="cancellationToken">A token checked before each item.</param>
        /// <returns>The outcome, with the items the entries were removed from and those that could not be rewritten.</returns>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        internal ExePermissionsHardeningResult RevokeIfUnused(string account, IReadOnlyCollection<string> remainingAccounts, CancellationToken cancellationToken)
        {
            var result = new ExePermissionsHardeningResult(account);

            var targetSid = ResolveAccount(account);
            if (targetSid == null)
                return result.Complete(ExePermissionsHardeningStatus.InvalidAccount, "the account could not be resolved on this machine or domain");

            if (IsBroadGroup(targetSid))
                return result.Complete(ExePermissionsHardeningStatus.InvalidAccount, "it is a broad group (Everyone, Users or Authenticated Users)");

            if (targetSid.Equals(AdministratorsSid) || targetSid.Equals(LocalSystemSid))
                return result.Complete(ExePermissionsHardeningStatus.Skipped, "it is a protected administrative principal that keeps Full Control");

            // The same account can be written two ways (.\user and MACHINE\user), so the SIDs decide
            foreach (var other in remainingAccounts ?? (IReadOnlyCollection<string>)Array.Empty<string>())
            {
                var otherSid = ResolveAccount(other);
                if (string.Equals(other, account, StringComparison.OrdinalIgnoreCase) || (otherSid != null && targetSid.Equals(otherSid)))
                    return result.Complete(ExePermissionsHardeningStatus.InUse, $"another service still runs under it (as '{other}')");
            }

            if (!IsProcessElevated())
                return result.Complete(ExePermissionsHardeningStatus.NotElevated, "the process is not elevated");

            if (!Directory.Exists(VaultDirectory))
                return result.Complete(ExePermissionsHardeningStatus.VaultNotFound, $"'{VaultDirectory}' does not exist");

            if ((new DirectoryInfo(VaultDirectory).Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                result.AddFailed(VaultDirectory);
                return result.Complete(ExePermissionsHardeningStatus.Failed, "the vault directory is a reparse point (symlink/junction)");
            }

            RevokeEntries(string.Empty, targetSid, result);

            foreach (var folder in GetWritableFolders())
            {
                cancellationToken.ThrowIfCancellationRequested();
                RevokeEntries(folder, targetSid, result);
            }

            RevokeClosedFolders(targetSid, result, cancellationToken);

            foreach (var target in GetTargetFiles())
            {
                cancellationToken.ThrowIfCancellationRequested();
                RevokeEntries(target.RelativePath, targetSid, result);
            }

            return result.Complete(result.Failed.Count > 0 ? ExePermissionsHardeningStatus.Failed : ExePermissionsHardeningStatus.Revoked);
        }

        /// <summary>
        /// Selects the distinct accounts, other than Local System, that the given services run under.
        /// </summary>
        /// <param name="services">The installed services.</param>
        /// <returns>The trimmed accounts, compared case-insensitively, in first-seen order.</returns>
        public static List<string> GetServiceAccounts(IEnumerable<ServiceDto> services)
        {
            return (services ?? Enumerable.Empty<ServiceDto>())
                .Where(s => s != null && s.RunAsLocalSystem != true && IsHardeningCandidate(s.UserAccount))
                .Select(s => s.UserAccount.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Applies the hardening for <paramref name="account"/> and records what happened to each file.
        /// </summary>
        /// <param name="account">The trimmed account to harden for.</param>
        /// <param name="cancellationToken">A token checked before each file.</param>
        /// <returns>The outcome, with the files in each state.</returns>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        internal ExePermissionsHardeningResult Harden(string account, CancellationToken cancellationToken)
        {
            var result = new ExePermissionsHardeningResult(account);

            if (!IsProcessElevated())
                return result.Complete(ExePermissionsHardeningStatus.NotElevated, "the process is not elevated");

            if (!Directory.Exists(VaultDirectory))
                return result.Complete(ExePermissionsHardeningStatus.VaultNotFound, $"'{VaultDirectory}' does not exist");

            var targetSid = ResolveAccount(account);
            if (targetSid == null)
                return result.Complete(ExePermissionsHardeningStatus.InvalidAccount, "the account could not be resolved on this machine or domain");

            if (IsBroadGroup(targetSid))
                return result.Complete(ExePermissionsHardeningStatus.InvalidAccount, "it is a broad group (Everyone, Users or Authenticated Users) that the hardening exists to remove");

            if (targetSid.Equals(AdministratorsSid) || targetSid.Equals(LocalSystemSid))
                return result.Complete(ExePermissionsHardeningStatus.Skipped, "it is a protected administrative principal that keeps Full Control");

            var isAdministrator = IsAdministratorsMember(targetSid);
            if (isAdministrator == true)
                return result.Complete(ExePermissionsHardeningStatus.Skipped, "it is a member of Administrators, which keeps Full Control");

            if (isAdministrator == null)
            {
                Logger.Info($"Could not rule out that '{account}' is a member of Administrators through a nested group; " +
                    "if it is, its effective access stays Full Control whatever the hardening writes.");
            }

            // Everything under a linked vault would be written somewhere else
            if ((new DirectoryInfo(VaultDirectory).Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                result.AddFailed(VaultDirectory);
                return result.Complete(ExePermissionsHardeningStatus.Failed, "the vault directory is a reparse point (symlink/junction)");
            }

            RevokeVaultRootAccess(targetSid, result);

            // Take back everything a previous version granted in db\, security\ and logs\ before granting logs\service\
            RevokeClosedFolders(targetSid, result, cancellationToken);

            foreach (var folder in GetWritableFolders())
            {
                cancellationToken.ThrowIfCancellationRequested();
                GrantFolderAccess(folder, targetSid, result);
            }

            foreach (var target in GetTargetFiles())
            {
                cancellationToken.ThrowIfCancellationRequested();
                HardenFile(target, targetSid, result);
            }

            var status = result.Failed.Count > 0
                ? ExePermissionsHardeningStatus.Failed
                : result.Missing.Count > 0 ? ExePermissionsHardeningStatus.Incomplete : ExePermissionsHardeningStatus.Hardened;

            return result.Complete(status);
        }

        /// <summary>
        /// Lists the files the hardening locks down, relative to <see cref="VaultDirectory"/>, with the rights the target
        /// account keeps on each.
        /// </summary>
        /// <returns>The binaries and every DLL in the vault (Read &amp; Execute) and the <c>.exe.config</c> files (Read,
        /// optional). The database and the encryption key are not listed: the service account has no access to them.</returns>
        internal IReadOnlyList<ExePermissionsTarget> GetTargetFiles()
        {
            var executables = new[] { AppConfig.ServyServiceUIExe, AppConfig.ServyServiceCLIExe, AppConfig.ServyRestarterExe, AppConfig.ServyHostExe };

            var targets = executables
                .Select(exe => new ExePermissionsTarget(exe, FileSystemRights.ReadAndExecute))
                .ToList();
            targets.Add(new ExePermissionsTarget(AppConfig.HandleExeFileName + ".exe", FileSystemRights.ReadAndExecute));

            // The service, the CLI service and the restarter load their assemblies from the vault, so every DLL
            // there is as much a binary as the executables are
            foreach (var dll in Directory.GetFiles(VaultDirectory, "*.dll", SearchOption.TopDirectoryOnly).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                targets.Add(new ExePermissionsTarget(Path.GetFileName(dll), FileSystemRights.ReadAndExecute));

            foreach (var exe in executables)
                targets.Add(new ExePermissionsTarget(exe + ".config", FileSystemRights.Read, optional: true));

            return targets;
        }

        /// <summary>
        /// Lists the folders, relative to <see cref="VaultDirectory"/>, in which the service creates, rewrites and deletes
        /// files: <c>logs\service\</c>, where the wrapper and the restarter write and rotate their logs.
        /// </summary>
        /// <returns>The writable folders.</returns>
        internal static IReadOnlyList<string> GetWritableFolders()
            => new[] { Path.Combine(AppConfig.LogsFolderName, AppConfig.ServiceLogsFolderName) };

        /// <summary>
        /// Lists the folders, relative to <see cref="VaultDirectory"/>, on which and in which the service account must
        /// hold no entry: the database, the encryption keys, and the logs of the administrative tools and the host.
        /// </summary>
        /// <returns>The closed folders.</returns>
        internal static IReadOnlyList<string> GetClosedFolders()
            => new[] { AppConfig.DbFolderName, AppConfig.SecurityFolderName, AppConfig.LogsFolderName };

        /// <summary>
        /// Returns the rights the target gets on the files inside one of the <see cref="GetWritableFolders"/> folders.
        /// </summary>
        /// <param name="relativePath">The writable folder, relative to <see cref="VaultDirectory"/>.</param>
        /// <returns>Read, Write and Delete: the logger rewrites, rotates and deletes its files.</returns>
        internal static FileSystemRights GetWritableFolderFileRights(string relativePath)
            => FileSystemRights.Read | FileSystemRights.Write | FileSystemRights.Delete;

        /// <summary>
        /// Removes the target's explicit entries from each of the <see cref="GetClosedFolders"/> folders and from every file
        /// directly in them, such as the grants on <c>db\Servy.db</c> and <c>security\aes_key.dat</c> and the folder
        /// grants of <c>db\</c> and <c>logs\</c> that earlier versions wrote. The entries the files inherited from those
        /// folder grants go with them.
        /// </summary>
        /// <param name="targetSid">The account whose entries are removed.</param>
        /// <param name="result">Receives each item the entries were removed from, or that failed.</param>
        /// <param name="cancellationToken">A token checked before each folder.</param>
        private void RevokeClosedFolders(SecurityIdentifier targetSid, ExePermissionsHardeningResult result, CancellationToken cancellationToken)
        {
            foreach (var folder in GetClosedFolders())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var path = Path.Combine(VaultDirectory, folder);
                if (!Directory.Exists(path))
                    continue;

                RevokeEntries(folder, targetSid, result);

                // A linked folder was refused above; never enumerate through it
                if ((new DirectoryInfo(path).Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    continue;

                string[] files;
                try
                {
                    files = Directory.GetFiles(path);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Logger.Error($"Failed to list '{folder}' to revoke the access of '{result.Account}' to its files.", ex);
                    result.AddFailed(folder);
                    continue;
                }

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    RevokeEntries(Path.Combine(folder, Path.GetFileName(file)), targetSid, result);
                }
            }
        }

        /// <summary>
        /// Removes every explicit entry the target holds on the vault root, such as the Modify grant and the Delete
        /// denial a previous version wrote there.
        /// </summary>
        /// <param name="targetSid">The target account.</param>
        /// <param name="result">Receives the vault as failed when its ACL cannot be rewritten.</param>
        private void RevokeVaultRootAccess(SecurityIdentifier targetSid, ExePermissionsHardeningResult result)
        {
            try
            {
                var vault = new DirectoryInfo(VaultDirectory);
                var acl = vault.GetAccessControl(AccessControlSections.Access);
                acl.PurgeAccessRules(targetSid);
                vault.SetAccessControl(acl);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to revoke the access of '{result.Account}' to '{VaultDirectory}'.", ex);
                result.AddFailed(VaultDirectory);
            }
        }

        /// <summary>
        /// Removes every explicit entry the target holds on one vault item. A missing item is ignored, and a link is
        /// refused the way the hardening refuses it, because the ACL write would land outside the vault.
        /// </summary>
        /// <param name="relativePath">The item, relative to <see cref="VaultDirectory"/>; empty for the vault itself.</param>
        /// <param name="targetSid">The account whose entries are removed.</param>
        /// <param name="result">Receives the item as revoked when it held an entry, or as failed.</param>
        private void RevokeEntries(string relativePath, SecurityIdentifier targetSid, ExePermissionsHardeningResult result)
        {
            var isVault = relativePath.Length == 0;
            var path = isVault ? VaultDirectory : Path.Combine(VaultDirectory, relativePath);
            var name = isVault ? VaultDirectory : relativePath;
            try
            {
                var isDirectory = Directory.Exists(path);
                if (!isDirectory && !File.Exists(path))
                    return;

                if (!isVault && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    Logger.Error($"Cannot revoke the access of '{result.Account}' to '{name}': it is a reparse point (symlink/junction).");
                    result.AddFailed(name);
                    return;
                }

                if (!isDirectory)
                {
                    var links = GetHardLinkCount(path);
                    if (links != 1)
                    {
                        Logger.Error(links < 1
                            ? $"Cannot revoke the access of '{result.Account}' to '{name}': its hard link count could not be verified."
                            : $"Cannot revoke the access of '{result.Account}' to '{name}': it has {links} NTFS hard links.");
                        result.AddFailed(name);
                        return;
                    }
                }

                if (isDirectory)
                {
                    var directory = new DirectoryInfo(path);
                    var acl = directory.GetAccessControl(AccessControlSections.Access);
                    if (!HoldsExplicitEntry(acl, targetSid))
                        return;
                    acl.PurgeAccessRules(targetSid);
                    directory.SetAccessControl(acl);
                }
                else
                {
                    var file = new FileInfo(path);
                    var acl = file.GetAccessControl(AccessControlSections.Access);
                    if (!HoldsExplicitEntry(acl, targetSid))
                        return;
                    acl.PurgeAccessRules(targetSid);
                    file.SetAccessControl(acl);
                }

                Logger.Debug($"Revoked the access of '{result.Account}' to '{name}'.");
                result.AddRevoked(name);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to revoke the access of '{result.Account}' to '{name}'.", ex);
                result.AddFailed(name);
            }
        }

        /// <summary>
        /// Determines whether an ACL carries an explicit entry for <paramref name="sid"/>.
        /// </summary>
        /// <param name="acl">The ACL to inspect.</param>
        /// <param name="sid">The account.</param>
        /// <returns><see langword="true"/> when at least one explicit allow or deny entry names the account.</returns>
        private static bool HoldsExplicitEntry(FileSystemSecurity acl, SecurityIdentifier sid)
        {
            foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, false, typeof(SecurityIdentifier)))
            {
                if (sid.Equals(rule.IdentityReference))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Lets the target create files in a writable folder and use the files created there with the rights
        /// <see cref="GetWritableFolderFileRights"/> names for that folder, creating the folder first when it does not
        /// exist yet.
        /// </summary>
        /// <param name="relativePath">The folder, relative to <see cref="VaultDirectory"/>.</param>
        /// <param name="targetSid">The target account.</param>
        /// <param name="result">Receives the folder as failed when it cannot be created or granted.</param>
        private void GrantFolderAccess(string relativePath, SecurityIdentifier targetSid, ExePermissionsHardeningResult result)
        {
            var path = Path.Combine(VaultDirectory, relativePath);
            try
            {
                // The service account cannot create folders in the vault root, so create it now, secured the way
                // AppFoldersHelper secures the vault's subfolders
                if (!Directory.Exists(path))
                    SecurityHelper.CreateSecureDirectory(path, breakInheritance: false);

                var folder = new DirectoryInfo(path);
                if ((folder.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    Logger.Error($"Cannot grant '{result.Account}' access to '{relativePath}': it is a reparse point (symlink/junction).");
                    result.AddFailed(relativePath);
                    return;
                }

                var acl = folder.GetAccessControl(AccessControlSections.Access);
                acl.PurgeAccessRules(targetSid);

                // The folder itself: list it and create files in it, but never delete or rename it
                acl.AddAccessRule(new FileSystemAccessRule(
                    targetSid,
                    FileSystemRights.Read | FileSystemRights.CreateFiles,
                    InheritanceFlags.None,
                    PropagationFlags.None,
                    AccessControlType.Allow));

                // The files in it: read, write and delete (log rotation)
                acl.AddAccessRule(new FileSystemAccessRule(
                    targetSid,
                    GetWritableFolderFileRights(relativePath),
                    InheritanceFlags.ObjectInherit,
                    PropagationFlags.InheritOnly,
                    AccessControlType.Allow));

                folder.SetAccessControl(acl);
                result.AddGrantedFolder(relativePath);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to grant '{result.Account}' access to '{relativePath}'.", ex);
                result.AddFailed(relativePath);
            }
        }

        /// <summary>
        /// Locks one file down for the target account.
        /// </summary>
        /// <param name="target">The file and the rights the target keeps on it.</param>
        /// <param name="targetSid">The target account.</param>
        /// <param name="result">Receives the file as hardened, missing, skipped or failed.</param>
        private void HardenFile(ExePermissionsTarget target, SecurityIdentifier targetSid, ExePermissionsHardeningResult result)
        {
            var path = Path.Combine(VaultDirectory, target.RelativePath);

            if (!File.Exists(path))
            {
                if (target.Optional)
                    result.AddSkipped(target.RelativePath);
                else
                    result.AddMissing(target.RelativePath);
                return;
            }

            try
            {
                var file = new FileInfo(path);

                // A link would make the ACL write land on a file outside the vault (#6866)
                if ((file.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    Logger.Error($"Cannot harden '{target.RelativePath}': it is a reparse point (symlink/junction).");
                    result.AddFailed(target.RelativePath);
                    return;
                }

                var links = GetHardLinkCount(path);
                if (links != 1)
                {
                    Logger.Error(links < 1
                        ? $"Cannot harden '{target.RelativePath}': its hard link count could not be verified."
                        : $"Cannot harden '{target.RelativePath}': it has {links} NTFS hard links.");
                    result.AddFailed(target.RelativePath);
                    return;
                }

                var acl = file.GetAccessControl();
                var previousOwner = acl.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
                acl.SetOwner(AdministratorsSid);

                // Break inheritance and drop the inherited entries in one pass
                acl.SetAccessRuleProtection(true, false);

                // Purge the target's explicit entries and any explicit grant to a broad group; keep everyone else's
                foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, false, typeof(SecurityIdentifier)))
                {
                    var sid = (SecurityIdentifier)rule.IdentityReference;
                    if (sid.Equals(targetSid) || (rule.AccessControlType == AccessControlType.Allow && IsBroadGroup(sid)))
                        acl.RemoveAccessRuleSpecific(rule);
                }

                acl.SetAccessRule(new FileSystemAccessRule(AdministratorsSid, FileSystemRights.FullControl, AccessControlType.Allow));
                acl.SetAccessRule(new FileSystemAccessRule(LocalSystemSid, FileSystemRights.FullControl, AccessControlType.Allow));
                acl.SetAccessRule(new FileSystemAccessRule(targetSid, target.Rights, AccessControlType.Allow));

                file.SetAccessControl(acl);

                if (previousOwner != null && !previousOwner.Equals(AdministratorsSid))
                    Logger.Debug($"Owner of '{target.RelativePath}' replaced: '{previousOwner.Value}' -> 'BUILTIN\\Administrators'.");

                Logger.Debug($"Hardened '{target.RelativePath}' for '{result.Account}' ({target.Rights}).");
                result.AddHardened(target.RelativePath);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to harden '{target.RelativePath}' for '{result.Account}'.", ex);
                result.AddFailed(target.RelativePath);
            }
        }

        /// <summary>
        /// Logs the outcome at the level it calls for.
        /// </summary>
        /// <param name="result">The outcome of one hardening run.</param>
        /// <returns><see langword="true"/> for <see cref="ExePermissionsHardeningStatus.Hardened"/>,
        /// <see cref="ExePermissionsHardeningStatus.Incomplete"/> and <see cref="ExePermissionsHardeningStatus.Skipped"/>.</returns>
        internal static bool ReportResult(ExePermissionsHardeningResult result)
        {
            var account = result.Account;
            switch (result.Status)
            {
                case ExePermissionsHardeningStatus.Hardened:
                    Logger.Info($"Hardened Servy's vault for '{account}': {result.Hardened.Count} files locked down.");
                    return true;
                case ExePermissionsHardeningStatus.Incomplete:
                    // A binary is only extracted by the app that embeds it; each extraction re-applies the hardening.
                    Logger.Info($"Hardened Servy's vault for '{account}': {result.Hardened.Count} files locked down. " +
                        $"Not present yet, hardened once extracted: {string.Join(", ", result.Missing)}.");
                    return true;
                case ExePermissionsHardeningStatus.Skipped:
                    Logger.Info($"Executable permission hardening skipped for '{account}': {result.Reason}.");
                    return true;
                case ExePermissionsHardeningStatus.Failed:
                    Logger.Error($"Executable permission hardening for '{account}' failed on: {string.Join(", ", result.Failed)}. " +
                        "Check file locks and the vault's permissions; the hardening is re-applied the next time the service is installed.");
                    return false;
                default:
                    Logger.Error($"Executable permission hardening for '{account}' was not applied: {result.Reason}.");
                    return false;
            }
        }

        /// <summary>
        /// Logs the outcome of a revocation at the level it calls for.
        /// </summary>
        /// <param name="result">The outcome of one revocation.</param>
        /// <returns><see langword="true"/> for <see cref="ExePermissionsHardeningStatus.Revoked"/>,
        /// <see cref="ExePermissionsHardeningStatus.InUse"/> and <see cref="ExePermissionsHardeningStatus.Skipped"/>.</returns>
        internal static bool ReportRevokeResult(ExePermissionsHardeningResult result)
        {
            var account = result.Account;
            switch (result.Status)
            {
                case ExePermissionsHardeningStatus.Revoked:
                    Logger.Info(result.Revoked.Count == 0
                        ? $"'{account}' held no entry in Servy's vault; nothing to revoke."
                        : $"Revoked the access of '{account}' to Servy's vault: {string.Join(", ", result.Revoked)}.");
                    return true;
                case ExePermissionsHardeningStatus.InUse:
                    Logger.Info($"Kept the access of '{account}' to Servy's vault: {result.Reason}.");
                    return true;
                case ExePermissionsHardeningStatus.Skipped:
                    Logger.Info($"Vault access revocation skipped for '{account}': {result.Reason}.");
                    return true;
                case ExePermissionsHardeningStatus.Failed:
                    Logger.Error($"Revoking the access of '{account}' to Servy's vault failed on: {string.Join(", ", result.Failed)}. " +
                        "Its entries on those items stay until they are removed by hand.");
                    return false;
                default:
                    Logger.Error($"The access of '{account}' to Servy's vault was not revoked: {result.Reason}.");
                    return false;
            }
        }

        /// <summary>
        /// Determines whether the current process runs elevated.
        /// </summary>
        /// <returns><see langword="true"/> when the process token is in Builtin Administrators.</returns>
        protected virtual bool IsProcessElevated() => SecurityHelper.IsAdministrator();

        /// <summary>
        /// Resolves an account name to its SID through LSA, accepting the relative <c>.\user</c> notation.
        /// </summary>
        /// <param name="account">The account name.</param>
        /// <returns>The account's SID, or <see langword="null"/> when it cannot be resolved.</returns>
        protected virtual SecurityIdentifier ResolveAccount(string account) => AccountSidResolver.Resolve(account);

        /// <summary>
        /// Determines whether a SID is a direct member of the local Administrators group.
        /// </summary>
        /// <param name="sid">The account's SID.</param>
        /// <returns>
        /// <see langword="true"/> for a direct member; <see langword="false"/> when every member was read and none is
        /// the account or a group that could contain it; <see langword="null"/> when that cannot be decided (the group
        /// holds nested groups, or its members cannot be read).
        /// </returns>
        protected virtual bool? IsAdministratorsMember(SecurityIdentifier sid)
        {
            // The WinNT API binds the group by name, and the built-in group is often renamed as a hardening measure
            string groupName;
            try
            {
                groupName = AdministratorsSid.Translate(typeof(NTAccount)).Value;
                groupName = groupName.Substring(groupName.LastIndexOf('\\') + 1);
            }
            catch (SystemException)
            {
                groupName = "Administrators";
            }

            var buffer = IntPtr.Zero;
            try
            {
                var rc = NetLocalGroupGetMembers(null, groupName, 1, out buffer, MAX_PREFERRED_LENGTH, out var read, out var total, IntPtr.Zero);
                if (rc != 0)
                {
                    Logger.Debug($"NetLocalGroupGetMembers failed for '{groupName}' with error {rc}.");
                    return null;
                }

                var complete = read == total;
                var size = Marshal.SizeOf(typeof(LOCALGROUP_MEMBERS_INFO_1));
                for (var i = 0; i < read; i++)
                {
                    var member = (LOCALGROUP_MEMBERS_INFO_1)Marshal.PtrToStructure(IntPtr.Add(buffer, i * size), typeof(LOCALGROUP_MEMBERS_INFO_1));
                    if (member.lgrmi1_sid == IntPtr.Zero)
                    {
                        complete = false;
                        continue;
                    }

                    if (new SecurityIdentifier(member.lgrmi1_sid).Equals(sid))
                        return true;

                    // A nested group could contain the account, and it is not expanded
                    if (member.lgrmi1_sidusage == SidTypeGroup || member.lgrmi1_sidusage == SidTypeAlias || member.lgrmi1_sidusage == SidTypeWellKnownGroup)
                        complete = false;
                }

                return complete ? false : (bool?)null;
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                    NetApiBufferFree(buffer);
            }
        }

        /// <summary>
        /// Counts the NTFS hard links of a file.
        /// </summary>
        /// <param name="path">The file.</param>
        /// <returns>The link count, or -1 when it cannot be read.</returns>
        protected virtual int GetHardLinkCount(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    return GetFileInformationByHandle(stream.SafeFileHandle, out var info) ? (int)info.NumberOfLinks : -1;
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Could not read the hard link count of '{path}': {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Determines whether a SID is Everyone, Users or Authenticated Users.
        /// </summary>
        /// <param name="sid">The SID to test.</param>
        /// <returns><see langword="true"/> for one of the broad groups.</returns>
        private static bool IsBroadGroup(SecurityIdentifier sid)
            => SecurityHelper.BroadUnprivilegedSids.Any(broad => broad.Equals(sid));
    }

    /// <summary>
    /// One file the hardening locks down.
    /// </summary>
    internal sealed class ExePermissionsTarget
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExePermissionsTarget"/> class.
        /// </summary>
        /// <param name="relativePath">The file's path relative to the vault.</param>
        /// <param name="rights">The rights the target account keeps on the file.</param>
        /// <param name="optional">Whether a missing file is skipped rather than reported as missing.</param>
        public ExePermissionsTarget(string relativePath, FileSystemRights rights, bool optional = false)
        {
            RelativePath = relativePath;
            Rights = rights;
            Optional = optional;
        }

        /// <summary>Gets the file's path relative to the vault.</summary>
        public string RelativePath { get; }

        /// <summary>Gets the rights the target account keeps on the file.</summary>
        public FileSystemRights Rights { get; }

        /// <summary>Gets whether a missing file is skipped rather than reported as missing.</summary>
        public bool Optional { get; }
    }
}

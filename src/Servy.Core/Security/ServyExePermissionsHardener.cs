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
    /// Hardens Servy's vault for a service account: the account gets Modify on <c>%ProgramData%\Servy</c> (inherited
    /// by every subfolder and file, with Delete denied on the folders), while Servy's binaries are locked down to
    /// Read &amp; Execute, its configuration files and encryption key to Read, and its configuration database to
    /// Read, Write without Delete.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the C# port of the former <c>Set-ServyExePermissions.ps1</c>, so Servy applies the hardening itself
    /// wherever its executables run. It is run when a service is installed under an account other than Local System
    /// (<see cref="Services.ServiceManager"/>), and again for every such account after the desktop app, the Manager or
    /// the CLI has extracted a binary into the vault, because a newly written file inherits the vault's Modify grant.
    /// </para>
    /// <para>
    /// Each hardened file stops inheriting from the vault, is owned by Builtin Administrators, and keeps Full Control
    /// for SYSTEM and Administrators (well-known SIDs, so the result is language-agnostic). Explicit grants for
    /// Users, Authenticated Users and Everyone are purged; explicit entries for other principals are kept, so
    /// hardening one account never removes another's access. The database and the key keep their inherited entries
    /// as explicit ones for the same reason. A file that is a reparse point or has more than one hard link is not
    /// touched and is reported as failed.
    /// </para>
    /// <para>
    /// The process must be elevated, as every Servy process that installs a service already is.
    /// </para>
    /// </remarks>
    public class ServyExePermissionsHardener : IServyExePermissionsHardener
    {
        /// <summary>The file name of the service's settings file in the vault.</summary>
        internal const string ServiceSettingsFileName = "appsettings.service.json";

        /// <summary>The file name of the restarter's settings file in the vault.</summary>
        internal const string RestarterSettingsFileName = "appsettings.restarter.json";

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
        public static bool IsHardeningCandidate(string? account)
            => !string.IsNullOrWhiteSpace(account) && !ServiceAccounts.LocalSystemAliases.Contains(account!.Trim());

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

        /// <summary>
        /// Selects the distinct accounts, other than Local System, that the given services run under.
        /// </summary>
        /// <param name="services">The installed services.</param>
        /// <returns>The trimmed accounts, compared case-insensitively, in first-seen order.</returns>
        internal static List<string> GetServiceAccounts(IEnumerable<ServiceDto> services)
        {
            return (services ?? Enumerable.Empty<ServiceDto>())
                .Where(s => s != null && s.RunAsLocalSystem != true && IsHardeningCandidate(s.UserAccount))
                .Select(s => s.UserAccount!.Trim())
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

            GrantVaultAccess(targetSid, result);

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
        /// <returns>The binaries (Read &amp; Execute), the settings files (Read, optional), the database (Read, Write) and
        /// the encryption key (Read).</returns>
        internal IReadOnlyList<ExePermissionsTarget> GetTargetFiles()
        {
            var targets = new List<ExePermissionsTarget>
            {
                new ExePermissionsTarget(AppConfig.ServyServiceUIExe, FileSystemRights.ReadAndExecute),
                new ExePermissionsTarget(AppConfig.ServyServiceCLIExe, FileSystemRights.ReadAndExecute),
                new ExePermissionsTarget(AppConfig.ServyRestarterExe, FileSystemRights.ReadAndExecute),
            };

            // Harden whichever handle binary is actually present (#6472); when neither is, report the one this
            // machine's architecture extracts.
            var handleX64 = AppConfig.HandleExeX64FileName + ".exe";
            var handleArm64 = AppConfig.HandleExeARM64FileName + ".exe";
            var hasX64 = File.Exists(Path.Combine(VaultDirectory, handleX64));
            var hasArm64 = File.Exists(Path.Combine(VaultDirectory, handleArm64));
            if (hasX64)
                targets.Add(new ExePermissionsTarget(handleX64, FileSystemRights.ReadAndExecute));
            if (hasArm64)
                targets.Add(new ExePermissionsTarget(handleArm64, FileSystemRights.ReadAndExecute));
            if (!hasX64 && !hasArm64)
                targets.Add(new ExePermissionsTarget(RuntimeInformation.OSArchitecture == Architecture.Arm64 ? handleArm64 : handleX64, FileSystemRights.ReadAndExecute));

            AddCommonTargets(targets);
            return targets;
        }

        /// <summary>
        /// Adds the settings files, the database and the encryption key.
        /// </summary>
        /// <param name="targets">The list to add to.</param>
        private static void AddCommonTargets(List<ExePermissionsTarget> targets)
        {
            targets.Add(new ExePermissionsTarget(ServiceSettingsFileName, FileSystemRights.Read, optional: true));
            targets.Add(new ExePermissionsTarget(RestarterSettingsFileName, FileSystemRights.Read, optional: true));

            // The shared configuration database: Modify without Delete (#7136). Its inherited entries are kept as
            // explicit ones, so other service accounts keep the access they already have.
            targets.Add(new ExePermissionsTarget(
                Path.Combine(AppConfig.DbFolderName, AppConfig.DatabaseFileName),
                FileSystemRights.Read | FileSystemRights.Write,
                preserveInherited: true));

            // The encryption key: read-only, so the service account can decrypt but can neither replace nor delete
            // the key every Servy process trusts.
            targets.Add(new ExePermissionsTarget(
                Path.Combine(AppConfig.SecurityFolderName, AppConfig.AESKeyFileName),
                FileSystemRights.Read,
                preserveInherited: true));
        }

        /// <summary>
        /// Grants the target Modify on the vault, inherited by subfolders and files, and denies it Delete on the
        /// vault and its subfolders.
        /// </summary>
        /// <param name="targetSid">The target account.</param>
        /// <param name="result">Receives the vault as failed when the grant cannot be written.</param>
        private void GrantVaultAccess(SecurityIdentifier targetSid, ExePermissionsHardeningResult result)
        {
            try
            {
                var vault = new DirectoryInfo(VaultDirectory);
                if ((vault.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    throw new IOException("The directory is a reparse point (symlink/junction) and cannot be granted access safely.");

                var acl = vault.GetAccessControl(AccessControlSections.Access);
                acl.SetAccessRule(new FileSystemAccessRule(
                    targetSid,
                    FileSystemRights.Modify,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));

                // Folders must not be renamable or deletable by the service account: Delete on a folder plus
                // add-subdirectory on its parent is a rename, which moves every hardened file inside it out of the way (#7140).
                acl.AddAccessRule(new FileSystemAccessRule(
                    targetSid,
                    FileSystemRights.Delete,
                    InheritanceFlags.ContainerInherit,
                    PropagationFlags.None,
                    AccessControlType.Deny));

                vault.SetAccessControl(acl);
                result.VaultAccessGranted = true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to grant '{result.Account}' Modify on '{VaultDirectory}'.", ex);
                result.AddFailed(VaultDirectory);
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

                if (target.PreserveInherited)
                {
                    // Break inheritance and keep the inherited entries as explicit ones. The conversion only takes effect
                    // once committed, so commit and re-read before the target's copy of the inherited Modify is removed.
                    acl.SetAccessRuleProtection(true, true);
                    file.SetAccessControl(acl);
                    acl = file.GetAccessControl();
                }
                else
                {
                    // Break inheritance and drop the inherited entries in one pass
                    acl.SetAccessRuleProtection(true, false);
                }

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
        /// Determines whether the current process runs elevated.
        /// </summary>
        /// <returns><see langword="true"/> when the process token is in Builtin Administrators.</returns>
        protected virtual bool IsProcessElevated() => SecurityHelper.IsAdministrator();

        /// <summary>
        /// Resolves an account name to its SID through LSA, accepting the relative <c>.\user</c> notation.
        /// </summary>
        /// <param name="account">The account name.</param>
        /// <returns>The account's SID, or <see langword="null"/> when it cannot be resolved.</returns>
        protected virtual SecurityIdentifier? ResolveAccount(string account)
        {
            var sid = TryTranslate(account);
            if (sid == null && account.StartsWith(@".\", StringComparison.Ordinal))
                sid = TryTranslate(Environment.MachineName + @"\" + account.Substring(2));

            return sid;
        }

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
                    var member = (LOCALGROUP_MEMBERS_INFO_1)Marshal.PtrToStructure(IntPtr.Add(buffer, i * size), typeof(LOCALGROUP_MEMBERS_INFO_1))!;
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

        /// <summary>
        /// Translates an account name to its SID.
        /// </summary>
        /// <param name="name">The account name.</param>
        /// <returns>The SID, or <see langword="null"/> when the name does not map.</returns>
        private static SecurityIdentifier? TryTranslate(string name)
        {
            try
            {
                return (SecurityIdentifier)new NTAccount(name).Translate(typeof(SecurityIdentifier));
            }
            catch (IdentityNotMappedException)
            {
                return null;
            }
            catch (SystemException)
            {
                return null;
            }
        }
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
        /// <param name="preserveInherited">Whether the file's inherited entries are kept as explicit ones.</param>
        public ExePermissionsTarget(string relativePath, FileSystemRights rights, bool optional = false, bool preserveInherited = false)
        {
            RelativePath = relativePath;
            Rights = rights;
            Optional = optional;
            PreserveInherited = preserveInherited;
        }

        /// <summary>Gets the file's path relative to the vault.</summary>
        public string RelativePath { get; }

        /// <summary>Gets the rights the target account keeps on the file.</summary>
        public FileSystemRights Rights { get; }

        /// <summary>Gets whether a missing file is skipped rather than reported as missing.</summary>
        public bool Optional { get; }

        /// <summary>Gets whether the file's inherited entries are kept as explicit ones.</summary>
        public bool PreserveInherited { get; }
    }
}

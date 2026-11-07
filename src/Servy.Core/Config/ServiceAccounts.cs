using Microsoft.Win32;
using Servy.Core.Logging;
using Servy.Core.Native;
using System;
using System.Collections.Immutable;

namespace Servy.Core.Config
{
    /// <summary>
    /// Contains the internal account names utilized by the Windows Service Control Manager (SCM).
    /// These values represent the raw strings returned by the Win32 Service Control Manager API, which are distinct from the localized display names used in the user interface.
    /// </summary>
    public static class ServiceAccounts
    {
        /// <summary>
        /// The internal SCM name for the highly-privileged LocalSystem account.
        /// </summary>
        public const string LocalSystem = "LocalSystem";

        /// <summary>
        /// The internal SCM name for the NT AUTHORITY\LocalService account, which has minimum privileges on the local computer.
        /// </summary>
        public const string LocalService = @"NT AUTHORITY\LocalService";

        /// <summary>
        /// The internal SCM name for the NT AUTHORITY\NetworkService account, which has minimum privileges on the local computer and acts as the computer on the network.
        /// </summary>
        public const string NetworkService = @"NT AUTHORITY\NetworkService";

        /// <summary>
        /// A collection of standard aliases used to identify the Windows 'LocalSystem' account.
        /// </summary>
        public static readonly ImmutableHashSet<string> LocalSystemAliases = ImmutableHashSet.Create(
            StringComparer.OrdinalIgnoreCase,
            LocalSystem,
            "System",
            "Local System",
            @".\LocalSystem",
            @".\System",
            @".\Local System",
            @"NT AUTHORITY\LocalSystem",
            @"NT AUTHORITY\Local System",
            @"NT AUTHORITY\SYSTEM",
            @"BUILTIN\LocalSystem",
            @"BUILTIN\System"
        );

        /// <summary>
        /// A collection of well-known alias strings representing the 'LocalService' account,
        /// used for normalizing service configuration security identifiers.
        /// </summary>
        public static readonly ImmutableHashSet<string> LocalServiceAliases = ImmutableHashSet.Create(
            StringComparer.OrdinalIgnoreCase,
            "LocalService",
            LocalService,
            @".\LocalService",
            @".\Local Service",
            @"NT AUTHORITY\Local Service",
            "Local Service",
            @"BUILTIN\LocalService"
        );

        /// <summary>
        /// A collection of well-known alias strings representing the 'NetworkService' account,
        /// used for normalizing service configuration security identifiers.
        /// </summary>
        public static readonly ImmutableHashSet<string> NetworkServiceAliases = ImmutableHashSet.Create(
            StringComparer.OrdinalIgnoreCase,
            "NetworkService",
            NetworkService,
            @".\NetworkService",
            @".\Network Service",
            @"NT AUTHORITY\Network Service",
            "Network Service",
            @"BUILTIN\NetworkService"
        );

        /// <summary>
        /// A safelist of built-in Windows accounts that are actual service runners.
        /// Combined from all documented LocalSystem, LocalService, and NetworkService aliases.
        /// </summary>
        public static readonly ImmutableHashSet<string> RunnableServiceAccounts = LocalSystemAliases
            .Union(LocalServiceAliases)
            .Union(NetworkServiceAliases);

        /// <summary>
        /// True when the account is an OS-managed passwordless service identity: a documented
        /// built-in alias, a virtual account (NT SERVICE\...) or an IIS AppPool identity.
        /// These must not be sent through LSA privilege assignment, nor through SID translation, with one
        /// exception: a virtual account resolves to its per-service SID, and <see cref="IsEligibleForServiceControlGrant"/>
        /// admits it to the service control grant, which translates it.
        /// </summary>
        /// <param name="account">The account name to check.</param>
        /// <returns><c>true</c> if the account is a built-in OS-managed service identity; otherwise, <c>false</c>.</returns>
        public static bool IsBuiltInServiceAccount(string account)
        {
            if (string.IsNullOrWhiteSpace(account)) return false;

            // Keep trimming for safety
            account = account.Trim();

            return RunnableServiceAccounts.Contains(account)
                || account.StartsWith(@"NT SERVICE\", StringComparison.OrdinalIgnoreCase)
                || account.StartsWith(@"IIS APPPOOL\", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True when an explicit service control and status grant may be written for the account.
        /// A virtual account (NT SERVICE\...) is eligible: its per-service SID belongs to that one
        /// service, so a grant to it cannot reach any other service. LocalService and NetworkService
        /// are not eligible - they are shared by every service that runs under them, so a grant to
        /// one of them would let all of those services control this one. LocalSystem already holds
        /// full control over its own service. An IIS AppPool identity, which the install paths accept
        /// as a log-on account, is not eligible either: the worker processes of that application pool
        /// run under the same SID, so a grant to it would let them control this service.
        /// </summary>
        /// <param name="account">The account name to check.</param>
        /// <returns>
        /// <c>true</c> if an explicit service control and status grant may be written for the
        /// account; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsEligibleForServiceControlGrant(string account)
        {
            if (string.IsNullOrWhiteSpace(account)) return false;

            // Keep trimming for safety
            account = account.Trim();

            const string virtualAccountPrefix = @"NT SERVICE\";

            if (account.StartsWith(virtualAccountPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // Nothing after the prefix is a paste artefact rather than an account, and has no
                // per-service SID to grant to.
                return account.Length > virtualAccountPrefix.Length;
            }

            return !IsBuiltInServiceAccount(account);
        }

        /// <summary>
        /// True when the account represents a group Managed Service Account (gMSA).
        /// Identified by a trailing '$' and empty or whitespace password context.
        /// </summary>
        /// <param name="account">The account name to check.</param>
        /// <param name="password">The account password string.</param>
        /// <returns><c>true</c> if the account is a gMSA identity; otherwise, <c>false</c>.</returns>
        public static bool IsGmsa(string account, string password)
        {
            if (string.IsNullOrWhiteSpace(account)) return false;

            // Keep trimming for safety
            return string.IsNullOrWhiteSpace(password) && account.Trim().EndsWith("$");
        }

        /// <summary>
        /// True when the account is a passwordless service identity other than LocalSystem: LocalService,
        /// NetworkService, a virtual account (NT SERVICE\...), an IIS AppPool identity or a group Managed
        /// Service Account (a name ending in '$'). Such an account carries no secret, so its name is the
        /// whole identity and can be written to an export file and applied again on import.
        /// </summary>
        /// <remarks>
        /// LocalSystem is excluded because it is the import baseline, expressed by RunAsLocalSystem rather
        /// than by an account name. A bare "NT SERVICE\" or "IIS APPPOOL\" prefix is excluded because it
        /// names no account.
        /// </remarks>
        /// <param name="account">The account name to check.</param>
        /// <returns><c>true</c> if the account needs no password and is not LocalSystem; otherwise, <c>false</c>.</returns>
        public static bool IsPasswordlessAccount(string account)
        {
            if (string.IsNullOrWhiteSpace(account)) return false;

            // Keep trimming for safety
            account = account.Trim();

            if (LocalSystemAliases.Contains(account)) return false;

            if (account.EndsWith(@"\", StringComparison.Ordinal)) return false;

            return IsBuiltInServiceAccount(account) || IsGmsa(account, null);
        }

        /// <summary>
        /// The registry key, under <see cref="Registry.LocalMachine"/>, that holds the LSA policy values.
        /// </summary>
        internal const string LsaSubKey = @"SYSTEM\CurrentControlSet\Control\Lsa";

        /// <summary>
        /// The LSA value behind the security policy "Accounts: Limit local account use of blank passwords to
        /// console logon only". <c>1</c> (the Windows default, also assumed when the value is absent) restricts
        /// a local account with a blank password to console logon, so a service cannot log on with it; <c>0</c>
        /// lifts that restriction for the whole machine.
        /// </summary>
        internal const string LimitBlankPasswordUseValueName = "LimitBlankPasswordUse";

        /// <summary>
        /// Reads <see cref="LimitBlankPasswordUseValueName"/>; <c>null</c> when the value is absent or unreadable.
        /// A seam for tests, which must not depend on the policy of the machine they run on.
        /// </summary>
        internal static Func<int?> LimitBlankPasswordUseReader = ReadLimitBlankPasswordUse;

        /// <summary>
        /// Validates an account and password, throwing when the logon would fail. A seam for tests, which
        /// cannot create a local account with a blank password on the machine they run on.
        /// </summary>
        internal static Action<string, string> BlankPasswordValidator = NativeMethodsHelpers.ValidateCredentials;

        /// <summary>
        /// True when the account names a user on this machine (".\name" or "MACHINE\name"), as opposed to a
        /// domain account, a built-in or virtual service identity, or a gMSA. Such an account can have a blank
        /// password, which only <see cref="IsBlankPasswordLocalAccount"/> can tell.
        /// </summary>
        /// <param name="account">The account name to check.</param>
        /// <returns><c>true</c> if the account is a local user account; otherwise, <c>false</c>.</returns>
        public static bool IsLocalAccount(string account)
        {
            if (string.IsNullOrWhiteSpace(account)) return false;

            // Keep trimming for safety
            account = account.Trim();

            if (IsBuiltInServiceAccount(account) || account.EndsWith("$", StringComparison.Ordinal)) return false;

            var separator = account.IndexOf('\\');
            if (separator <= 0 || separator != account.LastIndexOf('\\') || separator == account.Length - 1) return false;

            var machine = account.Substring(0, separator);
            return machine == "." || string.Equals(machine, Environment.MachineName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True when the account is a local user account whose password is blank and that Windows lets a
        /// service log on with: <see cref="LimitBlankPasswordUseValueName"/> is <c>0</c> on this machine, and
        /// validating the account with an empty password succeeds. Such an account is then treated like the
        /// passwordless accounts of <see cref="IsPasswordlessAccount"/>.
        /// </summary>
        /// <remarks>
        /// This performs a real logon attempt, so it is called once per import of such an account and never
        /// from a serialization path. Setting <see cref="LimitBlankPasswordUseValueName"/> to <c>0</c> is not
        /// recommended: it lets every local account with a blank password log on over the network.
        /// </remarks>
        /// <param name="account">The account name to check.</param>
        /// <returns><c>true</c> if the account is a local user that can log on with a blank password; otherwise, <c>false</c>.</returns>
        public static bool IsBlankPasswordLocalAccount(string account)
        {
            if (!IsLocalAccount(account)) return false;

            if (LimitBlankPasswordUseReader() != 0) return false;

            try
            {
                BlankPasswordValidator(account.Trim(), string.Empty);
                return true;
            }
            catch (Exception ex)
            {
                // A failed blank-password logon is the normal answer for an account that has a password,
                // not an error: the account is simply not passwordless, and the import resets it.
                Logger.Debug($"Account '{account.Trim()}' cannot log on with a blank password: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// True when the account may be passwordless without contacting the system: a passwordless account of
        /// <see cref="IsPasswordlessAccount"/>, or a local user account, which is passwordless when it was
        /// configured with a blank password. Used where no logon may be attempted - an export, which only
        /// writes a local account when no password is stored for it, and a re-import, whose account was
        /// already checked by <see cref="IsBlankPasswordLocalAccount"/>.
        /// </summary>
        /// <param name="account">The account name to check.</param>
        /// <returns><c>true</c> if the account is passwordless or a local user account; otherwise, <c>false</c>.</returns>
        public static bool CanBePasswordless(string account) => IsPasswordlessAccount(account) || IsLocalAccount(account);

        /// <summary>
        /// Reads <see cref="LimitBlankPasswordUseValueName"/> from the registry.
        /// </summary>
        /// <returns>The value, or <c>null</c> when it is absent, not a DWORD, or cannot be read.</returns>
        private static int? ReadLimitBlankPasswordUse()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(LsaSubKey))
                {
                    return key?.GetValue(LimitBlankPasswordUseValueName) as int?;
                }
            }
            catch (Exception ex)
            {
                // Unreadable is treated as the Windows default (restricted): no blank-password account is kept.
                Logger.Debug($"Could not read {LsaSubKey}\\{LimitBlankPasswordUseValueName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Expands the ".\" local-machine shorthand to "MACHINE\", leaving every other form untouched.
        /// This is the single definition of the rule: the credential validation gate and the
        /// "Log on as a service" grant path are two halves of one install and have to agree on
        /// which name forms mean "an account on this machine".
        /// </summary>
        /// <param name="account">The account name to expand.</param>
        /// <returns>
        /// The account name with a leading ".\" replaced by the local machine name, or the input
        /// unchanged when it carries no such prefix.
        /// </returns>
        public static string ExpandLocalShorthand(string account) =>
            account.StartsWith(@".\", StringComparison.Ordinal)
                ? Environment.MachineName + account.Substring(1)
                : account;
    }
}

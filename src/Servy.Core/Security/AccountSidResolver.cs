using Servy.Core.Config;
using System;
using System.Security.Principal;

namespace Servy.Core.Security
{
    /// <summary>
    /// Resolves service log-on account names to their security identifiers.
    /// </summary>
    public static class AccountSidResolver
    {
        /// <summary>
        /// Resolves an account name to its SID through LSA, accepting the relative <c>.\user</c> notation.
        /// </summary>
        /// <param name="account">The account name.</param>
        /// <returns>The account's SID, or <see langword="null"/> when it is blank or cannot be resolved.</returns>
        public static SecurityIdentifier Resolve(string account)
        {
            if (string.IsNullOrWhiteSpace(account))
                return null;

            var trimmed = account.Trim();

            // The built-in service accounts have fixed SIDs. Their SCM spellings (NT AUTHORITY\NetworkService, .\LocalService,
            // BUILTIN\NetworkService, ...) are not all names LSA knows, so never send them through a name lookup.
            var wellKnown = TryGetBuiltInServiceAccountSid(trimmed);
            if (wellKnown != null)
                return wellKnown;

            var sid = TryTranslate(trimmed);
            if (sid == null && trimmed.StartsWith(@".\", StringComparison.Ordinal))
                sid = TryTranslate(Environment.MachineName + @"\" + trimmed.Substring(2));

            return sid;
        }

        /// <summary>
        /// Maps every documented spelling of Local System, Local Service and Network Service to its well-known SID.
        /// </summary>
        /// <param name="account">The trimmed account name.</param>
        /// <returns>The well-known SID, or <see langword="null"/> when the name is not one of those spellings.</returns>
        private static SecurityIdentifier TryGetBuiltInServiceAccountSid(string account)
        {
            if (ServiceAccounts.LocalSystemAliases.Contains(account))
                return new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            if (ServiceAccounts.LocalServiceAliases.Contains(account))
                return new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
            if (ServiceAccounts.NetworkServiceAliases.Contains(account))
                return new SecurityIdentifier(WellKnownSidType.NetworkServiceSid, null);
            return null;
        }

        /// <summary>
        /// Translates an account name to its SID.
        /// </summary>
        /// <param name="name">The account name.</param>
        /// <returns>The SID, or <see langword="null"/> when the name does not map.</returns>
        private static SecurityIdentifier TryTranslate(string name)
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
}

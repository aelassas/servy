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
        public static SecurityIdentifier? Resolve(string? account)
        {
            if (string.IsNullOrWhiteSpace(account))
                return null;

            var trimmed = account!.Trim();
            var sid = TryTranslate(trimmed);
            if (sid == null && trimmed.StartsWith(@".\", StringComparison.Ordinal))
                sid = TryTranslate(Environment.MachineName + @"\" + trimmed.Substring(2));

            return sid;
        }

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
}

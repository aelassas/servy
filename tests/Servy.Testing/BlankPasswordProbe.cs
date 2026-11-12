using Servy.Core.Config;
using System.Collections.Generic;
using System.Linq;

namespace Servy.Testing
{
    /// <summary>
    /// Replaces the blank-password probe of <see cref="ServiceAccounts"/> for the duration of a test and
    /// restores it on disposal, so a test can say what the LimitBlankPasswordUse policy is and which local
    /// accounts log on with a blank password, instead of depending on the machine it runs on.
    /// </summary>
    /// <remarks>
    /// The probe is process-wide. Use it from ONE test class per test assembly, whose tests xUnit runs one at
    /// a time; a concurrent test in another class that names a local account sees a validator that refuses
    /// every account but the ones listed here, which is what the real machine does for an account with a
    /// password. Accounts given here should therefore be names no other test uses.
    /// </remarks>
    public sealed class BlankPasswordProbe : IDisposable
    {
        private const string ReaderField = "LimitBlankPasswordUseReader";
        private const string ValidatorField = "BlankPasswordValidator";

        private readonly object _originalReader;
        private readonly object _originalValidator;
        private readonly List<string> _validated = new List<string>();

        /// <summary>
        /// Installs the probe.
        /// </summary>
        /// <param name="limitBlankPasswordUse">The policy value the probe reports; <c>null</c> for an absent value.</param>
        /// <param name="blankPasswordAccounts">The local accounts whose blank-password logon succeeds.</param>
        public BlankPasswordProbe(int? limitBlankPasswordUse, params string[] blankPasswordAccounts)
        {
            _originalReader = TestReflection.GetFieldStatic<object>(typeof(ServiceAccounts), ReaderField);
            _originalValidator = TestReflection.GetFieldStatic<object>(typeof(ServiceAccounts), ValidatorField);

            var accepted = new HashSet<string>(blankPasswordAccounts, StringComparer.OrdinalIgnoreCase);
            Func<int?> reader = () => limitBlankPasswordUse;
            Action<string, string> validator = (account, password) =>
            {
                lock (_validated) _validated.Add(account);
                if (password.Length != 0 || !accepted.Contains(account))
                    throw new UnauthorizedAccessException("Invalid username or password.");
            };

            TestReflection.SetFieldStatic(typeof(ServiceAccounts), ReaderField, reader);
            TestReflection.SetFieldStatic(typeof(ServiceAccounts), ValidatorField, validator);
        }

        /// <summary>
        /// Whether a blank-password logon was attempted for <paramref name="account"/> while the probe was installed.
        /// </summary>
        /// <param name="account">The account name, compared case-insensitively.</param>
        /// <returns><c>true</c> if the validator was called for the account; otherwise, <c>false</c>.</returns>
        public bool WasValidated(string account)
        {
            lock (_validated) return _validated.Any(a => string.Equals(a, account, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Restores the production probe.
        /// </summary>
        public void Dispose()
        {
            TestReflection.SetFieldStatic(typeof(ServiceAccounts), ReaderField, _originalReader);
            TestReflection.SetFieldStatic(typeof(ServiceAccounts), ValidatorField, _originalValidator);
        }
    }
}

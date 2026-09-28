using System.Collections.Generic;

namespace Servy.Core.Security
{
    /// <summary>
    /// The overall outcome of hardening Servy's files for one account.
    /// </summary>
    public enum ExePermissionsHardeningStatus
    {
        /// <summary>The vault access was granted and every file was hardened.</summary>
        Hardened,

        /// <summary>Every file that exists was hardened; some required files are not present yet.</summary>
        Incomplete,

        /// <summary>The vault access grant or at least one present file could not be hardened.</summary>
        Failed,

        /// <summary>
        /// Nothing was changed: the account is Local System, the Administrators group or a member of it,
        /// all of which keep Full Control and are outside the boundary the hardening establishes.
        /// </summary>
        Skipped,

        /// <summary>Nothing was changed: the process is not elevated, so it cannot rewrite the ACLs.</summary>
        NotElevated,

        /// <summary>Nothing was changed: the vault directory does not exist.</summary>
        VaultNotFound,

        /// <summary>Nothing was changed: the account cannot be resolved, or is a broad group such as Everyone.</summary>
        InvalidAccount,
    }

    /// <summary>
    /// The outcome of hardening Servy's files for one account, with the files in each state.
    /// </summary>
    public sealed class ExePermissionsHardeningResult
    {
        private readonly List<string> _hardened = new List<string>();
        private readonly List<string> _missing = new List<string>();
        private readonly List<string> _failed = new List<string>();
        private readonly List<string> _skipped = new List<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ExePermissionsHardeningResult"/> class.
        /// </summary>
        /// <param name="account">The account the hardening was run for.</param>
        public ExePermissionsHardeningResult(string account)
        {
            Account = account;
        }

        /// <summary>Gets the account the hardening was run for.</summary>
        public string Account { get; }

        /// <summary>Gets the overall outcome.</summary>
        public ExePermissionsHardeningStatus Status { get; private set; }

        /// <summary>Gets a human-readable reason for a status that changed nothing, or <see langword="null"/>.</summary>
        public string Reason { get; private set; }

        /// <summary>Gets whether the account was granted Modify on the vault directory.</summary>
        public bool VaultAccessGranted { get; internal set; }

        /// <summary>Gets the files (relative to the vault) that were hardened.</summary>
        public IReadOnlyList<string> Hardened => _hardened;

        /// <summary>Gets the required files (relative to the vault) that are not present.</summary>
        public IReadOnlyList<string> Missing => _missing;

        /// <summary>Gets the files (relative to the vault), or the vault itself, that could not be hardened.</summary>
        public IReadOnlyList<string> Failed => _failed;

        /// <summary>Gets the optional files (relative to the vault) that are not present and were skipped.</summary>
        public IReadOnlyList<string> Skipped => _skipped;

        internal void AddHardened(string name) => _hardened.Add(name);

        internal void AddMissing(string name) => _missing.Add(name);

        internal void AddFailed(string name) => _failed.Add(name);

        internal void AddSkipped(string name) => _skipped.Add(name);

        /// <summary>
        /// Sets the final status and returns this instance.
        /// </summary>
        /// <param name="status">The overall outcome.</param>
        /// <param name="reason">Why nothing was changed, for the statuses that change nothing.</param>
        /// <returns>This instance.</returns>
        internal ExePermissionsHardeningResult Complete(ExePermissionsHardeningStatus status, string reason = null)
        {
            Status = status;
            Reason = reason;
            return this;
        }
    }
}

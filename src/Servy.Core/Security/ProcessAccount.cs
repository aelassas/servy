using Servy.Core.Native;
using System.Diagnostics.CodeAnalysis;
using System.Security.Principal;

namespace Servy.Core.Security
{
    /// <summary>
    /// Reads the account a running process runs under.
    /// </summary>
    public static class ProcessAccount
    {
        private const uint TokenQuery = 0x0008;

        /// <summary>
        /// Gets the SID of the user of a process's token.
        /// </summary>
        /// <param name="processId">The process identifier.</param>
        /// <returns>The SID, or <see langword="null"/> when the process is gone or its token cannot be read.</returns>
        [ExcludeFromCodeCoverage]
        public static SecurityIdentifier? TryGetUser(int processId)
        {
            if (processId <= 0)
                return null;

            try
            {
                using (var process = NativeMethods.OpenProcess(NativeMethods.ProcessAccess.QueryLimitedInformation, false, processId))
                {
                    if (process == null || process.IsInvalid)
                        return null;

                    if (!NativeMethods.OpenProcessToken(process, TokenQuery, out var token))
                        return null;

                    using (token)
                    using (var identity = new WindowsIdentity(token.DangerousGetHandle()))
                    {
                        return identity.User;
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

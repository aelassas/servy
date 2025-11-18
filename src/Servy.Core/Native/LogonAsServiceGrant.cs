using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Servy.Core.Native
{
    /// <summary>
    /// Provides methods to ensure that a given account has the "Log on as a service" privilege.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class LogonAsServiceGrant
    {
        private const string SE_SERVICE_LOGON_NAME = "SeServiceLogonRight";

        /// <summary>
        /// Ensures the specified account has the "Log on as a service" right.
        /// </summary>
        /// <param name="accountName">
        /// The account to grant the right to. Can be a domain account (DOMAIN\user),
        /// or a local account (.\user or MACHINE_NAME\user).
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the account cannot be resolved to a SID.
        /// </exception>
        public static void Ensure(string accountName)
        {
            var sid = AccountToSid(accountName);
            if (sid == null)
                throw new InvalidOperationException("Cannot resolve SID for: " + accountName);

            if (!HasLogonAsService(sid))
                GrantLogonAsService(sid);
        }

        /// <summary>
        /// Resolves an NT account name to a <see cref="SecurityIdentifier"/>.
        /// Replaces ".\" with the local machine name automatically.
        /// </summary>
        /// <param name="account">Account name to resolve.</param>
        /// <returns>The <see cref="SecurityIdentifier"/> of the account, or null if it cannot be resolved.</returns>
        private static SecurityIdentifier? AccountToSid(string account)
        {
            try
            {
                // Replace ".\" with machine name for local accounts
                if (account.StartsWith(@".\"))
                {
                    string machine = Environment.MachineName;
                    account = machine + @"\" + account.Substring(2);
                }

                var nt = new NTAccount(account);
                return (SecurityIdentifier)nt.Translate(typeof(SecurityIdentifier));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Checks whether the specified account already has the "Log on as a service" right.
        /// </summary>
        /// <param name="sid">The security identifier of the account.</param>
        /// <returns>True if the account has the right; otherwise false.</returns>
        private static bool HasLogonAsService(SecurityIdentifier sid)
        {
            IntPtr sidPtr = IntPtr.Zero;
            IntPtr policy = IntPtr.Zero;
            IntPtr rightsPtr = IntPtr.Zero;

            try
            {
                var oa = new LsaObjectAttributes();
                int status = LsaOpenPolicy(IntPtr.Zero, ref oa, PolicyAccess.POLICY_LOOKUP_NAMES, out policy);
                if (status != 0)
                    throw new InvalidOperationException($"LsaOpenPolicy failed: 0x{status:X}");

                uint rightsCount = 0;

                byte[] sidBytes = sid.GetBinaryForm();

                // Allocate unmanaged memory for SID
                sidPtr = Marshal.AllocHGlobal(sidBytes.Length);
                Marshal.Copy(sidBytes, 0, sidPtr, sidBytes.Length);

                status = LsaEnumerateAccountRights(policy, sidPtr, out rightsPtr, out rightsCount);

                // STATUS_OBJECT_NAME_NOT_FOUND → the account has *no* rights at all
                if (status == unchecked((int)0xC0000034))
                    return false;

                if (status != 0)
                    throw new InvalidOperationException($"LsaEnumerateAccountRights failed: 0x{status:X}");

                int structSize = Marshal.SizeOf(typeof(LsaUnicodeString));
                for (int i = 0; i < rightsCount; i++)
                {
                    IntPtr itemPtr = IntPtr.Add(rightsPtr, i * structSize);
                    var lus = Marshal.PtrToStructure<LsaUnicodeString>(itemPtr);

                    if (lus.Buffer == IntPtr.Zero || lus.Length == 0)
                        continue;

                    string right = Marshal.PtrToStringUni(lus.Buffer, lus.Length / 2)!;
                    if (string.Equals(right, SE_SERVICE_LOGON_NAME, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }
            finally
            {
                if (sidPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(sidPtr);
                if (rightsPtr != IntPtr.Zero)
                    LsaFreeMemory(rightsPtr);
                if (policy != IntPtr.Zero)
                    LsaClose(policy);
            }
        }

        [DllImport("advapi32.dll")]
        private static extern int LsaFreeMemory(IntPtr buffer);

        /// <summary>
        /// Grants the "Log on as a service" right to the specified account SID.
        /// </summary>
        /// <param name="sid">The security identifier of the account.</param>
        private static void GrantLogonAsService(SecurityIdentifier sid)
        {
            IntPtr sidPtr = IntPtr.Zero;
            IntPtr policy = IntPtr.Zero;
            IntPtr buffer = IntPtr.Zero;

            try
            {
                var oa = new LsaObjectAttributes();
                int status = LsaOpenPolicy(IntPtr.Zero, ref oa, PolicyAccess.POLICY_ALL_ACCESS, out policy);
                if (status != 0)
                    throw new InvalidOperationException($"LsaOpenPolicy failed: 0x{status:X}");

                buffer = Marshal.StringToHGlobalUni(SE_SERVICE_LOGON_NAME);
                var lus = new LsaUnicodeString
                {
                    Length = (ushort)(SE_SERVICE_LOGON_NAME.Length * 2),
                    MaximumLength = (ushort)((SE_SERVICE_LOGON_NAME.Length * 2) + 2),
                    Buffer = buffer
                };
                var rights = new[] { lus };

                byte[] sidBytes = sid.GetBinaryForm();

                // Allocate unmanaged memory for SID
                sidPtr = Marshal.AllocHGlobal(sidBytes.Length);
                Marshal.Copy(sidBytes, 0, sidPtr, sidBytes.Length);

                status = LsaAddAccountRights(policy, sidPtr, rights, 1);
                if (status != 0)
                    throw new InvalidOperationException($"LsaAddAccountRights failed: 0x{status:X}");
            }
            finally
            {
                if (sidPtr != IntPtr.Zero) Marshal.FreeHGlobal(sidPtr);
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                if (policy != IntPtr.Zero) LsaClose(policy);
            }
        }

        /// <summary>
        /// Returns the binary form of a SecurityIdentifier.
        /// </summary>
        private static byte[] GetBinaryForm(this SecurityIdentifier sid)
        {
            var bytes = new byte[sid.BinaryLength];
            sid.GetBinaryForm(bytes, 0);
            return bytes;
        }

        #region Interop

        [StructLayout(LayoutKind.Sequential)]
        private struct LsaUnicodeString
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;

            public LsaUnicodeString(string s)
            {
                Length = (ushort)(s.Length * 2);
                MaximumLength = (ushort)(Length + 2);
                Buffer = Marshal.StringToHGlobalUni(s);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LsaObjectAttributes
        {
            public int Length;
            public IntPtr RootDir;
            public IntPtr ObjectName;
            public uint Attributes;
            public IntPtr SecurityDesc;
            public IntPtr SecurityQos;
        }

        private static class PolicyAccess
        {
            public const uint POLICY_LOOKUP_NAMES = 0x00000800;
            public const uint POLICY_ALL_ACCESS = 0x00F0FFF;
        }

        [DllImport("advapi32.dll")]
        private static extern int LsaOpenPolicy(
            IntPtr systemName,
            ref LsaObjectAttributes objectAttributes,
            uint desiredAccess,
            out IntPtr policyHandle);

        [DllImport("advapi32.dll")]
        private static extern int LsaAddAccountRights(
            IntPtr policyHandle,
            IntPtr accountSid,
            LsaUnicodeString[] userRights,
            int count);

        [DllImport("advapi32.dll")]
        private static extern int LsaEnumerateAccountRights(
            IntPtr policyHandle,
            IntPtr accountSid,
            out IntPtr userRights,
            out uint countOfRights);

        [DllImport("advapi32.dll")]
        private static extern int LsaClose(IntPtr policyHandle);

        #endregion
    }
}

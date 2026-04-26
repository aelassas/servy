using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Principal;
using static Servy.Core.Native.NativeMethods;

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
            var sid = AccountToSidOrThrow(accountName);
            if (!HasLogonAsService(sid)) GrantLogonAsService(sid);
        }

        /// <summary>
        /// Translates a Windows account name into its corresponding <see cref="SecurityIdentifier"/>.
        /// </summary>
        /// <param name="account">The account name to resolve. Handles the ".\" shorthand for local machine accounts.</param>
        /// <returns>A <see cref="SecurityIdentifier"/> representing the specified account.</returns>
        /// <exception cref="ArgumentException">Thrown if the account name is null or whitespace.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the account cannot be resolved to a SID, often due to a non-existent account 
        /// or an unreachable Domain Controller.
        /// </exception>
        private static SecurityIdentifier AccountToSidOrThrow(string account)
        {
            if (string.IsNullOrWhiteSpace(account))
                throw new ArgumentException("Account name cannot be empty.", nameof(account));

            // Clean up potential copy-paste whitespace
            account = account.Trim();

            // Replace ".\" with machine name for local accounts
            if (account.StartsWith(@".\", StringComparison.OrdinalIgnoreCase))
            {
                string machine = Environment.MachineName;
                account = $"{machine}\\{account.Substring(2)}";
            }

            try
            {
                return (SecurityIdentifier)new NTAccount(account).Translate(typeof(SecurityIdentifier));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Cannot resolve SID for '{account}'. The account may not exist, be misspelled, or the domain controller may be unreachable.",
                    ex);
            }
        }

        /// <summary>
        /// Retrieves the system error message corresponding to the specified NTSTATUS code.
        /// </summary>
        /// <remarks>This method converts an NTSTATUS code to a Win32 error code before retrieving the
        /// error message. The returned message is localized based on the current system culture.</remarks>
        /// <param name="status">The NTSTATUS code for which to obtain the associated Win32 error message.</param>
        /// <returns>A string containing the system-provided error message for the specified status code. If the code does not
        /// correspond to a known error, a generic message is returned.</returns>
        private static string GetWin32ErrorMessage(int status)
        {
            int win = LsaNtStatusToWinError(status);
            string msg = new Win32Exception(win).Message;
            return msg;
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
                var oa = new LsaObjectAttributes
                {
                    Length = Marshal.SizeOf<LsaObjectAttributes>()
                };
                int status = LsaOpenPolicy(IntPtr.Zero, ref oa, PolicyAccess.POLICY_LOOKUP_NAMES, out policy);
                if (status != 0)
                {
                    var msg = GetWin32ErrorMessage(status);
                    throw new InvalidOperationException($"LsaOpenPolicy failed: {msg} (NTSTATUS 0x{status:X})");
                }

                uint rightsCount = 0;

                byte[] sidBytes = sid.GetBinaryForm();

                // Allocate unmanaged memory for SID
                sidPtr = Marshal.AllocHGlobal(sidBytes.Length);
                Marshal.Copy(sidBytes, 0, sidPtr, sidBytes.Length);

                status = LsaEnumerateAccountRights(policy, sidPtr, out rightsPtr, out rightsCount);

                // STATUS_OBJECT_NAME_NOT_FOUND -> the account has *no* rights at all
                if (status == unchecked((int)0xC0000034))
                {
                    return false;
                }

                if (status != 0)
                {
                    var msg = GetWin32ErrorMessage(status);
                    throw new InvalidOperationException($"LsaEnumerateAccountRights failed: {msg} (NTSTATUS 0x{status:X})");
                }

                int structSize = Marshal.SizeOf<LsaUnicodeString>();
                for (int i = 0; i < rightsCount; i++)
                {
                    IntPtr itemPtr = IntPtr.Add(rightsPtr, i * structSize);
                    var lus = Marshal.PtrToStructure<LsaUnicodeString>(itemPtr);

                    if (lus.Buffer == IntPtr.Zero || lus.Length == 0)
                    {
                        continue;
                    }

                    string right = Marshal.PtrToStringUni(lus.Buffer, lus.Length / 2);
                    if (string.Equals(right, SE_SERVICE_LOGON_NAME, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                if (sidPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(sidPtr);
                }
                if (rightsPtr != IntPtr.Zero)
                {
                    LsaFreeMemory(rightsPtr);
                }
                if (policy != IntPtr.Zero)
                {
                    LsaClose(policy);
                }
            }
        }

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
                var oa = new LsaObjectAttributes
                {
                    Length = Marshal.SizeOf<LsaObjectAttributes>()
                };

                // Request only the minimal rights required to add account privileges.
                // POLICY_LOOKUP_NAMES:   To resolve SIDs/Names.
                // POLICY_CREATE_ACCOUNT: To create the account entry in LSA if it doesn't exist.
                // POLICY_ASSIGN_PRIVILEGE: Required specifically by LsaAddAccountRights.
                uint accessMask = PolicyAccess.POLICY_LOOKUP_NAMES |
                                  PolicyAccess.POLICY_CREATE_ACCOUNT |
                                  PolicyAccess.POLICY_ASSIGN_PRIVILEGE;

                int status = LsaOpenPolicy(IntPtr.Zero, ref oa, accessMask, out policy);

                if (status != 0)
                {
                    var msg = GetWin32ErrorMessage(status);
                    throw new InvalidOperationException($"LsaOpenPolicy failed: {msg} (NTSTATUS 0x{status:X})");
                }

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
                {
                    var msg = GetWin32ErrorMessage(status);
                    throw new InvalidOperationException($"LsaAddAccountRights failed: {msg} (NTSTATUS 0x{status:X})");
                }
            }
            finally
            {
                if (sidPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(sidPtr);
                }
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }
                if (policy != IntPtr.Zero)
                {
                    LsaClose(policy);
                }
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

    }
}

using Servy.Core.Config;
using Servy.Core.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.RegularExpressions;
using static Servy.Core.Native.NativeMethods;

namespace Servy.Core.Native
{
    /// <summary>
    /// Native Helper methods.
    /// </summary>
    public class NativeMethodsHelpers
    {
        #region Helper Methods

        /// <summary>
        /// A safelist of built-in Windows accounts that are actual service runners.
        /// These do not have passwords and cannot be validated via LogonUser.
        /// </summary>
        private static readonly HashSet<string> RunnableServiceAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // --- Core Service Identities ---
            "System", "LocalSystem", "LocalService", "NetworkService",

            // --- NT AUTHORITY Prefixed ---
            "NT AUTHORITY\\System", "NT AUTHORITY\\LocalSystem", "NT AUTHORITY\\Local System",
            "NT AUTHORITY\\LocalService", "NT AUTHORITY\\Local Service",
            "NT AUTHORITY\\NetworkService", "NT AUTHORITY\\Network Service",

            // --- Dot / Local Prefixed ---
            ".\\System", ".\\LocalSystem", ".\\Local Service", ".\\LocalService",
            ".\\Network Service", ".\\NetworkService",

            // --- BUILTIN Prefixed ---
            "BUILTIN\\System", "BUILTIN\\LocalSystem", "BUILTIN\\LocalService", "BUILTIN\\NetworkService"
        };

        /// <summary>
        /// Identifies well-known Windows groups or logon contexts that are NOT 
        /// valid runnable service accounts, despite being passwordless.
        /// </summary>
        private static readonly HashSet<string> ForbiddenGroupIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Everyone", "Authenticated Users", "Anonymous Logon", "IUSR",
            "NT AUTHORITY\\Everyone", "NT AUTHORITY\\Authenticated Users", "NT AUTHORITY\\Anonymous Logon", "NT AUTHORITY\\IUSR",
            "Batch", "NT AUTHORITY\\Batch",
            "Interactive", "NT AUTHORITY\\Interactive",
            "Service", "NT AUTHORITY\\Service",
            "Network", "NT AUTHORITY\\Network"
        };

        /// <summary>
        /// Validates Windows credentials by resolving the identity and attempting a network logon.
        /// Handles domain accounts, local accounts, gMSAs, and built-in service identities.
        /// </summary>
        /// <param name="username">The account name (e.g., DOMAIN\User, .\User, or NT AUTHORITY\NetworkService).</param>
        /// <param name="password">The account password. Must be null or empty for built-in accounts; optional for gMSAs.</param>
        /// <exception cref="ArgumentException">Invalid username format, or a password was provided for a passwordless built-in account.</exception>
        /// <exception cref="SecurityException">Identity cannot be resolved or translation failed.</exception>
        /// <exception cref="UnauthorizedAccessException">Invalid credentials or policy restriction.</exception>
        /// <exception cref="Win32Exception">Unexpected system error during logon.</exception>
        public static void ValidateCredentials(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty.");

            username = username.Trim();

            // 0. FORBIDDEN GROUP CHECK
            // Prevent groups or session contexts from passing as valid service runners.
            if (ForbiddenGroupIdentities.Contains(username))
            {
                throw new ArgumentException($"The identity '{username}' is a group or logon context, not a runnable service account. Please use a specific service account (e.g., NetworkService) or a standard user.");
            }

            // The pattern allows for 'NT AUTHORITY\Account', 'DOMAIN\Account', or '.\Account'
            const string pattern = @"^(?:[\w\s\.\-]+|\.)\\[\w\s\.@!\-]+\$?$";
            var isGmsa = string.IsNullOrEmpty(password) && username.EndsWith("$");

            // LOGIC: 
            // 1. Check the static exhaustive list (Case-Insensitive via HashSet comparer).
            // 2. Catch Virtual Service Accounts (NT SERVICE\...)
            // 3. Catch IIS AppPool Identities (IIS APPPOOL\...)
            var isVirtualAccount = username.StartsWith("NT SERVICE\\", StringComparison.OrdinalIgnoreCase)
                                   || username.StartsWith("IIS APPPOOL\\", StringComparison.OrdinalIgnoreCase);
            var isBuiltIn = RunnableServiceAccounts.Contains(username) || isVirtualAccount;

            // Logon Validation Guard for Built-in Accounts
            // These accounts (NetworkService, Virtual Accounts, etc.) are managed by the OS.
            // We short-circuit here to prevent Identity Resolution (Translate) from failing on 
            // dot-prefixed formats like '.\NetworkService' which do not exist in the local SAM.
            if (isBuiltIn)
            {
                if (!string.IsNullOrEmpty(password))
                {
                    throw new ArgumentException($"A password cannot be provided for the built-in passwordless identity '{username}'.");
                }
                return;
            }

            // Skip regex validation for known built-in identities to avoid false negatives 
            // on specialized formats.
            const string invalidMsg = "Username format is invalid. Expected .\\Username, DOMAIN\\Username, or NT AUTHORITY\\ServiceAccount.";
            if (!Regex.IsMatch(username, pattern, RegexOptions.IgnoreCase, AppConfig.InputRegexTimeout))
            {
                throw new ArgumentException(invalidMsg);
            }

            string domain = null;
            string user = username;

            var parts = username.Split('\\');
            domain = parts[0]?.Trim();
            user = parts[1]?.Trim();

            if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(user?.TrimEnd('$')))
            {
                throw new ArgumentException(invalidMsg);
            }

            // 1. Identity Resolution
            try
            {
                string translationName = username;
                if (username.StartsWith(".\\", StringComparison.OrdinalIgnoreCase))
                {
                    translationName = Environment.MachineName + username.Substring(1);
                }
                var ntAccount = new NTAccount(translationName);
                _ = ntAccount.Translate(typeof(SecurityIdentifier));
            }
            catch (IdentityNotMappedException)
            {
                throw new SecurityException($"The account '{username}' could not be resolved. Please verify the username.");
            }
            catch (Exception ex)
            {
                throw new SecurityException($"An error occurred while resolving the account '{username}': {ex.Message}", ex);
            }

            // gMSAs are managed by Active Directory and bypass standard local LogonUser validation.
            if (isGmsa)
            {
                return;
            }

            // 2. Password Validation for standard accounts
            var token = IntPtr.Zero;
            try
            {
                var success = LogonUser(
                    user,
                    domain,
                    password,
                    LOGON32_LOGON_NETWORK,
                    LOGON32_PROVIDER_DEFAULT,
                    out token
                );

                if (!success)
                {
                    var error = Marshal.GetLastWin32Error();

                    switch (error)
                    {
                        case 1326: // ERROR_LOGON_FAILURE
                            throw new UnauthorizedAccessException("Invalid username or password.");
                        case 1327: // ERROR_ACCOUNT_RESTRICTION
                            throw new UnauthorizedAccessException("Account restrictions prevent logon (e.g., blank password use is restricted).");
                        case 1385: // ERROR_LOGON_TYPE_NOT_GRANTED
                            // Network logon denied by policy — retry with SERVICE logon type,
                            // which is the logon type the SCM will actually use.
                            if (LogonUser(user, domain, password, LOGON32_LOGON_SERVICE, LOGON32_PROVIDER_DEFAULT, out token))
                            {
                                return; // valid
                            }
                            error = Marshal.GetLastWin32Error();
                            if (error == 1326) throw new UnauthorizedAccessException("Invalid username or password.");
                            throw new Win32Exception(error,
                                $"Service logon failed. Ensure the account has 'Log on as a service' (granted automatically by Servy) and is not denied service logon.");
                        default:
                            throw new Win32Exception(error, $"Logon failed with error code {error}.");
                    }
                }
            }
            finally
            {
                if (token != IntPtr.Zero)
                {
                    CloseHandle(token);
                }
            }
        }

        /// <summary>
        /// Atomically replaces a destination file with a source file, ensuring that the source's 
        /// security descriptor (ACLs) and metadata are preserved at the destination.
        /// </summary>
        /// <param name="source">The path to the source file.</param>
        /// <param name="destination">The path to the destination file to replace.</param>
        /// <remarks>
        /// <para>
        /// <b>Volume Constraint:</b> This operation is only atomic when both <paramref name="source"/> 
        /// and <paramref name="destination"/> reside on the SAME volume. 
        /// </para>
        /// <para>
        /// If the paths reside on different volumes, this method will throw an <see cref="IOException"/> 
        /// instead of falling back to a non-atomic copy+delete operation. This prevents partial-state 
        /// windows during critical operations like key rotation or service configuration updates.
        /// </para>
        /// </remarks>
        /// <exception cref="IOException">Thrown when source and destination are on different volumes.</exception>
        /// <exception cref="Win32Exception">Thrown when the native MoveFileEx call fails for other reasons.</exception>
        public static void AtomicSecureMove(string source, string destination)
        {
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("Source path cannot be null or empty.", nameof(source));
            if (string.IsNullOrWhiteSpace(destination))
                throw new ArgumentException("Destination path cannot be null or empty.", nameof(destination));

            if (!MoveFileEx(source, destination, MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
            {
                var error = Marshal.GetLastWin32Error();

                if (error == Errors.ERROR_NOT_SAME_DEVICE)
                {
                    // Provide a high-visibility diagnostic message for deployment troubleshooting.
                    throw new IOException(
                        $"AtomicSecureMove failed: Source and destination must be on the same volume to ensure atomicity. " +
                        $"Source: '{Path.GetPathRoot(source)}', Destination: '{Path.GetPathRoot(destination)}'.");
                }

                throw new Win32Exception(error, $"Failed to atomically replace secure file. Win32 Error: {error}");
            }
        }

        /// <summary>
        /// Probes a file to capture its unique identity using OS handles and cryptographic content digests.
        /// </summary>
        /// <param name="fs">The open file stream.</param>
        /// <returns>A populated identity structure. Check <see cref="FILE_IDENTITY.IsValidHandleInfo"/> for success state.</returns>
        public static FILE_IDENTITY GetFileIdentity(FileStream fs)
        {
            var identity = new FILE_IDENTITY();

            // 1. Kernel32 Handle Probe
            try
            {
                if (GetFileInformationByHandle(fs.SafeFileHandle, out var info))
                {
                    identity.VolumeSerialNumber = info.VolumeSerialNumber;
                    identity.FileIndex = ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow;
                    identity.IsValidHandleInfo = true;
                }
            }
            catch (Exception ex)
            {
                // Surfaces P/Invoke failures or AccessViolation/ObjectDisposed exceptions
                Logger.Debug($"GetFileIdentity: Kernel32 handle probe failed for '{fs.Name}'. Exception: {ex.GetType().Name} - {ex.Message}");
            }

            // 2. Prefix-Digest Content Probe
            // Note: We use a separate block to ensure that a handle-info failure 
            // does not prevent a best-effort content check.
            try
            {
                if (fs.CanSeek)
                {
                    long origPos = fs.Position;
                    try
                    {
                        fs.Seek(0, SeekOrigin.Begin);

                        // FIX: Buffer size is configurable via AppConfig to get past common log headers/prologues.
                        // We also incorporate fs.Length to differentiate rotated logs that have identical prefixes but different sizes.
                        byte[] buffer = new byte[AppConfig.FileIdentityPrefixBytes];
                        int read = fs.Read(buffer, 0, buffer.Length);

                        if (read > 0)
                        {
                            using (var sha256 = SHA256.Create())
                            {
                                byte[] hashBytes = sha256.ComputeHash(buffer, 0, read);
                                // Store as a lowercase hex string prefixed with the file length 
                                // for consistent identification across probes and to catch size drifts.
                                identity.PrefixDigest = $"{fs.Length}:{BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant()}";
                            }
                        }
                        else
                        {
                            // Ensure identity.PrefixDigest is never null to prevent NREs in IsDifferentFrom
                            identity.PrefixDigest = string.Empty;
                        }
                    }
                    finally
                    {
                        // Best-effort restore even if Read/Seek threw, so the caller's stream position is preserved.
                        try { fs.Seek(origPos, SeekOrigin.Begin); } catch { /* stream may be dead */ }
                    }
                }
            }
            catch (Exception ex)
            {
                // Surfaces Seek/Read failures (e.g. file truncated or locked by another process)
                Logger.Debug($"GetFileIdentity: Prefix-digest probe failed for '{fs.Name}'. Exception: {ex.GetType().Name} - {ex.Message}");
            }

            return identity;
        }

        #endregion
    }
}

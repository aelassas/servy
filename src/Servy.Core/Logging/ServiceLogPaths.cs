using Servy.Core.Config;
using Servy.Core.Helpers;
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Servy.Core.Logging
{
    /// <summary>
    /// Maps a service to its own log folder, <c>%ProgramData%\Servy\logs\services\&lt;ServiceName&gt;\</c>, where its
    /// wrapper (<c>Servy.Service.log</c>) and its restarter (<c>Servy.Restarter.log</c>) write and rotate their logs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each service account is granted access to the folders of its own services only, so the logs of one service
    /// account cannot be read, written or deleted by another (see <see cref="Security.ServyExePermissionsHardener"/>).
    /// </para>
    /// <para>
    /// A service name may contain characters a folder name cannot (<c>&lt; &gt; : " | ? *</c> and control characters),
    /// may end with a dot or a space that Windows would strip, or may be a reserved device name such as <c>CON</c>.
    /// <see cref="GetFolderName"/> makes it safe by percent-encoding those characters (and <c>%</c> and <c>~</c>
    /// themselves), so two different service names never share a folder. A name whose safe form is longer than
    /// <see cref="MaxFolderNameLength"/> characters is shortened and suffixed with a hash of the name.
    /// </para>
    /// </remarks>
    public static class ServiceLogPaths
    {
        /// <summary>
        /// The longest folder name <see cref="GetFolderName"/> returns. It keeps the log paths well below
        /// <c>MAX_PATH</c> with the rotated file names appended.
        /// </summary>
        public const int MaxFolderNameLength = 100;

        /// <summary>
        /// The number of characters of the safe name kept in front of the hash when a name is shortened.
        /// </summary>
        private const int ShortenedPrefixLength = 64;

        /// <summary>
        /// The number of hexadecimal characters of the SHA-256 hash appended to a shortened name.
        /// </summary>
        private const int HashLength = 16;

        /// <summary>
        /// Gets the safe folder name of a service's log folder.
        /// </summary>
        /// <param name="serviceName">The service name, as registered with the Service Control Manager.</param>
        /// <returns>
        /// A folder name that is valid on NTFS and that no other service name maps to: the service name itself when it
        /// is already safe, otherwise its percent-encoded form, shortened with a hash when it is longer than
        /// <see cref="MaxFolderNameLength"/>.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceName"/> is null, empty or whitespace.</exception>
        public static string GetFolderName(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("The service name is required.", nameof(serviceName));

            var builder = new StringBuilder(serviceName.Length);
            for (var i = 0; i < serviceName.Length; i++)
            {
                var c = serviceName[i];
                var isEdge = i == 0 || i == serviceName.Length - 1;
                if (MustEncode(c) || (c == ' ' && isEdge) || (c == '.' && i == serviceName.Length - 1))
                    builder.Append(Encode(c));
                else
                    builder.Append(c);
            }

            // CON, CON.log and "CON .log" all open the console device, so encode the first character of such a name
            var stem = serviceName.Split('.')[0];
            if (ReservedNames.IsReservedDeviceName(stem))
            {
                builder.Remove(0, 1);
                builder.Insert(0, Encode(serviceName[0]));
            }

            var safe = builder.ToString();
            if (safe.Length <= MaxFolderNameLength)
                return safe;

            // Never cut an escape sequence in half
            var cut = ShortenedPrefixLength;
            var lastPercent = safe.LastIndexOf('%', cut - 1, 3);
            if (lastPercent >= 0 && lastPercent > cut - 3)
                cut = lastPercent;

            return safe.Substring(0, cut) + "~" + GetHash(serviceName);
        }

        /// <summary>
        /// Gets the log folder of a service under <see cref="AppConfig.ServiceLogsFolderPath"/>.
        /// </summary>
        /// <param name="serviceName">The service name, as registered with the Service Control Manager.</param>
        /// <returns>The full path of the service's log folder.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceName"/> is null, empty or whitespace.</exception>
        public static string GetFolderPath(string serviceName)
            => Path.Combine(AppConfig.ServiceLogsFolderPath, GetFolderName(serviceName));

        /// <summary>
        /// Gets the log folder of a service relative to the vault (<c>logs\services\&lt;ServiceName&gt;</c>).
        /// </summary>
        /// <param name="serviceName">The service name, as registered with the Service Control Manager.</param>
        /// <returns>The path of the service's log folder relative to <see cref="AppConfig.ProgramDataPath"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceName"/> is null, empty or whitespace.</exception>
        public static string GetRelativeFolderPath(string serviceName)
            => Path.Combine(AppConfig.LogsFolderName, AppConfig.ServiceLogsFolderName, GetFolderName(serviceName));

        /// <summary>
        /// Determines whether a character must be encoded wherever it appears in the name.
        /// </summary>
        /// <param name="c">The character.</param>
        /// <returns>
        /// <see langword="true"/> for a control character, a character NTFS does not allow in a file name, and for
        /// <c>%</c> and <c>~</c>, which the encoding and the shortening use themselves.
        /// </returns>
        private static bool MustEncode(char c)
        {
            if (c < 0x20 || c == 0x7F)
                return true;

            switch (c)
            {
                case '<':
                case '>':
                case ':':
                case '"':
                case '/':
                case '\\':
                case '|':
                case '?':
                case '*':
                case '%':
                case '~':
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Percent-encodes a character as <c>%XX</c>, with upper-case hexadecimal digits. Only ASCII characters are
        /// ever encoded.
        /// </summary>
        /// <param name="c">The character.</param>
        /// <returns>The encoded character.</returns>
        private static string Encode(char c)
            => "%" + ((int)c).ToString("X2", CultureInfo.InvariantCulture);

        /// <summary>
        /// Hashes a service name case-insensitively, as the Service Control Manager compares service names.
        /// </summary>
        /// <param name="serviceName">The service name.</param>
        /// <returns>The first <see cref="HashLength"/> upper-case hexadecimal characters of its SHA-256 hash.</returns>
        private static string GetHash(string serviceName)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(serviceName.ToUpperInvariant()));
                var hex = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    hex.Append(b.ToString("X2", CultureInfo.InvariantCulture));
                return hex.ToString(0, HashLength);
            }
        }
    }
}

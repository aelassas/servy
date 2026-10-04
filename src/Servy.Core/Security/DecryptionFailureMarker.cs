using Servy.Core.Config;
using Servy.Core.DTOs;
using System.Text.RegularExpressions;

namespace Servy.Core.Security
{
    /// <summary>
    /// The markers the service repository writes into a service's description when at least one of its sensitive fields
    /// cannot be decrypted, and the checks every application uses to recognize such a service.
    /// </summary>
    /// <remarks>
    /// A service read with a field that fails to decrypt comes back with every sensitive field cleared and its description
    /// prefixed with one of these markers, so it can be shown. It must never be saved: saving it would replace the stored
    /// ciphertext with blanks, and the configuration could no longer be recovered with the original <c>aes_key.dat</c>.
    /// </remarks>
    public static class DecryptionFailureMarker
    {
        /// <summary>
        /// The marker of a row whose ciphertext failed to decrypt; <c>{0}</c> is the root cause's exception type.
        /// </summary>
        public const string CorruptFormat = "[DECRYPTION FAILED: {0}] The record's key or payload is corrupt.";

        /// <summary>
        /// The marker of a row whose ciphertext uses the disabled pre-v2 encryption.
        /// </summary>
        public const string LegacyBlocked = "[LEGACY ENCRYPTION BLOCKED] This record uses pre-v2 encryption, which is disabled. Export it with a v1-compatible version of Servy and re-import it here to upgrade.";

        /// <summary>
        /// Separates a marker from the service's original description.
        /// </summary>
        public const string OriginalDescriptionSeparator = " Original Description: ";

        private static readonly Regex MarkerRegex = new Regex(
            "^(?:" + Regex.Escape(string.Format(CorruptFormat, "__MARKER_PLACEHOLDER__")).Replace("__MARKER_PLACEHOLDER__", @"[^\\]+")
            + "|" + Regex.Escape(LegacyBlocked) + ")"
            + Regex.Escape(OriginalDescriptionSeparator.TrimEnd()) + @"\s*",
            RegexOptions.Compiled, AppConfig.InputRegexTimeout);

        /// <summary>
        /// Determines whether a description starts with a decryption failure marker.
        /// </summary>
        /// <param name="description">The description.</param>
        /// <returns><see langword="true"/> when the description carries a marker.</returns>
        public static bool IsPresent(string? description)
            => !string.IsNullOrEmpty(description) && MarkerRegex.IsMatch(description);

        /// <summary>
        /// Determines whether a service was read with at least one sensitive field that failed to decrypt.
        /// </summary>
        /// <param name="service">The service, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when the service must not be saved.</returns>
        /// <remarks>
        /// The decision comes from <see cref="ServiceDto.DecryptionFailed"/>, which the read path sets, and not
        /// from the description text. A marker in the description is display text: Servy v9.5 persisted one into
        /// the stored description (#5186), and such a row decrypts cleanly today, so reading the text here would
        /// refuse every install, update and import of it for good while naming <c>aes_key.dat</c> as a cause that
        /// is not true. The next save strips the stored marker through <see cref="Strip(string)"/>.
        /// </remarks>
        public static bool HasDecryptionFailure(ServiceDto? service)
            => service != null && service.DecryptionFailed;

        /// <summary>
        /// Removes every leading marker from a description.
        /// </summary>
        /// <param name="description">The description.</param>
        /// <returns>The original description.</returns>
        public static string Strip(string description)
        {
            if (string.IsNullOrEmpty(description))
                return description;

            while (MarkerRegex.IsMatch(description))
                description = MarkerRegex.Replace(description, string.Empty);

            return description;
        }
    }
}

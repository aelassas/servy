using System.Globalization;

namespace Servy.UI.Localization
{
    /// <summary>
    /// Maps the language names used on the command line and in the settings files to <see cref="AppLanguage"/>
    /// values, and those values to the UI culture Servy loads its resources for.
    /// </summary>
    public static class LocalizationCulture
    {
        /// <summary>The name that selects <see cref="AppLanguage.System"/>.</summary>
        public const string SystemCode = "system";

        /// <summary>The name that selects <see cref="AppLanguage.English"/>.</summary>
        public const string EnglishCode = "en";

        /// <summary>The name that selects <see cref="AppLanguage.ChineseSimplified"/>, and the culture of its resources.</summary>
        public const string ChineseSimplifiedCode = "zh-CN";

        /// <summary>
        /// Parses a language name given on the command line or stored in a settings file.
        /// </summary>
        /// <param name="value">
        /// <c>system</c>, <c>en</c> or <c>zh-CN</c>, in any casing. A few obvious spellings of each are accepted
        /// too: <c>auto</c>, <c>english</c>, any <c>en-*</c> culture name, and the Simplified Chinese culture names
        /// <c>zh</c>, <c>zh-Hans</c>, <c>zh-Hans-*</c>, <c>zh-SG</c> and <c>zh-CHS</c>.
        /// </param>
        /// <param name="language">The parsed language; <see cref="AppLanguage.System"/> when the method returns <see langword="false"/>.</param>
        /// <returns><see langword="true"/> when <paramref name="value"/> names a supported language.</returns>
        public static bool TryParse(string? value, out AppLanguage language)
        {
            language = AppLanguage.System;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var name = value.Trim();

            if (Is(name, SystemCode) || Is(name, "auto"))
            {
                return true;
            }

            if (Is(name, EnglishCode) || Is(name, nameof(AppLanguage.English))
                || name.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
            {
                language = AppLanguage.English;
                return true;
            }

            if (Is(name, nameof(AppLanguage.ChineseSimplified)) || IsSimplifiedChineseName(name))
            {
                language = AppLanguage.ChineseSimplified;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Parses a stored language name, an empty or unsupported name yielding <see cref="AppLanguage.System"/>.
        /// </summary>
        /// <param name="value">The stored name; see <see cref="TryParse"/> for the accepted spellings.</param>
        /// <returns>The parsed language, or <see cref="AppLanguage.System"/>.</returns>
        public static AppLanguage ParseOrSystem(string? value)
        {
            return TryParse(value, out var language) ? language : AppLanguage.System;
        }

        /// <summary>
        /// Returns the name <paramref name="language"/> is stored under: <c>system</c>, <c>en</c> or <c>zh-CN</c>.
        /// </summary>
        /// <param name="language">The language to name.</param>
        /// <returns>The name <see cref="TryParse"/> maps back to <paramref name="language"/>.</returns>
        public static string Serialize(AppLanguage language)
        {
            switch (language)
            {
                case AppLanguage.English:
                    return EnglishCode;
                case AppLanguage.ChineseSimplified:
                    return ChineseSimplifiedCode;
                default:
                    return SystemCode;
            }
        }

        /// <summary>
        /// Resolves the UI culture to switch to for <paramref name="language"/>.
        /// </summary>
        /// <param name="language">The selected language.</param>
        /// <param name="systemUiCulture">The Windows display language of the user, as the process started with it.</param>
        /// <returns>
        /// The UI culture to apply, or <see langword="null"/> to leave the UI culture as Windows set it. That is the
        /// answer for <see cref="AppLanguage.System"/> on every display language other than Simplified Chinese:
        /// the neutral (English) resources are what such a culture falls back to anyway.
        /// </returns>
        public static CultureInfo? Resolve(AppLanguage language, CultureInfo? systemUiCulture)
        {
            switch (language)
            {
                case AppLanguage.English:
                    return CultureInfo.GetCultureInfo(EnglishCode);
                case AppLanguage.ChineseSimplified:
                    return CultureInfo.GetCultureInfo(ChineseSimplifiedCode);
                default:
                    return systemUiCulture != null && IsSimplifiedChineseName(systemUiCulture.Name)
                        ? CultureInfo.GetCultureInfo(ChineseSimplifiedCode)
                        : null;
            }
        }

        /// <summary>
        /// Tells whether a culture name is a Simplified Chinese one. Traditional Chinese (<c>zh-Hant</c>,
        /// <c>zh-CHT</c>, and the Taiwan, Hong Kong and Macao regions when no script is given) is not.
        /// </summary>
        /// <param name="cultureName">A culture name such as <c>zh-CN</c>, <c>zh-Hans-SG</c> or <c>zh-TW</c>.</param>
        /// <returns><see langword="true"/> for a Simplified Chinese culture name.</returns>
        public static bool IsSimplifiedChineseName(string? cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
            {
                return false;
            }

            var parts = cultureName.Trim().Split('-');
            if (!Is(parts[0], "zh"))
            {
                return false;
            }

            // An explicit script decides: zh-Hans-HK is Simplified, zh-Hant-CN is Traditional
            for (var i = 1; i < parts.Length; i++)
            {
                if (Is(parts[i], "Hans") || Is(parts[i], "CHS"))
                {
                    return true;
                }

                if (Is(parts[i], "Hant") || Is(parts[i], "CHT"))
                {
                    return false;
                }
            }

            // No script: the region decides
            for (var i = 1; i < parts.Length; i++)
            {
                if (Is(parts[i], "TW") || Is(parts[i], "HK") || Is(parts[i], "MO"))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Is(string value, string expected)
        {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}

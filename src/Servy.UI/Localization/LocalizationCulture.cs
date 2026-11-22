using System.Globalization;

namespace Servy.UI.Localization
{
    /// <summary>
    /// Maps a configured language and the system culture to the culture Servy loads resources for.
    /// </summary>
    public static class LocalizationCulture
    {
        /// <summary>The culture of the Simplified Chinese resources.</summary>
        public const string ChineseSimplified = "zh-CN";

        /// <summary>The culture name that selects the neutral (English) resources.</summary>
        public const string English = "";

        /// <summary>
        /// The cultures Servy ships resources for. The empty name is the neutral culture.
        /// </summary>
        public static readonly IReadOnlyList<string> SupportedCultures = new[] { English, ChineseSimplified };

        /// <summary>
        /// Maps a Simplified Chinese culture name to <c>zh-CN</c>, and any other name to the empty (neutral) culture.
        /// </summary>
        /// <param name="cultureName">The culture name, for example <c>zh-CN</c> or <c>fr-FR</c>.</param>
        /// <returns><c>zh-CN</c>, or the empty string for the neutral culture.</returns>
        public static string Normalize(string? cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
            {
                return English;
            }

            var name = cultureName.Trim();

            if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                CultureInfo culture;
                try
                {
                    culture = CultureInfo.GetCultureInfo(name);
                }
                catch (CultureNotFoundException)
                {
                    return name.Equals("zh", StringComparison.OrdinalIgnoreCase) ? ChineseSimplified : English;
                }

                return IsTraditionalChinese(culture) ? English : ChineseSimplified;
            }

            return English;
        }

        /// <summary>
        /// Maps a culture to the culture Servy loads resources for.
        /// </summary>
        public static string FromCulture(CultureInfo? culture)
            => Normalize(culture?.Name);

        /// <summary>
        /// Resolves the configured language against the system culture.
        /// </summary>
        public static string Resolve(AppLanguage language, CultureInfo? systemCulture)
        {
            switch (language)
            {
                case AppLanguage.English:
                    return English;
                case AppLanguage.ChineseSimplified:
                    return ChineseSimplified;
                default:
                    return FromCulture(systemCulture);
            }
        }

        /// <summary>
        /// Converts a culture name to a <see cref="CultureInfo"/>, the neutral culture yielding the invariant culture.
        /// </summary>
        public static CultureInfo ToCulture(string? cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
            {
                return CultureInfo.InvariantCulture;
            }

            var name = Normalize(cultureName);
            return name.Length == 0 ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(name);
        }

        /// <summary>
        /// Parses a persisted language name, an unknown or empty name yielding <see cref="AppLanguage.System"/>.
        /// </summary>
        public static AppLanguage Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return AppLanguage.System;
            }

            return Enum.TryParse<AppLanguage>(value.Trim(), ignoreCase: true, out var language) && Enum.IsDefined(language)
                ? language
                : AppLanguage.System;
        }

        /// <summary>
        /// Serializes a language for the settings files.
        /// </summary>
        public static string Serialize(AppLanguage language)
            => language switch
            {
                AppLanguage.English => nameof(AppLanguage.English),
                AppLanguage.ChineseSimplified => nameof(AppLanguage.ChineseSimplified),
                _ => nameof(AppLanguage.System),
            };

        private static bool IsTraditionalChinese(CultureInfo culture)
        {
            if (culture.Name.EndsWith("-Hant", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var region = culture.Name.Split('-').ElementAtOrDefault(1);
            return region != null && (region.Equals("TW", StringComparison.OrdinalIgnoreCase)
                || region.Equals("HK", StringComparison.OrdinalIgnoreCase)
                || region.Equals("MO", StringComparison.OrdinalIgnoreCase));
        }
    }
}

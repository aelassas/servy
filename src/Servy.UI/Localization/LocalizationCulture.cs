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

        /// <summary>The name that selects <see cref="AppLanguage.French"/>, and the culture of its resources.</summary>
        public const string FrenchCode = "fr";

        /// <summary>The name that selects <see cref="AppLanguage.German"/>, and the culture of its resources.</summary>
        public const string GermanCode = "de";

        /// <summary>The name that selects <see cref="AppLanguage.Spanish"/>, and the culture of its resources.</summary>
        public const string SpanishCode = "es";

        /// <summary>The name that selects <see cref="AppLanguage.Japanese"/>, and the culture of its resources.</summary>
        public const string JapaneseCode = "ja";

        /// <summary>The name that selects <see cref="AppLanguage.Italian"/>, and the culture of its resources.</summary>
        public const string ItalianCode = "it";

        /// <summary>The name that selects <see cref="AppLanguage.PortuguesePortugal"/>, and the culture of its resources.</summary>
        public const string PortuguesePortugalCode = "pt-PT";

        /// <summary>The name that selects <see cref="AppLanguage.PortugueseBrazil"/>, and the culture of its resources.</summary>
        public const string PortugueseBrazilCode = "pt-BR";

        /// <summary>The name that selects <see cref="AppLanguage.Korean"/>, and the culture of its resources.</summary>
        public const string KoreanCode = "ko";

        /// <summary>
        /// Parses a language name given on the command line or stored in a settings file.
        /// </summary>
        /// <param name="value">
        /// <c>system</c>, <c>en</c>, <c>zh-CN</c>, <c>fr</c>, <c>de</c>, <c>es</c>, <c>ja</c>, <c>it</c>, <c>pt-PT</c>,
        /// <c>pt-BR</c> or <c>ko</c>, in any casing. A few obvious spellings of each are accepted too: <c>auto</c>, the
        /// English name of the language (<c>english</c>, <c>french</c>, <c>german</c>, <c>spanish</c>, <c>japanese</c>,
        /// <c>italian</c>, <c>portugueseportugal</c>, <c>portuguesebrazil</c>, <c>korean</c>), any regional culture name
        /// of the language (<c>en-*</c>, <c>fr-*</c>, <c>de-*</c>, <c>es-*</c>, <c>ja-*</c>, <c>it-*</c>,
        /// <c>ko-*</c>), the Simplified Chinese culture names <c>zh</c>, <c>zh-Hans</c>, <c>zh-Hans-*</c>, <c>zh-SG</c>
        /// and <c>zh-CHS</c>, and the Portuguese culture names as <see cref="Resolve"/> maps them (<c>pt</c> and
        /// <c>pt-BR</c> to Brazilian Portuguese, every other <c>pt-*</c> to European Portuguese).
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

            foreach (var translated in TranslatedLanguages)
            {
                if (Is(name, translated.ToString()) || IsCultureOfLanguage(name, Serialize(translated)))
                {
                    language = translated;
                    return true;
                }
            }

            if (Is(name, nameof(AppLanguage.PortuguesePortugal)) || Is(name, nameof(AppLanguage.PortugueseBrazil)))
            {
                language = Is(name, nameof(AppLanguage.PortuguesePortugal))
                    ? AppLanguage.PortuguesePortugal
                    : AppLanguage.PortugueseBrazil;
                return true;
            }

            if (TryGetPortuguese(name, out var portuguese))
            {
                language = portuguese;
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
        /// Returns the name <paramref name="language"/> is stored under: <c>system</c>, <c>en</c>, <c>zh-CN</c>,
        /// <c>fr</c>, <c>de</c>, <c>es</c>, <c>ja</c>, <c>it</c>, <c>pt-PT</c>, <c>pt-BR</c> or <c>ko</c>.
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
                case AppLanguage.French:
                    return FrenchCode;
                case AppLanguage.German:
                    return GermanCode;
                case AppLanguage.Spanish:
                    return SpanishCode;
                case AppLanguage.Japanese:
                    return JapaneseCode;
                case AppLanguage.Italian:
                    return ItalianCode;
                case AppLanguage.PortuguesePortugal:
                    return PortuguesePortugalCode;
                case AppLanguage.PortugueseBrazil:
                    return PortugueseBrazilCode;
                case AppLanguage.Korean:
                    return KoreanCode;
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
        /// The UI culture to apply, or <see langword="null"/> to leave the UI culture as Windows set it. For
        /// <see cref="AppLanguage.System"/> a Simplified Chinese display language resolves to <c>zh-CN</c>, and a
        /// French, German, Spanish, Japanese, Italian or Korean one, neutral or regional (<c>fr-CA</c>, <c>de-CH</c>,
        /// <c>es-419</c>, <c>ja-JP</c>, <c>it-CH</c>, <c>ko-KR</c>, ...), to the neutral culture of that language. A
        /// Portuguese one resolves to <c>pt-BR</c> for Brazil and for the neutral <c>pt</c> (the region .NET picks for
        /// it), and to <c>pt-PT</c> for Portugal and every other region (<c>pt-AO</c>, <c>pt-MZ</c>, ...), which
        /// writes European Portuguese. Every other display language is left alone, because the neutral (English)
        /// resources are what such a culture falls back to anyway.
        /// </returns>
        public static CultureInfo? Resolve(AppLanguage language, CultureInfo? systemUiCulture)
        {
            switch (language)
            {
                case AppLanguage.English:
                    return CultureInfo.GetCultureInfo(EnglishCode);
                case AppLanguage.ChineseSimplified:
                case AppLanguage.French:
                case AppLanguage.German:
                case AppLanguage.Spanish:
                case AppLanguage.Japanese:
                case AppLanguage.Italian:
                case AppLanguage.PortuguesePortugal:
                case AppLanguage.PortugueseBrazil:
                case AppLanguage.Korean:
                    return CultureInfo.GetCultureInfo(Serialize(language));
                default:
                    if (systemUiCulture == null)
                    {
                        return null;
                    }

                    if (IsSimplifiedChineseName(systemUiCulture.Name))
                    {
                        return CultureInfo.GetCultureInfo(ChineseSimplifiedCode);
                    }

                    foreach (var translated in TranslatedLanguages)
                    {
                        var code = Serialize(translated);
                        if (IsCultureOfLanguage(systemUiCulture.Name, code))
                        {
                            return CultureInfo.GetCultureInfo(code);
                        }
                    }

                    if (TryGetPortuguese(systemUiCulture.Name, out var portuguese))
                    {
                        return CultureInfo.GetCultureInfo(Serialize(portuguese));
                    }

                    return null;
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

        /// <summary>
        /// The languages besides English and Simplified Chinese whose resources are named after the neutral culture
        /// of the language, so that every regional culture of the language selects them.
        /// </summary>
        private static readonly AppLanguage[] TranslatedLanguages =
        {
            AppLanguage.French,
            AppLanguage.German,
            AppLanguage.Spanish,
            AppLanguage.Japanese,
            AppLanguage.Italian,
            AppLanguage.Korean,
        };

        /// <summary>
        /// Tells which Portuguese language a culture name selects. Portuguese has two translations named after
        /// regional cultures, so a regional culture other than <c>pt-BR</c> cannot reach either through the resource
        /// fallback: <c>pt</c> and <c>pt-BR</c> (and <c>pt-BR-*</c>) select Brazilian Portuguese, the region .NET
        /// picks for the neutral <c>pt</c>, and every other <c>pt-*</c> culture (<c>pt-PT</c>, <c>pt-AO</c>,
        /// <c>pt-MZ</c>, ...) selects European Portuguese, which those regions write.
        /// </summary>
        /// <param name="cultureName">A culture name such as <c>pt</c>, <c>pt-BR</c> or <c>pt-AO</c>.</param>
        /// <param name="language">
        /// <see cref="AppLanguage.PortugueseBrazil"/> or <see cref="AppLanguage.PortuguesePortugal"/>;
        /// <see cref="AppLanguage.System"/> when the method returns <see langword="false"/>.
        /// </param>
        /// <returns><see langword="true"/> for a Portuguese culture name.</returns>
        private static bool TryGetPortuguese(string cultureName, out AppLanguage language)
        {
            language = AppLanguage.System;
            var parts = cultureName.Trim().Split('-');
            if (!Is(parts[0], "pt"))
            {
                return false;
            }

            language = parts.Length == 1 || Is(parts[1], "BR")
                ? AppLanguage.PortugueseBrazil
                : AppLanguage.PortuguesePortugal;
            return true;
        }

        /// <summary>
        /// Tells whether <paramref name="cultureName"/> is the neutral culture <paramref name="code"/> or one of its
        /// regional cultures, such as <c>fr-CA</c> or <c>es-419</c> for <c>fr</c> and <c>es</c>.
        /// </summary>
        private static bool IsCultureOfLanguage(string cultureName, string code)
        {
            var name = cultureName.Trim();
            return Is(name, code) || name.StartsWith(code + "-", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Is(string value, string expected)
        {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}

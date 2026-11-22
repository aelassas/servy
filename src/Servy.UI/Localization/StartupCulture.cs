using Newtonsoft.Json.Linq;
using System.Globalization;
using System.IO;

namespace Servy.UI.Localization
{
    /// <summary>
    /// Reads the configured language from an application settings file and applies it to the calling thread.
    /// </summary>
    public static class StartupCulture
    {
        /// <summary>The settings key holding the selected language.</summary>
        public const string LanguageSettingKey = "Language";

        /// <summary>The settings key recording that the language changed and a restart is pending.</summary>
        public const string LanguageChangedSettingKey = "LanguageChanged";

        /// <summary>
        /// The settings keys the localization layer owns, so the settings loader does not report them as unknown.
        /// </summary>
        public static readonly IReadOnlyList<string> IgnoredSettingKeys = new[] { LanguageSettingKey, LanguageChangedSettingKey };

        private const string CamelCaseLanguageSettingKey = "language";

        /// <summary>
        /// Applies the language configured in <paramref name="configPath"/> and returns the language that was read.
        /// A missing or malformed file selects <see cref="AppLanguage.System"/> and never throws.
        /// </summary>
        /// <param name="configPath">The full path of the application settings file.</param>
        /// <param name="systemCulture">The system culture; defaults to <see cref="CultureInfo.InstalledUICulture"/>.</param>
        public static AppLanguage ApplyFromSettings(string configPath, CultureInfo? systemCulture = null)
        {
            var language = ReadLanguage(configPath);
            var culture = LocalizationCulture.ToCulture(
                LocalizationCulture.Resolve(language, systemCulture ?? CultureInfo.InstalledUICulture));

            TemporaryCultureScope.Apply(culture);
            return language;
        }

        /// <summary>
        /// Reads the configured language, accepting both the PascalCase and camelCase JSON spellings.
        /// </summary>
        public static AppLanguage ReadLanguage(string configPath)
        {
            var root = TryReadRoot(configPath);
            return LocalizationCulture.Parse((root?[LanguageSettingKey] ?? root?[CamelCaseLanguageSettingKey])?.ToString());
        }

        /// <summary>
        /// Writes the language under the given settings key, plus a restart flag the application's settings file can carry.
        /// </summary>
        /// <param name="configPath">The full path of the settings file.</param>
        /// <param name="settingsKey">The exact key the target application reads.</param>
        /// <param name="language">The language to persist.</param>
        public static void WriteLanguage(string configPath, string settingsKey, AppLanguage language)
        {
            var root = TryReadRoot(configPath) ?? new JObject();
            root[settingsKey] = LocalizationCulture.Serialize(language);
            root[LanguageChangedSettingKey] = true;
            WriteRoot(configPath, root);
        }

        private static JObject? TryReadRoot(string configPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
                {
                    return null;
                }

                return JObject.Parse(File.ReadAllText(configPath));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void WriteRoot(string configPath, JObject root)
        {
            var bytes = new System.Text.UTF8Encoding(false).GetBytes(root.ToString(Newtonsoft.Json.Formatting.Indented));
            Core.Helpers.Helper.WriteFileAtomic(configPath, stream => stream.Write(bytes, 0, bytes.Length));
        }
    }
}

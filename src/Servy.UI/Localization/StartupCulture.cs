using Newtonsoft.Json.Linq;
using Servy.Core.Helpers;
using Servy.Core.Logging;
using System.Globalization;
using System.IO;
using System.Text;

namespace Servy.UI.Localization
{
    /// <summary>
    /// Selects the display language of a Servy application at start-up, from the <c>--lang</c> command-line
    /// option and the <c>Language</c> key of the application's settings file, and keeps the two in sync.
    /// </summary>
    /// <remarks>
    /// Only the UI culture is switched, which is what resource lookups follow. The culture used to format
    /// numbers and dates stays the one Windows gave the process.
    /// </remarks>
    public static class StartupCulture
    {
        /// <summary>The settings key holding the selected language.</summary>
        public const string LanguageSettingKey = "Language";

        /// <summary>The command-line option selecting the language, as <c>--lang zh-CN</c> or <c>--lang=zh-CN</c>.</summary>
        public const string LanguageArg = "--lang";

        /// <summary>
        /// The settings files of the applications that share the display language. They sit in the same directory,
        /// and a language change is written to all of them.
        /// </summary>
        public static readonly IReadOnlyList<string> SettingsFileNames = new[] { "appsettings.desktop.json", "appsettings.manager.json" };

        /// <summary>
        /// Gets the outcome of the last <see cref="Apply(string, IEnumerable{string}, CultureInfo)"/> call, or <see langword="null"/> before the first one.
        /// </summary>
        public static StartupCultureResult? Last { get; private set; }

        /// <summary>
        /// Selects the language and applies its UI culture to the calling thread and to the threads started later.
        /// </summary>
        /// <param name="configPath">The full path of the application's settings file.</param>
        /// <param name="args">The command-line arguments, without the executable path.</param>
        /// <param name="systemUiCulture">The Windows display language; defaults to <see cref="CultureInfo.CurrentUICulture"/>.</param>
        /// <returns>What was selected. The method never throws: a problem is reported in the result.</returns>
        /// <remarks>
        /// A valid <c>--lang</c> wins over the settings file and is written to the settings files of both
        /// applications (see <see cref="SettingsFileNames"/>), so the next start of either one without the option
        /// uses the same language. An unsupported <c>--lang</c> value is ignored.
        /// </remarks>
        public static StartupCultureResult Apply(string configPath, IEnumerable<string>? args, CultureInfo? systemUiCulture = null)
        {
            return Apply(configPath, args, systemUiCulture, ApplyUiCulture);
        }

        /// <summary>
        /// Selects the language and hands its UI culture to <paramref name="applyCulture"/>.
        /// </summary>
        /// <param name="configPath">The full path of the application's settings file.</param>
        /// <param name="args">The command-line arguments, without the executable path.</param>
        /// <param name="systemUiCulture">The Windows display language; <see langword="null"/> uses <see cref="CultureInfo.CurrentUICulture"/>.</param>
        /// <param name="applyCulture">Switches the UI culture; not called when the culture is left as Windows set it.</param>
        /// <returns>What was selected. The method never throws for a settings or argument problem: it is reported in the result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="applyCulture"/> is <see langword="null"/>.</exception>
        public static StartupCultureResult Apply(string configPath, IEnumerable<string>? args, CultureInfo? systemUiCulture, Action<CultureInfo> applyCulture)
        {
            if (applyCulture == null) throw new ArgumentNullException(nameof(applyCulture));

            var language = ReadLanguage(configPath);
            string? rejected = null;
            string? syncError = null;

            if (TryGetLanguageArg(args, out var value))
            {
                if (LocalizationCulture.TryParse(value, out var requested))
                {
                    if (GetSettingsFiles(configPath).Any(path => ReadLanguage(path) != requested))
                    {
                        try
                        {
                            var failures = WriteLanguageToAll(configPath, requested);
                            if (failures.Count > 0)
                            {
                                syncError = string.Join("; ", failures);
                            }
                        }
                        catch (Exception ex)
                        {
                            syncError = ex.Message;
                        }
                    }

                    language = requested;
                }
                else
                {
                    rejected = value ?? string.Empty;
                }
            }

            var culture = LocalizationCulture.Resolve(language, systemUiCulture ?? CultureInfo.CurrentUICulture);
            if (culture != null)
            {
                applyCulture(culture);
            }

            var result = new StartupCultureResult(language, culture, rejected, syncError);
            Last = result;
            return result;
        }

        /// <summary>
        /// Switches the UI culture of the calling thread and of the threads started later. The culture used to
        /// format numbers and dates is left alone.
        /// </summary>
        /// <param name="culture">The UI culture to apply.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="culture"/> is <see langword="null"/>.</exception>
        public static void ApplyUiCulture(CultureInfo culture)
        {
            if (culture == null) throw new ArgumentNullException(nameof(culture));

            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }

        /// <summary>
        /// Writes what an <see cref="Apply(string, IEnumerable{string}, CultureInfo)"/> call could not do to the log.
        /// Called once the logger is configured, because the language is selected before that.
        /// </summary>
        /// <param name="result">The outcome to report; <see langword="null"/> reports nothing.</param>
        /// <param name="logger">The logger to write to; <see langword="null"/> uses the static <see cref="Logger"/>.</param>
        public static void LogOutcome(StartupCultureResult? result, IServyLogger? logger = null)
        {
            if (result == null)
            {
                return;
            }

            if (result.RejectedArgument != null)
            {
                Warn(logger, $"Ignored {LanguageArg} '{result.RejectedArgument}': supported values are "
                    + $"{LocalizationCulture.SystemCode}, {LocalizationCulture.EnglishCode}, {LocalizationCulture.ChineseSimplifiedCode}, "
                    + $"{LocalizationCulture.FrenchCode}, {LocalizationCulture.GermanCode}, {LocalizationCulture.SpanishCode} and {LocalizationCulture.JapaneseCode}.");
            }

            if (result.SyncError != null)
            {
                Warn(logger, $"Failed to save the {LanguageSettingKey} setting given with {LanguageArg} to every settings file: {result.SyncError}");
            }
        }

        /// <summary>
        /// Reads the language stored in a settings file.
        /// </summary>
        /// <param name="configPath">The full path of the settings file.</param>
        /// <returns>
        /// The stored language. A missing or unreadable file, a missing key and an unsupported value all yield
        /// <see cref="AppLanguage.System"/>; the method never throws.
        /// </returns>
        public static AppLanguage ReadLanguage(string configPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
                {
                    return AppLanguage.System;
                }

                var root = JObject.Parse(File.ReadAllText(configPath));
                var token = root.GetValue(LanguageSettingKey, StringComparison.OrdinalIgnoreCase);
                return LocalizationCulture.ParseOrSystem(token?.Type == JTokenType.String ? token.ToString() : null);
            }
            catch (Exception)
            {
                return AppLanguage.System;
            }
        }

        /// <summary>
        /// Stores the language in a settings file, keeping every other key, in one atomic replacement.
        /// </summary>
        /// <param name="configPath">The full path of the settings file; it is created when missing.</param>
        /// <param name="language">The language to store.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="configPath"/> is null or blank.</exception>
        /// <exception cref="Newtonsoft.Json.JsonException">Thrown when the existing file is not valid JSON; it is left untouched.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read or replaced.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the file cannot be read or replaced.</exception>
        public static void WriteLanguage(string configPath, AppLanguage language)
        {
            if (string.IsNullOrWhiteSpace(configPath))
            {
                throw new ArgumentException("The configuration path cannot be null or empty.", nameof(configPath));
            }

            var root = File.Exists(configPath) ? JObject.Parse(File.ReadAllText(configPath)) : new JObject();

            // Configuration keys are case-insensitive, so a differently cased spelling is replaced, not shadowed
            var existing = root.Property(LanguageSettingKey, StringComparison.OrdinalIgnoreCase);
            if (existing != null)
            {
                existing.Value = LocalizationCulture.Serialize(language);
            }
            else
            {
                root[LanguageSettingKey] = LocalizationCulture.Serialize(language);
            }

            var bytes = new UTF8Encoding(false).GetBytes(root.ToString(Newtonsoft.Json.Formatting.Indented));
            Helper.WriteFileAtomic(configPath, stream => stream.Write(bytes, 0, bytes.Length));
        }

        /// <summary>
        /// Lists the settings files a language change is written to: <paramref name="configPath"/> first, then
        /// the settings file of the other application in the same directory.
        /// </summary>
        /// <param name="configPath">The full path of the settings file of the running application.</param>
        /// <returns>
        /// The files to keep in sync. Only <paramref name="configPath"/> when its name is not one of
        /// <see cref="SettingsFileNames"/>; empty when it is null or blank.
        /// </returns>
        public static IReadOnlyList<string> GetSettingsFiles(string configPath)
        {
            var files = new List<string>();
            if (string.IsNullOrWhiteSpace(configPath))
            {
                return files;
            }

            files.Add(configPath);

            var name = Path.GetFileName(configPath);
            if (SettingsFileNames.Any(known => known.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                var directory = Path.GetDirectoryName(configPath) ?? string.Empty;
                files.AddRange(SettingsFileNames
                    .Where(known => !known.Equals(name, StringComparison.OrdinalIgnoreCase))
                    .Select(known => Path.Combine(directory, known)));
            }

            return files;
        }

        /// <summary>
        /// Stores the language in the settings file of the running application and in the settings file of the
        /// other application, so both display the same language.
        /// </summary>
        /// <param name="configPath">The full path of the settings file of the running application.</param>
        /// <param name="language">The language to store.</param>
        /// <returns>
        /// One message per other settings file that could not be written; empty when every file was written.
        /// </returns>
        /// <remarks>
        /// The file of the running application is written first and its failure is thrown, exactly as
        /// <see cref="WriteLanguage"/> throws it: nothing else is written then.
        /// </remarks>
        public static IReadOnlyList<string> WriteLanguageToAll(string configPath, AppLanguage language)
        {
            WriteLanguage(configPath, language);

            var failures = new List<string>();
            foreach (var other in GetSettingsFiles(configPath).Skip(1))
            {
                try
                {
                    WriteLanguage(other, language);
                }
                catch (Exception ex)
                {
                    failures.Add($"{Path.GetFileName(other)}: {ex.Message}");
                }
            }

            return failures;
        }

        /// <summary>
        /// Finds the <c>--lang</c> option in the command-line arguments.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <param name="value">The value given with the option; <see langword="null"/> when the option has none.</param>
        /// <returns><see langword="true"/> when the option is present, with or without a value.</returns>
        public static bool TryGetLanguageArg(IEnumerable<string>? args, out string? value)
        {
            value = null;
            if (args == null)
            {
                return false;
            }

            var list = args.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                var arg = list[i];
                if (arg == null)
                {
                    continue;
                }

                if (arg.Equals(LanguageArg, StringComparison.OrdinalIgnoreCase))
                {
                    value = i + 1 < list.Count ? list[i + 1] : null;
                    return true;
                }

                if (arg.StartsWith(LanguageArg + "=", StringComparison.OrdinalIgnoreCase))
                {
                    value = arg.Substring(LanguageArg.Length + 1);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the command-line arguments without the <c>--lang</c> option and its value, so the positional
        /// arguments keep their places.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>The remaining arguments, in their original order.</returns>
        public static IReadOnlyList<string> StripLanguageArg(IEnumerable<string>? args)
        {
            var remaining = new List<string>();
            if (args == null)
            {
                return remaining;
            }

            var skipValue = false;
            foreach (var arg in args)
            {
                if (skipValue)
                {
                    skipValue = false;
                    continue;
                }

                if (arg != null && arg.Equals(LanguageArg, StringComparison.OrdinalIgnoreCase))
                {
                    skipValue = true;
                    continue;
                }

                if (arg != null && arg.StartsWith(LanguageArg + "=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                remaining.Add(arg!);
            }

            return remaining;
        }

        private static void Warn(IServyLogger? logger, string message)
        {
            if (logger != null)
            {
                logger.Warn(message);
            }
            else
            {
                Logger.Warn(message);
            }
        }
    }
}

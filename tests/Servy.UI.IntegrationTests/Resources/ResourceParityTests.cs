using Servy.Core.Resources;
using Servy.Manager.Resources;
using Servy.UI.Resources;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;

namespace Servy.UI.IntegrationTests.Resources
{
    /// <summary>
    /// Compares each project's neutral resx with the Simplified Chinese satellite: the same keys, the same
    /// format placeholders, no blank values and no visible text left in English.
    /// </summary>
    public class ResourceParityTests
    {
        private static readonly Regex PlaceholderPattern = new Regex(@"\{(\d+)(?:[^}]*)\}", RegexOptions.Compiled);
        private static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-CN");

        /// <summary>
        /// Values that are deliberately identical in both cultures: brand names, account aliases, column
        /// abbreviations and the language names shown in their own script.
        /// </summary>
        private static readonly HashSet<string> KeepEnglish = new HashSet<string>(StringComparer.Ordinal)
        {
            "Language_English",
            "Language_System",
            "Msg_Show_Masked",
            "Msg_Show_ValueNotSet",
            "Msg_Show_Yes",
            "Msg_Show_No",
            "Column_Pid",
            "Column_CPU",
            "Column_RAM",
            "Label_Pid",
            "Label_CPU",
            "Label_RAM",
            "Account_LocalSystem",
            "Account_LocalService",
            "Account_NetworkService",
            "Msg_Show_Label_RunAsLocalSystem",
            "Msg_Show_Label_Pid",
            "Msg_Show_Column_Pid",
            "Msg_Show_Megabytes",
        };

        public static TheoryData<string, string> Assemblies => new TheoryData<string, string>
        {
            { "Servy", "Servy.Resources.Strings" },
            { "Servy.Manager", "Servy.Manager.Resources.Strings" },
            { "Servy.UI", "Servy.UI.Resources.Strings" },
            { "Servy.Core", "Servy.Core.Resources.Strings" },
        };

        #region Key parity

        [Theory]
        [MemberData(nameof(Assemblies))]
        public void ZhCn_HasExactlyTheNeutralKeys(string assemblyName, string typeName)
        {
            var resources = Load(assemblyName, typeName);

            Assert.Equal(resources.NeutralKeys, resources.ChineseKeys);
        }

        [Theory]
        [MemberData(nameof(Assemblies))]
        public void ZhCn_HasNoBlankValues(string assemblyName, string typeName)
        {
            var resources = Load(assemblyName, typeName);

            var blank = resources.ChineseKeys.Where(k => string.IsNullOrWhiteSpace(resources.Chinese(k))).ToList();

            Assert.Empty(blank);
        }

        #endregion

        #region Placeholder parity

        [Theory]
        [MemberData(nameof(Assemblies))]
        public void ZhCn_KeepsEveryFormatPlaceholder(string assemblyName, string typeName)
        {
            var resources = Load(assemblyName, typeName);

            var mismatches = new List<string>();
            foreach (var key in resources.NeutralKeys)
            {
                var expected = Placeholders(resources.English(key));
                var actual = Placeholders(resources.Chinese(key));
                if (!expected.SetEquals(actual))
                {
                    mismatches.Add($"{key}: expected [{string.Join(",", expected)}], found [{string.Join(",", actual)}]");
                }
            }

            Assert.Empty(mismatches);
        }

        #endregion

        #region Translation presence

        [Theory]
        [MemberData(nameof(Assemblies))]
        public void ZhCn_TranslatesVisibleText(string assemblyName, string typeName)
        {
            var resources = Load(assemblyName, typeName);

            var untranslated = resources.NeutralKeys
                .Where(k => !KeepEnglish.Contains(k))
                .Where(k => string.Equals(resources.English(k), resources.Chinese(k), StringComparison.Ordinal))
                .ToList();

            Assert.Empty(untranslated);
        }

        #endregion

        #region Helpers

        private sealed record Resources(IReadOnlyList<string> NeutralKeys, IReadOnlyList<string> ChineseKeys, Func<string, string?> English, Func<string, string?> Chinese);

        private static Resources Load(string assemblyName, string typeName)
        {
            var neutralFile = FindResx(assemblyName, typeName);
            var chineseFile = neutralFile[..^".resx".Length] + ".zh-CN.resx";

            // The satellite assembly next to the test binary is the deployment proof; the values are read from the
            // resx files, so no lookup can answer with the other culture.
            var satellite = Path.Combine(AppContext.BaseDirectory, "zh-CN", assemblyName + ".resources.dll");
            Assert.True(File.Exists(satellite), $"{assemblyName} was built without its zh-CN satellite at '{satellite}'.");
            Assert.True(File.Exists(chineseFile), $"The zh-CN resx of {typeName} was not found at '{chineseFile}'.");

            var english = ReadResx(neutralFile);
            var translated = ReadResx(chineseFile);

            return new Resources(
                english.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(),
                translated.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(),
                key => english.TryGetValue(key, out var v) ? v : null,
                key => translated.TryGetValue(key, out var v) ? v : null);
        }

        private static string FindResx(string assemblyName, string typeName)
        {
            var candidate = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", assemblyName, "Resources", "Strings.resx");

            Assert.True(File.Exists(candidate), $"The neutral resx of {typeName} was not found at '{candidate}'.");
            return Path.GetFullPath(candidate);
        }

        private static Dictionary<string, string> ReadResx(string path)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var document = System.Xml.Linq.XDocument.Load(path);

            foreach (var data in document.Root!.Elements("data"))
            {
                var name = data.Attribute("name")?.Value;
                var value = data.Element("value")?.Value;
                if (name != null && value != null)
                {
                    result[name] = value;
                }
            }

            return result;
        }

        private static HashSet<string> Placeholders(string? value)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(value))
            {
                return found;
            }

            foreach (Match match in PlaceholderPattern.Matches(value))
            {
                found.Add(match.Groups[1].Value);
            }

            return found;
        }

        #endregion
    }
}

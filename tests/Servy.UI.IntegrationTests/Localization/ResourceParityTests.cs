using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Servy.UI.IntegrationTests.Localization
{
    /// <summary>
    /// Guards the Simplified Chinese resource files against the mistakes a translation can make: a key the
    /// English file does not have, an empty value, and a format placeholder that was dropped or added.
    /// </summary>
    /// <remarks>
    /// A key that is missing from a translation is allowed on purpose: the lookup falls back to the English
    /// text, so a string added in English does not have to wait for its translation.
    /// </remarks>
    public class ResourceParityTests
    {
        private const string Culture = "zh-CN";

        private static readonly Regex PlaceholderPattern = new Regex(@"\{(\d+)(?:[^}]*)\}", RegexOptions.Compiled);

        public static TheoryData<string> Projects => new TheoryData<string>
        {
            "Servy",
            "Servy.Manager",
            "Servy.UI",
            "Servy.Core",
        };

        [Theory]
        [MemberData(nameof(Projects))]
        public void Translation_HasNoKeyTheEnglishFileLacks(string project)
        {
            // Arrange
            var english = ReadResx(NeutralPath(project));
            var translated = ReadResx(TranslatedPath(project));

            // Act
            var unknown = translated.Keys.Where(k => !english.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();

            // Assert
            Assert.NotEmpty(translated);
            Assert.Empty(unknown);
        }

        [Theory]
        [MemberData(nameof(Projects))]
        public void Translation_HasNoBlankValueForANonBlankEnglishText(string project)
        {
            // Arrange
            var english = ReadResx(NeutralPath(project));
            var translated = ReadResx(TranslatedPath(project));

            // Act
            var blank = translated
                .Where(p => string.IsNullOrWhiteSpace(p.Value) && english.TryGetValue(p.Key, out var text) && !string.IsNullOrWhiteSpace(text))
                .Select(p => p.Key)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();

            // Assert
            Assert.Empty(blank);
        }

        [Theory]
        [MemberData(nameof(Projects))]
        public void Translation_KeepsEveryFormatPlaceholderOfTheEnglishText(string project)
        {
            // Arrange
            var english = ReadResx(NeutralPath(project));
            var translated = ReadResx(TranslatedPath(project));

            // Act
            var mismatches = translated
                .Where(p => english.ContainsKey(p.Key) && !Placeholders(english[p.Key]).SetEquals(Placeholders(p.Value)))
                .Select(p => p.Key)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();

            // Assert
            Assert.Empty(mismatches);
        }

        [Theory]
        [InlineData("Servy.UI")]
        [InlineData("Servy.Core")]
        public void Build_PlacesTheSatelliteAssemblyNextToTheBinaries(string assemblyName)
        {
            // Arrange
            var satellite = Path.Combine(AppContext.BaseDirectory, Culture, assemblyName + ".resources.dll");

            // Act
            var exists = File.Exists(satellite);

            // Assert
            Assert.True(exists, $"{assemblyName} was built without its {Culture} satellite assembly at '{satellite}'.");
        }

        private static string NeutralPath(string project)
        {
            return Path.Combine(FindRepositoryRoot(), "src", project, "Resources", "Strings.resx");
        }

        private static string TranslatedPath(string project)
        {
            return Path.Combine(FindRepositoryRoot(), "src", project, "Resources", "Strings." + Culture + ".resx");
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "src", "Servy.Core", "Resources", "Strings.resx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException($"The repository root was not found above '{AppContext.BaseDirectory}'.");
        }

        private static Dictionary<string, string> ReadResx(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"The resource file '{path}' was not found.", path);
            }

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var data in XDocument.Load(path).Root.Elements("data"))
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

        private static HashSet<string> Placeholders(string value)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in PlaceholderPattern.Matches(value ?? string.Empty))
            {
                found.Add(match.Groups[1].Value);
            }

            return found;
        }
    }
}

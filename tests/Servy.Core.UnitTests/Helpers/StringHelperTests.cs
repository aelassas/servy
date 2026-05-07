using Servy.Core.Helpers;
using System.Reflection;

namespace Servy.Core.UnitTests.Helpers
{
    public class StringHelperTests
    {
        [Theory]
        // --- Basic Null/Empty Handling ---
        [InlineData(null, "")]
        [InlineData("", "")]

        // --- Standard Line Break Replacement ---
        [InlineData("line1", "line1")]
        [InlineData("line1\r\nline2", "line1;line2")]
        [InlineData("line1\nline2", "line1;line2")]
        [InlineData("line1\rline2", "line1;line2")]
        [InlineData("line1\r\nline2;line3", "line1;line2\\;line3")]
        [InlineData("line1\rline2;line3", "line1;line2\\;line3")]
        [InlineData("line1\nline2;line3", "line1;line2\\;line3")]
        [InlineData("line1\r\nline2\nline3\rline4", "line1;line2;line3;line4")]

        // --- Semicolon Escaping (The "PATH" Scenarios) ---
        // Should escape an unescaped semicolon within a single entry
        [InlineData("PATH=C:\\Tools;C:\\Data", "PATH=C:\\Tools\\;C:\\Data")]
        // Should NOT double-escape an already escaped semicolon
        [InlineData("PATH=C:\\Tools\\;C:\\Data", "PATH=C:\\Tools\\;C:\\Data")]
        // Mixed: One unescaped, one escaped
        [InlineData("VAR=val1;val2\\;val3", "VAR=val1\\;val2\\;val3")]

        // --- Complex Multi-line + Semicolon Combinations ---
        // Multi-line input where individual lines contain semicolons
        [InlineData("PATH=C:\\Tools;C:\\Data\nHOME=C:\\Users", "PATH=C:\\Tools\\;C:\\Data;HOME=C:\\Users")]
        // Multiple semicolons in one line
        [InlineData("a;b;c", "a\\;b\\;c")]
        // Semicolon at the very start or end of a line
        [InlineData(";start", "\\;start")]
        [InlineData("end;", "end\\;")]

        // --- Edge Cases ---
        // Empty lines should be removed by StringSplitOptions.RemoveEmptyEntries
        [InlineData("line1\n\nline2", "line1;line2")]
        // Backslash not followed by a semicolon should remain untouched
        [InlineData("C:\\Only\\Backslashes\\", "C:\\Only\\Backslashes\\")]
        public void NormalizeString_ShouldHandleLineBreaksAndEscapeSemicolons(string? input, string expected)
        {
            var result = StringHelper.NormalizeString(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void FormatServiceDependencies_ShouldReturnNull_WhenInputIsNull()
        {
            var result = StringHelper.FormatServiceDependencies(null!);
            Assert.Null(result);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("dep1;dep2;dep3", "dep1\r\ndep2\r\ndep3")]
        [InlineData("singleDep", "singleDep")]
        public void FormatServiceDependencies_ShouldReplaceSemicolonWithNewLine(string input, string expected)
        {
            var result = StringHelper.FormatServiceDependencies(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Escape_NullInput_ReturnsEmptyString_UsingReflection()
        {
            // Arrange
            var type = typeof(StringHelper);
            var method = type.GetMethod("Escape", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            // Act
            var result = method.Invoke(null, new object[] { null! }) as string;

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void FormatEnvirnomentVariables_ShouldEscapeSpecialCharactersCorrectly()
        {
            // Arrange
            // Each variable tests one or more escape sequences:
            // - VAR1: normal, no escaping
            // - VAR2: '=' in value
            // - VAR3: ';' in value
            // - VAR4: '"' in value
            // - VAR5: '\' in value
            // - VAR6: combinations of multiple escaped chars
            var rawVars = string.Join(";", new[]
            {
                "VAR1=val1",
                @"VAR2=a\=b",
                @"VAR3=x\;y",
                @"VAR4=hello \""world\""",
                "VAR5=C:\\path\\to\\file",
                "VAR6=combo\\=\\;\\\"end\\\"",
                @"VAR7\=a=b\=c",
            });

            // Act
            var result = StringHelper.FormatEnvironmentVariables(rawVars);

            // Assert
            var expected = string.Join(Environment.NewLine, new[]
            {
                "VAR1=val1",
                @"VAR2=a\=b",
                @"VAR3=x\;y",
                @"VAR4=hello \""world\""",
                @"VAR5=C:\\path\\to\\file",
                "VAR6=combo\\=\\;\\\"end\\\"",
                @"VAR7\=a=b\=c",
            });

            Assert.Equal(expected, result);
        }

    }
}

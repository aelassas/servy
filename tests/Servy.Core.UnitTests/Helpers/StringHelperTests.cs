using Servy.Core.EnvironmentVariables;
using Servy.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class StringHelperTests
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("line1", "line1")]
        [InlineData("line1\r\nline2", "line1;line2")]
        [InlineData("line1\nline2", "line1;line2")]
        [InlineData("line1\rline2", "line1;line2")]
        [InlineData("line1\r\nline2;line3", "line1;line2;line3")]
        [InlineData("line1\rline2;line3", "line1;line2;line3")]
        [InlineData("line1\nline2;line3", "line1;line2;line3")]
        [InlineData("line1\r\nline2\nline3\rline4", "line1;line2;line3;line4")]
        [InlineData("VAR1=value1\r\nVAR2=value2\nVAR3=value3\rVAR4=value4", "VAR1=value1;VAR2=value2;VAR3=value3;VAR4=value4")]
        public void NormalizeString_ReplacesLineBreaksWithSemicolons(string input, string expected)
        {
            var result = StringHelper.NormalizeString(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("PATH=C:\\foo\\\r\nTEMP=C:\\bar", "PATH=C:\\foo\\\\;TEMP=C:\\bar")]
        [InlineData("PATH=C:\\foo\\\nTEMP=C:\\bar", "PATH=C:\\foo\\\\;TEMP=C:\\bar")]
        [InlineData("PATH=C:\\foo\\\rTEMP=C:\\bar", "PATH=C:\\foo\\\\;TEMP=C:\\bar")]
        [InlineData("PATH=C:\\foo\\", "PATH=C:\\foo\\\\")] // End of string boundary check
        public void NormalizeString_ShouldDoubleTrailingBackslashes_WhenLineEndsWithBackslash(string input, string expected)
        {
            // Act
            string result = StringHelper.NormalizeString(input);

            // Assert
            // Verifies that the backslash is doubled so downstream EscapedTokenizer interprets it as 
            // a literal backslash followed by a separate record delimiter semicolon.
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("KEY=C:\\Foo\\\\\r\nNEXT=Val", "KEY=C:\\Foo\\\\;NEXT=Val")] // Trailing even run (2) safely remains even (2)
        public void NormalizeString_ValidatesExplicitLineBreakParity(string input, string expected)
        {
            // Act
            string result = StringHelper.NormalizeString(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void EnvironmentVariable_FullRoundTripLifecycle_PreservesTrickyPunctuation()
        {
            // Arrange: Replicates all collision conditions specified in issue #1972
            var originalVars = new List<EnvironmentVariable>
            {
                new EnvironmentVariable { Name = "KEY_TRAILING_SLASH", Value = @"C:\Foo\" },
                new EnvironmentVariable { Name = "KEY_DOUBLE_SLASH", Value = @"C:\Bar\\" },
                new EnvironmentVariable { Name = "KEY_INTERNAL_NEWLINE", Value = "line1\\nline2" },
                new EnvironmentVariable { Name = "KEY_ESCAPED_SEMICOLON", Value = "value1;value2" },
                new EnvironmentVariable { Name = "STANDARD_KEY", Value = "NormalValue" }
            };

            // Convert raw structural model to single-line persistence string (Simulates initial load from DB)
            // Example layout: "KEY_TRAILING_SLASH=C:\\Foo\\;KEY_DOUBLE_SLASH=..."
            string serializedDbString = string.Join(";", originalVars.Select(v => $"{v.Name}={StringHelper.Escape(v.Value)}"));

            // Step 1: Format for UI presentation (Multi-line layout)
            string uiText = StringHelper.FormatEnvironmentVariables(serializedDbString);

            // Step 2: Act - Run the UI multi-line string through our updated, parity-aware normalization
            string singleLineSaveString = StringHelper.NormalizeString(uiText);

            // Step 3: Parse the saved string back into model objects
            List<EnvironmentVariable> processedVars = EnvironmentVariableParser.Parse(singleLineSaveString).ToList();

            // Assert
            Assert.Equal(originalVars.Count, processedVars.Count);

            for (int i = 0; i < originalVars.Count; i++)
            {
                Assert.Equal(originalVars[i].Name, processedVars[i].Name);
                Assert.Equal(originalVars[i].Value, processedVars[i].Value);
            }
        }

        [Fact]
        public void FormatServiceDependencies_ShouldReturnEmpty_WhenInputIsNull()
        {
            var result = StringHelper.FormatServiceDependencies(null);
            Assert.Empty(result);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("singleDep", "singleDep")]
        [InlineData("dep1;dep2;dep3", "dep1|dep2|dep3")] // Use a temporary pipe delimiter for tokenization
        public void FormatServiceDependencies_ShouldReplaceSemicolonWithNewLine(string input, string expectedTemplate)
        {
            // Arrange
            // Dynamically construct the cross-platform expected string if the token template is detected
            string expected = expectedTemplate.Contains("|")
                ? expectedTemplate.Replace("|", Environment.NewLine)
                : expectedTemplate;

            // Act
            var result = StringHelper.FormatServiceDependencies(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Escape_NullInput_ReturnsEmptyString()
        {
            // Act
            var result = StringHelper.Escape(null);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void FormatEnvironmentVariables_ShouldEscapeSpecialCharactersCorrectly()
        {
            // Arrange
            // Each variable tests one or more escape sequences:
            // - VAR1: normal, no escaping
            // - VAR2: '=' in value
            // - VAR3: ';' in value
            // - VAR4: '"' in value
            // - VAR5: '\' in value
            // - VAR6: combinations of multiple escaped chars
            // - VAR7: escaped '=' in the KEY (not the value)
            var rawVars = string.Join(";", new[]
            {
                "VAR1=val1",
                @"VAR2=a\=b",
                @"VAR3=x\;y",
                @"VAR4=hello \""world\""",
                @"VAR5=C:\path\to\file",
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

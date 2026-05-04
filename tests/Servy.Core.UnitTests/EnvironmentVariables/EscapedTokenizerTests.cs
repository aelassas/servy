using System;
using System.Linq;
using Servy.Core.EnvironmentVariables;
using Xunit;

namespace Servy.Core.UnitTests.EnvironmentVariables
{
    /// <summary>
    /// Contains comprehensive unit tests for the <see cref="EscapedTokenizer"/> class.
    /// Ensures all branching paths for splitting, indexing, counting, and unescaping are verified.
    /// </summary>
    public class EscapedTokenizerTests
    {
        #region SplitByUnescapedDelimiters Tests

        /// <summary>
        /// Verifies that <see cref="EscapedTokenizer.SplitByUnescapedDelimiters"/> correctly splits
        /// when delimiters are not escaped.
        /// </summary>
        [Fact]
        public void SplitByUnescapedDelimiters_NoEscape_SplitsCorrectly()
        {
            // Arrange
            string input = "key1=val1;key2=val2";
            char[] delimiters = { ';' };

            // Act
            var result = EscapedTokenizer.SplitByUnescapedDelimiters(input, delimiters);

            // Assert
            Assert.Equal(2, result.Length);
            Assert.Equal("key1=val1", result[0]);
            Assert.Equal("key2=val2", result[1]);
        }

        /// <summary>
        /// Verifies that delimiters preceded by an odd number of backslashes are not treated as split points.
        /// </summary>
        [Theory]
        [InlineData("key1=val1\\;stillKey1", 1)] // Escaped semicolon
        [InlineData("key1=val1\\\\;key2=val2", 2)] // Even backslashes (unescaped semicolon)
        [InlineData("key1=val1\\\\\\;stillKey1", 1)] // Triple backslashes (escaped semicolon)
        public void SplitByUnescapedDelimiters_WithEscapes_RespectsOddEvenBackslashes(string input, int expectedCount)
        {
            // Arrange
            char[] delimiters = { ';' };

            // Act
            var result = EscapedTokenizer.SplitByUnescapedDelimiters(input, delimiters);

            // Assert
            Assert.Equal(expectedCount, result.Length);
        }

        /// <summary>
        /// Verifies that newlines and carriage returns can be used as unescaped delimiters or escaped to keep segments together.
        /// </summary>
        [Fact]
        public void SplitByUnescapedDelimiters_EscapedNewlines_KeepsSegmentsTogether()
        {
            // Arrange
            string input = "line1\\\nline2"; // Literal '\' followed by LF
            char[] delimiters = { '\n' };

            // Act
            var result = EscapedTokenizer.SplitByUnescapedDelimiters(input, delimiters);

            // Assert
            Assert.Single(result);
            Assert.Equal("line1\\\nline2", result[0]);
        }

        #endregion

        #region IndexOfUnescapedChar Tests

        /// <summary>
        /// Verifies that <see cref="EscapedTokenizer.IndexOfUnescapedChar"/> finds the correct index
        /// only when the character is not escaped.
        /// </summary>
        [Theory]
        [InlineData("a=b", '=', 1)]
        [InlineData("a\\=b", '=', -1)]
        [InlineData("a\\\\=b", '=', 3)]
        [InlineData("abc", '=', -1)]
        public void IndexOfUnescapedChar_BranchCoverage_ReturnsExpectedIndex(string input, char ch, int expected)
        {
            // Act
            int result = EscapedTokenizer.IndexOfUnescapedChar(input, ch);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region CountUnescapedChar Tests

        /// <summary>
        /// Verifies that <see cref="EscapedTokenizer.CountUnescapedChar"/> only increments for unescaped occurrences.
        /// </summary>
        [Theory]
        [InlineData("a;b;c", ';', 2)]
        [InlineData("a\\;b;c", ';', 1)]
        [InlineData("a\\;b\\;c", ';', 0)]
        [InlineData("\\\\;\\\\;", ';', 2)] // Even backslashes are unescaped
        public void CountUnescapedChar_BranchCoverage_ReturnsCorrectCount(string input, char ch, int expected)
        {
            // Act
            int result = EscapedTokenizer.CountUnescapedChar(input, ch);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region Unescape Tests

        /// <summary>
        /// Verifies that <see cref="EscapedTokenizer.Unescape"/> handles null or empty inputs gracefully.
        /// </summary>
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        public void Unescape_EmptyInput_ReturnsEmptyString(string? input, string expected)
        {
            // Act
            var result = EscapedTokenizer.Unescape(input!);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifies the fix for #1114, ensuring escaped newlines and carriage returns are properly unescaped.
        /// </summary>
        /// <summary>
        /// Verifies the fix for #1114, ensuring literal newlines and carriage returns 
        /// preceded by an escape character have the escape backslash stripped.
        /// </summary>
        [Theory]
        [InlineData("val1\\;val2", "val1;val2")]
        [InlineData("val1\\=val2", "val1=val2")]
        [InlineData("val1\\\\val2", "val1\\val2")]
        [InlineData("val1\\\"val2", "val1\"val2")]
        // FIX: Using literal newlines/CRs to match the logic in EscapedTokenizer.cs
        [InlineData("line1\\\nline2", "line1\nline2")] // Literal backslash + actual LF
        [InlineData("line1\\\rline2", "line1\rline2")] // Literal backslash + actual CR
        public void Unescape_KnownEscapes_StripsBackslash(string input, string expected)
        {
            // Act
            var result = EscapedTokenizer.Unescape(input);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifies that backslashes followed by characters not in the unescape switch are preserved literally.
        /// </summary>
        [Theory]
        [InlineData("path\\to\\file", "path\\to\\file")]
        [InlineData("escape\\x", "escape\\x")]
        public void Unescape_UnknownEscapes_PreservesBackslash(string input, string expected)
        {
            // Act
            var result = EscapedTokenizer.Unescape(input);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifies that a trailing backslash is preserved literally.
        /// </summary>
        [Fact]
        public void Unescape_TrailingBackslash_PreservesBackslash()
        {
            // Arrange
            string input = "trailing\\";

            // Act
            var result = EscapedTokenizer.Unescape(input);

            // Assert
            Assert.Equal("trailing\\", result);
        }

        #endregion
    }
}
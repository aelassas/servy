using Servy.Core.EnvironmentVariables;
using System.Linq;
using Xunit;

namespace Servy.Core.UnitTests.EnvironmentVariables
{
    /// <summary>
    /// Contains comprehensive unit tests for the <see cref="EscapedTokenizer"/> class.
    /// Ensures all branching paths for splitting, indexing, counting, and unescaping are verified.
    /// </summary>
    public class EscapedTokenizerTests
    {
        private static readonly char[] Delimiters = new[] { ';', '\r', '\n' };

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
        public void Unescape_EmptyInput_ReturnsEmptyString(string input, string expected)
        {
            // Act
            var result = EscapedTokenizer.Unescape(input);

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
        // Using literal newlines/CRs to match the logic in EscapedTokenizer.cs
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

        /// <summary>
        /// Validates that SplitByUnescapedDelimiters treats standard backslash-escaped 
        /// CR and LF control characters as part of the token value rather than splitting records on them.
        /// </summary>
        [Fact]
        public void SplitByUnescapedDelimiters_LiteralEscapedControlLineBreaks_DoesNotSplit()
        {
            // Arrange
            // Explicitly embedding literal escaped control character sequences into a single structural block
            string input = "KEY1\\=value1\\;contains\\\r\\\ncontinued;KEY2\\=value2";

            // Act
            var tokens = EscapedTokenizer.SplitByUnescapedDelimiters(input, Delimiters)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            // Assert
            // The unescaped semicolon should break this into 2 records; the escaped CR/LF must stay internal
            Assert.Equal(2, tokens.Count);
            Assert.Contains("KEY1\\=value1\\;contains\\\r\\\ncontinued", tokens[0]);
            Assert.Equal("KEY2\\=value2", tokens[1]);
        }

        /// <summary>
        /// Verifies that Unescape preserves and flushes a literal CR or LF control character when 
        /// it acts as a line continuation directly following an escaping backslash sequence.
        /// </summary>
        [Theory]
        [InlineData("ValueWith\\\rContinuation", "ValueWith\rContinuation")]
        [InlineData("ValueWith\\\nContinuation", "ValueWith\nContinuation")]
        [InlineData("ValueWith\\\r\\\nDoubleContinuation", "ValueWith\r\nDoubleContinuation")]
        public void Unescape_LiteralControlLineBreakContinuations_PreservesControlBytes(string input, string expected)
        {
            // Act
            string result = EscapedTokenizer.Unescape(input);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Ensures that trailing escape symbols at the absolute bounds of an input boundary string 
        /// fail closed and preserve the structural symbol literally instead of truncating or throwing.
        /// </summary>
        [Fact]
        public void Unescape_TrailingEscapeAtStringBounds_PreservesBackslash()
        {
            // Arrange
            string input = "MalformedValue\\";

            // Act
            string result = EscapedTokenizer.Unescape(input);

            // Assert
            Assert.Equal("MalformedValue\\", result);
        }

        /// <summary>
        /// Validates IndexOfUnescapedChar boundary evaluation rules, ensuring that a target character 
        /// is ignored if it is preceded by an active escaping operator but detected normally otherwise.
        /// </summary>
        [Fact]
        public void IndexOfUnescapedChar_EscapedVsUnescapedTargets_LocatesCorrectIndex()
        {
            // Arrange
            // First '=' is hidden behind an escape; second '=' is structurally active
            string input = "PREFIX\\=HIDDEN=VALID_VALUE";

            // Act
            int index = EscapedTokenizer.IndexOfUnescapedChar(input, '=');

            // Assert
            // "PREFIX\=HIDDEN" is 14 chars long; index of unescaped '=' should be 14
            Assert.Equal(14, index);
            Assert.Equal('=', input[index]);
        }

        #endregion
    }
}
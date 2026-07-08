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
        /// Verifies that delimiters preceded by an odd number of backslashes are not treated as split points,
        /// and guarantees backslash run sequences remain preserved within the extracted token segments.
        /// </summary>
        [Theory]
        [InlineData("key1=val1\\;stillKey1", new[] { "key1=val1\\;stillKey1" })] // Escaped semicolon -> single segment
        [InlineData("key1=val1\\\\;key2=val2", new[] { "key1=val1\\\\", "key2=val2" })] // Even backslashes -> unescaped split
        [InlineData("key1=val1\\\\\\;stillKey1", new[] { "key1=val1\\\\\\;stillKey1" })] // Triple backslashes -> escaped single segment
        public void SplitByUnescapedDelimiters_WithEscapes_RespectsOddEvenBackslashes(string input, string[] expectedSegments)
        {
            // Arrange
            char[] delimiters = { ';' };

            // Act
            var result = EscapedTokenizer.SplitByUnescapedDelimiters(input, delimiters);

            // Assert
            Assert.Equal(expectedSegments.Length, result.Length);
            Assert.Equal(expectedSegments, result);
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

        /// <summary>
        /// CONSOLIDATION VARIATION: Ported and unified from internal parser/validator test classes
        /// to comprehensively cover boundary limits, single/multiple backslash loops, and index shifts.
        /// </summary>
        [Theory]
        // No delimiter
        [InlineData("abc", new[] { '=' }, new[] { "abc" })]
        // Unescaped delimiter
        [InlineData("a=b", new[] { '=' }, new[] { "a", "b" })]
        // Escaped delimiter (odd backslashes)
        [InlineData(@"a\=b", new[] { '=' }, new[] { @"a\=b" })]
        // Even backslashes -> Delimiter remains unescaped
        [InlineData(@"a\\=b", new[] { '=' }, new[] { @"a\\", "b" })]
        // Multiple unescaped delimiters
        [InlineData("a=b=c", new[] { '=' }, new[] { "a", "b", "c" })]
        // Trailing delimiter
        [InlineData("a=", new[] { '=' }, new[] { "a", "" })]
        // Delimiter at index 0 (j < 0 loop path check)
        [InlineData("=a", new[] { '=' }, new[] { "", "a" })]
        // Loop runs multiple times (triple backslashes)
        [InlineData(@"a\\\=b", new[] { '=' }, new[] { @"a\\\=b" })]
        // Multiple variations of delimiters mixed together
        [InlineData(@"a=b\;c;d", new[] { '=', ';' }, new[] { "a", @"b\;c", "d" })]
        public void SplitByUnescapedDelimiters_ConsolidatedMatrix_VaryingDelimitersAndEscapes(string input, char[] delimiters, string[] expected)
        {
            // Arrange & Act
            var result = EscapedTokenizer.SplitByUnescapedDelimiters(input, delimiters);

            // Assert
            Assert.Equal(expected, result);
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
            // Arrange & Act
            int result = EscapedTokenizer.IndexOfUnescapedChar(input, ch);

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
            // Arrange & Act
            var result = EscapedTokenizer.Unescape(input);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifies the fix for #1114, ensuring literal newlines and carriage returns 
        /// preceded by an escape character have the escape backslash stripped.
        /// </summary>
        [Theory]
        [InlineData("val1\\;val2", "val1;val2")]
        [InlineData("val1\\=val2", "val1=val2")]
        [InlineData("val1\\\\val2", "val1\\val2")]
        [InlineData("val1\\\"val2", "val1\"val2")]
        [InlineData("line1\\\nline2", "line1\nline2")] // Literal backslash + actual LF
        [InlineData("line1\\\rline2", "line1\rline2")] // Literal backslash + actual CR
        // DUPLICATION FIX: Folded the unique sequential consecutive \r\n double-continuation path directly into 
        // this method matrix to entirely drop the redundant Unescape_LiteralControlLineBreakContinuations method.
        [InlineData("ValueWith\\\r\\\nDoubleContinuation", "ValueWith\r\nDoubleContinuation")]
        public void Unescape_KnownEscapes_StripsBackslash(string input, string expected)
        {
            // Arrange & Act
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
            // Arrange & Act
            var result = EscapedTokenizer.Unescape(input);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Ensures that trailing escape symbols at the absolute bounds of an input boundary string 
        /// fail closed and preserve the structural symbol literally instead of truncating or throwing out-of-bounds exceptions.
        /// </summary>
        [Theory]
        [InlineData("trailing\\", "trailing\\")]
        [InlineData("MalformedValue\\", "MalformedValue\\")]
        public void Unescape_TrailingBackslashAtStringBounds_PreservesBackslash(string input, string expected)
        {
            // Arrange & Act
            var result = EscapedTokenizer.Unescape(input);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Validates that SplitByUnescapedDelimiters treats standard backslash-escaped 
        /// CR and LF control characters as part of the token value rather than splitting records on them.
        /// </summary>
        [Fact]
        public void SplitByUnescapedDelimiters_KeepsEscapedControlLineBreaksInternal()
        {
            // Arrange
            string input = "KEY1\\=value1\\;contains\\\r\\\ncontinued;KEY2\\=value2";

            // Act
            var tokens = EscapedTokenizer.SplitByUnescapedDelimiters(input, Delimiters)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            // Assert
            Assert.Equal(2, tokens.Count);
            Assert.Equal("KEY1\\=value1\\;contains\\\r\\\ncontinued", tokens[0]);
            Assert.Equal("KEY2\\=value2", tokens[1]);
        }

        /// <summary>
        /// Validates IndexOfUnescapedChar boundary evaluation rules, ensuring that a target character 
        /// is ignored if it is preceded by an active escaping operator but detected normally otherwise.
        /// </summary>
        [Fact]
        public void IndexOfUnescapedChar_EscapedVsUnescapedTargets_LocatesCorrectIndex()
        {
            // Arrange
            string input = "PREFIX\\=HIDDEN=VALID_VALUE";

            // Act
            int index = EscapedTokenizer.IndexOfUnescapedChar(input, '=');

            // Assert
            Assert.Equal(14, index);
            Assert.Equal('=', input[index]);
        }

        #endregion
    }
}
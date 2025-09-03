using Servy.Core.Helpers;

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
        [InlineData("line1\r\nline2\nline3\rline4", "line1;line2;line3;line4")]
        // New cases for trailing backslash before line breaks
        [InlineData("VAR1=C:\\Path\\\r\nVAR2=Next", "VAR1=C:\\Path\\\\;VAR2=Next")]
        [InlineData("VAR1=C:\\Path\\\nVAR2=Next", "VAR1=C:\\Path\\\\;VAR2=Next")]
        [InlineData("VAR1=C:\\Path\\\rVAR2=Next", "VAR1=C:\\Path\\\\;VAR2=Next")]
        // New case: ends with a backslash (no line break)
        [InlineData("C:\\Path\\", "C:\\Path\\")]
        // New case: multiple variables with trailing backslash
        [InlineData("PATH=C:\\Windows\\System32\\\r\nTEMP=C:\\Temp", "PATH=C:\\Windows\\System32\\\\;TEMP=C:\\Temp")]
        [InlineData("PATH=C:\\Windows\\System32\\\\\r\nTEMP=C:\\Temp", "PATH=C:\\Windows\\System32\\\\\\\\;TEMP=C:\\Temp")]
        public void NormalizeString_ShouldNormalizeCorrectly(string input, string expected)
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
        public void FormatEnvirnomentVariables_ShouldFormatParsedVariables()
        {
            // Arrange
            var rawVars = "VAR1=val1;VAR2=val2";

            // Act
            var result = StringHelper.FormatEnvirnomentVariables(rawVars);

            // Assert
            var expected = "VAR1=val1" + Environment.NewLine + "VAR2=val2";
            Assert.Equal(expected, result);
        }
    }
}

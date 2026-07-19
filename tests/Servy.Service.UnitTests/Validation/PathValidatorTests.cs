using Servy.Service.Validation;
using Xunit;

namespace Servy.Service.UnitTests.Validation
{
    public class PathValidatorTests
    {
        [Theory]
        [InlineData(@"C:\Valid\Path.txt", true)]
        [InlineData(@"..\Traversal.txt", false)] // Directory traversal
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData(@"C:\Invalid|Char.txt", false)] // Invalid path chars
        public void IsValidPath_EvaluatesCorrectly(string path, bool expected)
        {
            var validator = new PathValidator();
            Assert.Equal(expected, validator.IsValidPath(path));
        }
    }
}
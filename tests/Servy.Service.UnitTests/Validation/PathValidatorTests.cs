using Servy.Service.Validation;

namespace Servy.Service.UnitTests.Validation
{
    public class PathValidatorTests
    {
        /// <summary>
        /// Smoke tests verifying that <see cref="PathValidator"/> correctly delegates path validation to <see cref="Core.Helpers.Helper.IsValidPath"/>.
        /// Exhaustive validation rule-set coverage is owned by <c>HelperTests.IsValidPath_VariousInputs_ReturnsExpected</c>.
        /// </summary>
        [Theory]
        [InlineData(@"C:\Valid\Path.txt", true)]
        [InlineData(@"C:\my..folder\path", true)] // Tests proper path-normalization delegation vs naive substring traversal checks
        [InlineData(@"..\Traversal.txt", false)]  // Directory traversal
        public void IsValidPath_EvaluatesCorrectly(string path, bool expected)
        {
            var validator = new PathValidator();
            Assert.Equal(expected, validator.IsValidPath(path));
        }
    }
}

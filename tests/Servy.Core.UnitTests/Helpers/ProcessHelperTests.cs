using Servy.Core.Helpers;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class ProcessHelperTests
    {
        [Theory]
        [InlineData(1.0, "1%")]       // integer case
        [InlineData(1.1, "1.10%")]    // one decimal → force two decimals
        [InlineData(1.05, "1.05%")]   // two decimals
        [InlineData(1.23, "1.23%")]   // two decimals
        [InlineData(1.234, "1.23%")]  // rounding down
        [InlineData(1.235, "1.24%")]  // rounding up
        [InlineData(1.236, "1.24%")]  // rounding up
        public void FormatCPUUsage_ReturnsExpected(double input, string expected)
        {
            var result = ProcessHelper.FormatCPUUsage(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(512, "512 B")]                // < KB
        [InlineData(2048, "2 KB")]                // exact KB
        [InlineData(3072, "3 KB")]                // KB range
        [InlineData(1048576, "1 MB")]             // exact MB
        [InlineData((1.5 * 1024 * 1024), "1.5 MB")] // MB range
        [InlineData(1073741824, "1 GB")]          // exact GB
        [InlineData((2.25 * 1024 * 1024 * 1024), "2.25 GB")] // GB range
        [InlineData(1099511627776, "1 TB")]       // exact TB
        [InlineData((3.75 * 1024 * 1024 * 1024 * 1024), "3.75 TB")] // TB range
        public void FormatRAMUsage_ReturnsExpected(long input, string expected)
        {
            var result = ProcessHelper.FormatRAMUsage(input);
            Assert.Equal(expected, result);
        }
    }
}

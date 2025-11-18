using Servy.Core.Helpers;
using Xunit;

namespace Servy.Core.UnitTests.Helpers
{
    public class ProcessHelperTests
    {
        [Theory]
        [InlineData(0, "0%")]        // zero case
        [InlineData(0.03, "0%")]     // zero case
        [InlineData(1.0, "1.0%")]    // integer case
        [InlineData(1.04, "1.0%")]   // two decimals
        [InlineData(1.05, "1.1%")]   // two decimals
        [InlineData(1.06, "1.1%")]   // two decimals
        [InlineData(1.1, "1.1%")]    // one decimal -> force two decimals
        [InlineData(1.23, "1.2%")]   // two decimals
        [InlineData(1.34, "1.3%")]   // rounding down
        [InlineData(1.35, "1.4%")]   // rounding up
        [InlineData(1.36, "1.4%")]   // rounding up
        public void FormatCPUUsage_ReturnsExpected(double input, string expected)
        {
            var result = ProcessHelper.FormatCpuUsage(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(512, "512.0 B")]                // < KB
        [InlineData(2048, "2.0 KB")]                // exact KB
        [InlineData(3072, "3.0 KB")]                // KB range
        [InlineData(1048576, "1.0 MB")]             // exact MB
        [InlineData((1.5 * 1024 * 1024), "1.5 MB")] // MB range
        [InlineData(1073741824, "1.0 GB")]          // exact GB
        [InlineData((2.23 * 1024 * 1024 * 1024), "2.2 GB")] // GB range
        [InlineData((2.25 * 1024 * 1024 * 1024), "2.3 GB")] // GB range
        [InlineData(1099511627776, "1.0 TB")]       // exact TB
        [InlineData((3.75 * 1024 * 1024 * 1024 * 1024), "3.8 TB")] // TB range
        public void FormatRAMUsage_ReturnsExpected(long input, string expected)
        {
            var result = ProcessHelper.FormatRamUsage(input);
            Assert.Equal(expected, result);
        }
    }
}

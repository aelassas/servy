using Servy.UI.Helpers;
using System;
using Xunit;

namespace Servy.UI.UnitTests.Helpers
{
    public class HelperTests
    {
        // ---------- FormatDuration ----------

        [Fact]
        public void FormatDuration_Hours_ReturnsHoursMinutesSeconds()
        {
            var ts = new TimeSpan(1, 2, 3); // 1h 2m 3s
            var result = Helper.FormatDuration(ts);
            Assert.Equal("1h 2m 3s", result);
        }

        [Fact]
        public void FormatDuration_Minutes_ReturnsMinutesSeconds()
        {
            var ts = new TimeSpan(0, 2, 5); // 2m 5s
            var result = Helper.FormatDuration(ts);
            Assert.Equal("2m 5s", result);
        }

        [Fact]
        public void FormatDuration_Seconds_ReturnsSecondsAndMilliseconds()
        {
            var ts = TimeSpan.FromMilliseconds(5250); // 5.25s
            var result = Helper.FormatDuration(ts);
            Assert.Equal("5s 250ms", result);
        }

        [Fact]
        public void FormatDuration_OnlySecondsNoMilliseconds()
        {
            var ts = TimeSpan.FromSeconds(10); // 10s exactly
            var result = Helper.FormatDuration(ts);
            Assert.Equal("10s 0ms", result);
        }

        [Fact]
        public void FormatDuration_MillisecondsOnly_ReturnsMilliseconds()
        {
            var ts = TimeSpan.FromMilliseconds(500);
            var result = Helper.FormatDuration(ts);
            Assert.Equal("500ms", result);
        }

        [Fact]
        public void FormatDuration_ZeroMilliseconds_Returns0ms()
        {
            var ts = TimeSpan.Zero;
            var result = Helper.FormatDuration(ts);
            Assert.Equal("0ms", result);
        }

        // ---------- FormatNumber ----------

        [Fact]
        public void FormatNumber_FormatsWithThousandsSeparator()
        {
            var result = Helper.FormatNumber(1234567);
            Assert.Equal("1,234,567", result);
        }

        [Fact]
        public void FormatNumber_FormatsSmallNumberWithoutSeparator()
        {
            var result = Helper.FormatNumber(42);
            Assert.Equal("42", result);
        }

        // ---------- GetRowsInfo ----------

        [Fact]
        public void GetRowsInfo_NoRows_ReturnsCorrectMessage()
        {
            var ts = TimeSpan.FromSeconds(2);
            var result = Helper.GetRowsInfo(0, ts, "item");
            Assert.Equal("No items loaded in 2s 0ms", result);
        }

        [Fact]
        public void GetRowsInfo_SingleRow_ReturnsCorrectMessage()
        {
            var ts = TimeSpan.FromSeconds(5.5);
            var result = Helper.GetRowsInfo(1, ts, "item");
            Assert.Equal("Loaded 1 item in 5s 500ms", result);
        }

        [Fact]
        public void GetRowsInfo_MultipleRows_ReturnsCorrectMessage()
        {
            var ts = new TimeSpan(0, 1, 20); // 1m 20s
            var result = Helper.GetRowsInfo(1234, ts, "item");
            Assert.Equal("Loaded 1,234 items in 1m 20s", result);
        }
    }
}

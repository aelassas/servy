using Servy.Testing;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace Servy.UI.IntegrationTests.Helpers
{
    public class HelperTests
    {
        #region GetVisualChild Tests

        [Fact]
        public async Task GetVisualChild_NoChildren_ReturnsNull()
        {
            await Helper.RunInSTAContext(async () =>
            {
                // Branch: Loop i < GetChildrenCount (zero count)
                var border = new Border();

                // Force layout generation
                border.Measure(new Size(100, 100));
                border.Arrange(new Rect(0, 0, 100, 100));

                var result = UI.Helpers.Helper.GetVisualChild<ScrollViewer>(border);
                Assert.Null(result);
            });
        }

        [Fact]
        public async Task GetVisualChild_ImmediateChildFound_ReturnsChild()
        {
            await Helper.RunInSTAContext(async () =>
            {
                // Branch: if (child is T t) return t;
                var grid = new Grid();
                var scrollViewer = new ScrollViewer();
                grid.Children.Add(scrollViewer);

                // Force WPF to build the visual tree in memory
                grid.Measure(new Size(100, 100));
                grid.Arrange(new Rect(0, 0, 100, 100));
                grid.UpdateLayout();

                var result = UI.Helpers.Helper.GetVisualChild<ScrollViewer>(grid);

                Assert.NotNull(result);
                Assert.Same(scrollViewer, result);
            });
        }

        [Fact]
        public async Task GetVisualChild_DeepChildFound_ReturnsChild()
        {
            await Helper.RunInSTAContext(async () =>
            {
                // Branch: var res = GetVisualChild<T>(child); if (res != null) return res;
                var grid = new Grid();
                var border = new Border();
                var scrollViewer = new ScrollViewer();

                grid.Children.Add(border);
                border.Child = scrollViewer;

                // Force WPF to build the deep visual tree in memory
                grid.Measure(new Size(100, 100));
                grid.Arrange(new Rect(0, 0, 100, 100));
                grid.UpdateLayout();

                var result = UI.Helpers.Helper.GetVisualChild<ScrollViewer>(grid);

                Assert.NotNull(result);
                Assert.Same(scrollViewer, result);
            });
        }

        #endregion

        #region FormatDuration Tests

        [Theory]
        [InlineData(0, 0, 0, 0, "0ms")]                  // Branch: parts.Count == 0
        [InlineData(0, 0, 0, 500, "500ms")]              // Branch: Milliseconds > 0
        [InlineData(0, 0, 30, 0, "30s")]                 // Branch: Seconds > 0
        [InlineData(0, 45, 0, 0, "45m")]                 // Branch: Minutes > 0
        [InlineData(5, 0, 0, 0, "5h")]                   // Branch: TotalHours > 0
        [InlineData(26, 10, 5, 1, "26h 10m 5s 1ms")]     // Branch: All components > 0
        public void FormatDuration_VariousTimeSpans_ReturnsExpectedFormat(
            int hours, int minutes, int seconds, int milliseconds, string expected)
        {
            var duration = new TimeSpan(0, hours, minutes, seconds, milliseconds);
            var result = UI.Helpers.Helper.FormatDuration(duration);
            Assert.Equal(expected, result);
        }

        #endregion

        #region FormatNumber Tests

        [Theory]
        [InlineData(123, "123")]
        [InlineData(1234, "1,234")]
        [InlineData(1000000, "1,000,000")]
        public void FormatNumber_Integers_ReturnsInvariantCultureFormatting(int number, string expected)
        {
            var result = UI.Helpers.Helper.FormatNumber(number);
            Assert.Equal(expected, result);
        }

        #endregion

        #region GetRowsInfo Tests

        private const string None = "None: {0}";
        private const string One = "One: {0}";
        private const string Many = "Many: {0} {1}";

        [Fact]
        public void GetRowsInfo_CountZero_ReturnsNoneFormat()
        {
            // Branch: if (count == 0)
            var result = UI.Helpers.Helper.GetRowsInfo(0, TimeSpan.FromSeconds(1), None, One, Many);
            Assert.Equal("None: 1s", result);
        }

        [Fact]
        public void GetRowsInfo_CountOne_ReturnsOneFormat()
        {
            // Branch: if (count == 1)
            var result = UI.Helpers.Helper.GetRowsInfo(1, TimeSpan.FromSeconds(1), None, One, Many);
            Assert.Equal("One: 1s", result);
        }

        [Fact]
        public void GetRowsInfo_CountMany_ReturnsManyFormat()
        {
            // Branch: Default/Many
            var result = UI.Helpers.Helper.GetRowsInfo(1234, TimeSpan.FromSeconds(1), None, One, Many);
            Assert.Equal("Many: 1,234 1s", result);
        }

        #endregion
    }
}
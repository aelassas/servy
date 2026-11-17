using Servy.Manager.Utils;
using Servy.Testing;
using System.Windows;
using System.Windows.Controls;

namespace Servy.Manager.UnitTests.Utils
{
    /// <summary>
    /// Tests for <see cref="DataGridColumnWidths"/>, which applies, captures and resets the persisted
    /// column widths of a <see cref="DataGrid"/>.
    /// </summary>
    public class DataGridColumnWidthsTests
    {
        /// <summary>
        /// Builds a grid shaped like the Manager tables: a header-less fixed column, two resizable pixel
        /// columns, a column the user cannot resize and a star column.
        /// </summary>
        private static DataGrid CreateGrid()
        {
            var grid = new DataGrid { AutoGenerateColumns = false };
            grid.Columns.Add(new DataGridTextColumn { Width = new DataGridLength(32), CanUserResize = false });
            grid.Columns.Add(new DataGridTextColumn { Header = "Name", Width = new DataGridLength(120), MinWidth = 100 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Status", Width = new DataGridLength(90), MinWidth = 80, MaxWidth = 400 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Actions", Width = new DataGridLength(160), CanUserResize = false });
            grid.Columns.Add(new DataGridTextColumn { Header = "Message", Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 200 });
            return grid;
        }

        /// <summary>
        /// Asserts that a column has an absolute width of <paramref name="expected"/> device-independent units.
        /// </summary>
        private static void AssertPixelWidth(double expected, DataGridColumn column)
        {
            Assert.True(column.Width.IsAbsolute);
            Assert.Equal(expected, column.Width.Value);
        }

        [Fact]
        public void Apply_PersistedWidths_ResizesOnlyTheResizableNonStarColumnsWithAHeader()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var grid = CreateGrid();

                // Act
                DataGridColumnWidths.Apply(grid, "Name=250,Status=95.5,Actions=300,Message=500");

                // Assert
                AssertPixelWidth(32, grid.Columns[0]);
                AssertPixelWidth(250, grid.Columns[1]);
                AssertPixelWidth(95.5, grid.Columns[2]);
                AssertPixelWidth(160, grid.Columns[3]);
                Assert.True(grid.Columns[4].Width.IsStar);
            });
        }

        [Fact]
        public void Apply_PersistedWidthOutsideTheColumnLimits_IsLimitedToMinWidthAndMaxWidth()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var grid = CreateGrid();

                // Act
                DataGridColumnWidths.Apply(grid, "Name=10,Status=900");

                // Assert
                AssertPixelWidth(100, grid.Columns[1]);
                AssertPixelWidth(400, grid.Columns[2]);
            });
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("PID=60")]
        [InlineData("Name=abc")]
        public void Apply_NothingUsablePersistedForAColumn_KeepsItsWidth(string? persisted)
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var grid = CreateGrid();

                // Act
                DataGridColumnWidths.Apply(grid, persisted);

                // Assert
                AssertPixelWidth(120, grid.Columns[1]);
                AssertPixelWidth(90, grid.Columns[2]);
            });
        }

        [Fact]
        public void Capture_VisibleColumns_WritesTheirWidthsAndKeepsThePreviousWidthOfAHiddenColumn()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var grid = CreateGrid();
                grid.Columns[1].Width = new DataGridLength(250);
                grid.Columns[2].Visibility = Visibility.Collapsed;

                // Act
                var text = DataGridColumnWidths.Capture(grid, "Name=120,Status=300");

                // Assert: no header-less, non-resizable or star column; "Status" is hidden and keeps 300
                Assert.Equal("Name=250,Status=300", text);
            });
        }

        [Fact]
        public void Reset_AfterApply_GivesEveryColumnItsDeclaredWidthBack()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var grid = CreateGrid();
                var defaults = DataGridColumnWidths.CaptureDefaults(grid);
                DataGridColumnWidths.Apply(grid, "Name=250,Status=200");

                // Act
                DataGridColumnWidths.Reset(grid, defaults);

                // Assert
                AssertPixelWidth(32, grid.Columns[0]);
                AssertPixelWidth(120, grid.Columns[1]);
                AssertPixelWidth(90, grid.Columns[2]);
                AssertPixelWidth(160, grid.Columns[3]);
                Assert.True(grid.Columns[4].Width.IsStar);
            });
        }

        [Fact]
        public void Reset_ColumnAddedAfterTheDefaultsWereRecorded_IsLeftAsItIs()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var grid = CreateGrid();
                var defaults = DataGridColumnWidths.CaptureDefaults(grid);
                grid.Columns.Add(new DataGridTextColumn { Header = "Late", Width = new DataGridLength(75) });

                // Act
                DataGridColumnWidths.Reset(grid, defaults);

                // Assert
                AssertPixelWidth(75, grid.Columns[5]);
            });
        }

        [Fact]
        public void Methods_NullArguments_Throw()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var grid = CreateGrid();

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => DataGridColumnWidths.CaptureDefaults(null!));
                Assert.Throws<ArgumentNullException>(() => DataGridColumnWidths.Apply(null!, "Name=250"));
                Assert.Throws<ArgumentNullException>(() => DataGridColumnWidths.Capture(null!, null));
                Assert.Throws<ArgumentNullException>(() => DataGridColumnWidths.Reset(null!, DataGridColumnWidths.CaptureDefaults(grid)));
                Assert.Throws<ArgumentNullException>(() => DataGridColumnWidths.Reset(grid, null!));
            });
        }
    }
}

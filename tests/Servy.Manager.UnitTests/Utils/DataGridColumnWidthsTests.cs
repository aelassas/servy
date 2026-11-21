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
        public void Capture_AutoSizedColumnThatWasNeverResized_IsLeftOut()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange: no Width, so the column sizes itself to its content, like Status or Startup Type
                var grid = CreateGrid();
                grid.Columns.Add(new DataGridTextColumn { Header = "Startup Type", MinWidth = 120 });

                // Act
                var text = DataGridColumnWidths.Capture(grid, null);

                // Assert
                Assert.Equal("Name=120,Status=90", text);
            });
        }

        [Fact]
        public void Apply_PersistedWidthForAnAutoSizedColumn_PinsItAndCaptureKeepsIt()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var grid = CreateGrid();
                grid.Columns.Add(new DataGridTextColumn { Header = "Startup Type", MinWidth = 120 });

                // Act
                DataGridColumnWidths.Apply(grid, "Startup Type=200");
                var text = DataGridColumnWidths.Capture(grid, "Startup Type=200");

                // Assert
                AssertPixelWidth(200, grid.Columns[5]);
                Assert.Equal("Name=120,Status=90,Startup Type=200", text);
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
        public void AddWidthChangedHandler_HandledColumnResized_CallsTheHandlerWithTheColumn()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var grid = CreateGrid();
                var senders = new List<object?>();
                DataGridColumnWidths.AddWidthChangedHandler(grid, (s, e) => senders.Add(s));

                // Act
                grid.Columns[1].Width = new DataGridLength(250);

                // Assert
                Assert.Equal(new object?[] { grid.Columns[1] }, senders);
            });
        }

        [Fact]
        public void AddWidthChangedHandler_ColumnThatIsNotPersistedResized_DoesNotCallTheHandler()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var grid = CreateGrid();
                var calls = 0;
                DataGridColumnWidths.AddWidthChangedHandler(grid, (s, e) => calls++);

                // Act: the header-less column and the one the user cannot resize
                grid.Columns[0].Width = new DataGridLength(50);
                grid.Columns[3].Width = new DataGridLength(200);

                // Assert
                Assert.Equal(0, calls);
            });
        }

        [Fact]
        public void Methods_ResizableColumnWithoutAHeaderText_AreLeftOut()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange: resizable pixel columns whose header is a control or an empty text
                var grid = CreateGrid();
                grid.Columns.Add(new DataGridTextColumn { Header = new CheckBox(), Width = new DataGridLength(40) });
                grid.Columns.Add(new DataGridTextColumn { Header = string.Empty, Width = new DataGridLength(50) });
                var calls = 0;
                DataGridColumnWidths.AddWidthChangedHandler(grid, (s, e) => calls++);

                // Act
                grid.Columns[5].Width = new DataGridLength(60);
                grid.Columns[6].Width = new DataGridLength(70);
                var text = DataGridColumnWidths.Capture(grid, null);

                // Assert: nothing was reported, nothing was captured, and Capture did not throw
                Assert.Equal(0, calls);
                Assert.Equal("Name=120,Status=90", text);
            });
        }

        [Fact]
        public void RemoveWidthChangedHandler_ColumnResizedAfterwards_DoesNotCallTheHandler()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var grid = CreateGrid();
                var calls = 0;
                EventHandler handler = (s, e) => calls++;
                DataGridColumnWidths.AddWidthChangedHandler(grid, handler);
                grid.Columns[1].Width = new DataGridLength(250);

                // Act
                DataGridColumnWidths.RemoveWidthChangedHandler(grid, handler);
                grid.Columns[1].Width = new DataGridLength(300);
                grid.Columns[2].Width = new DataGridLength(200);

                // Assert: only the change made before the removal was reported
                Assert.Equal(1, calls);
            });
        }

        [Fact]
        public void PinAutoFittedWidth_ColumnAutoFittedByADoubleClick_MakesItsWidthAbsoluteAndCallsTheHandler()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange: a double-click on a header gripper sets the width of the column to Auto
                var grid = CreateGrid();
                var column = grid.Columns[1];
                column.Width = DataGridLength.Auto;
                var fitted = column.ActualWidth;
                var senders = new List<object?>();

                // Act
                var pinned = DataGridColumnWidths.PinAutoFittedWidth(column, (s, e) => senders.Add(s), new Dictionary<DataGridColumn, DataGridLength>());
                var text = DataGridColumnWidths.Capture(grid, null);

                // Assert
                Assert.True(pinned);
                AssertPixelWidth(fitted, column);
                Assert.Equal(new object?[] { column }, senders);
                Assert.Equal(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Name={0:0.##},Status=90", fitted), text);
            });
        }

        [Fact]
        public void PinAutoFittedWidth_ColumnThatIsNotAutoSizedOrNotPersisted_IsLeftAsItIs()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange: a pixel column, the column the user cannot resize set to Auto, and the star column
                var grid = CreateGrid();
                grid.Columns[3].Width = DataGridLength.Auto;
                var calls = 0;
                EventHandler handler = (s, e) => calls++;
                var starColumns = new Dictionary<DataGridColumn, DataGridLength>();

                // Act
                var pinnedPixel = DataGridColumnWidths.PinAutoFittedWidth(grid.Columns[2], handler, starColumns);
                var pinnedNotResizable = DataGridColumnWidths.PinAutoFittedWidth(grid.Columns[3], handler, starColumns);
                var pinnedStar = DataGridColumnWidths.PinAutoFittedWidth(grid.Columns[4], handler, starColumns);

                // Assert
                Assert.False(pinnedPixel);
                Assert.False(pinnedNotResizable);
                Assert.False(pinnedStar);
                AssertPixelWidth(90, grid.Columns[2]);
                Assert.True(grid.Columns[3].Width.IsAuto);
                Assert.True(grid.Columns[4].Width.IsStar);
                Assert.Equal(0, calls);
            });
        }

        [Fact]
        public void PinAutoFittedWidth_StarColumnTheDoubleClickTurnedIntoAuto_GetsItsStarWidthBackAndIsNotPersisted()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange: the star column is recorded with its 2* width, then a double-click on its header gripper sets it to Auto
                var grid = CreateGrid();
                var message = grid.Columns[4];
                message.Width = new DataGridLength(2, DataGridLengthUnitType.Star);
                var starColumns = new Dictionary<DataGridColumn, DataGridLength> { { message, message.Width } };
                message.Width = DataGridLength.Auto;
                var calls = 0;

                // Act
                var pinned = DataGridColumnWidths.PinAutoFittedWidth(message, (s, e) => calls++, starColumns);
                var text = DataGridColumnWidths.Capture(grid, null);

                // Assert: the column fills the remaining width again, nothing was pinned and Message is not written
                Assert.False(pinned);
                Assert.True(message.Width.IsStar);
                Assert.Equal(2, message.Width.Value);
                Assert.Equal(0, calls);
                Assert.Equal("Name=120,Status=90", text);
            });
        }

        [Fact]
        public void GetColumnToFit_RightGripper_ReturnsTheColumnOfTheHeader()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var grid = CreateGrid();

                // Act
                var column = DataGridColumnWidths.GetColumnToFit(grid, grid.Columns[2], false);

                // Assert
                Assert.Same(grid.Columns[2], column);
            });
        }

        [Fact]
        public void GetColumnToFit_LeftGripper_ReturnsThePreviousVisibleColumn()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange: Status is hidden, so the left gripper of Actions resizes Name
                var grid = CreateGrid();
                grid.Columns[2].Visibility = Visibility.Collapsed;

                // Act
                var beforeStatus = DataGridColumnWidths.GetColumnToFit(grid, grid.Columns[2], true);
                var beforeActions = DataGridColumnWidths.GetColumnToFit(grid, grid.Columns[3], true);
                var beforeFirst = DataGridColumnWidths.GetColumnToFit(grid, grid.Columns[0], true);

                // Assert
                Assert.Same(grid.Columns[1], beforeStatus);
                Assert.Same(grid.Columns[1], beforeActions);
                Assert.Null(beforeFirst);
            });
        }

        [Fact]
        public void RemoveAutoFitHandler_AfterAddAutoFitHandler_UnregistersTheMouseHook()
        {
            Helper.RunOnSTA(() =>
            {
                // Arrange
                var grid = CreateGrid();
                var hookProperty = TestReflection.GetFieldStatic<DependencyProperty>(typeof(DataGridColumnWidths), "AutoFitHookProperty");
                DataGridColumnWidths.AddAutoFitHandler(grid, (s, e) => { });
                var hookAfterAdd = grid.GetValue(hookProperty);

                // Act
                DataGridColumnWidths.RemoveAutoFitHandler(grid);

                // Assert
                Assert.NotNull(hookAfterAdd);
                Assert.Null(grid.GetValue(hookProperty));
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
                Assert.Throws<ArgumentNullException>(() => DataGridColumnWidths.AddWidthChangedHandler(null!, (s, e) => { }));
                Assert.Throws<ArgumentNullException>(() => DataGridColumnWidths.AddWidthChangedHandler(grid, null!));
                Assert.Throws<ArgumentNullException>(() => DataGridColumnWidths.RemoveWidthChangedHandler(null!, (s, e) => { }));
                Assert.Throws<ArgumentNullException>(() => DataGridColumnWidths.RemoveWidthChangedHandler(grid, null!));
                Assert.Throws<ArgumentNullException>(() => DataGridColumnWidths.AddAutoFitHandler(null!, (s, e) => { }));
                Assert.Throws<ArgumentNullException>(() => DataGridColumnWidths.AddAutoFitHandler(grid, null!));
                Assert.Throws<ArgumentNullException>(() => DataGridColumnWidths.RemoveAutoFitHandler(null!));
            });
        }
    }
}

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Servy.Manager.Utils
{
    /// <summary>
    /// Applies, captures and resets the persisted column widths of a <see cref="DataGrid"/>.
    /// </summary>
    /// <remarks>
    /// Only the columns the user can resize and that carry a header text are handled, and each is identified
    /// by <see cref="ColumnIds.For(DataGridColumn)"/>, which does not change with the display language. A star-sized column
    /// is left alone on purpose: it keeps absorbing the space the other columns leave, exactly as it does
    /// with the default layout, so the grid still fills the window after a restore. An auto-sized column is
    /// captured only once its width is absolute (the user dragged it, or a persisted width was applied), so a
    /// column the user never resized keeps sizing itself to its content. A column the user auto-fits by
    /// double-clicking the edge of its header is given its fitted width as an absolute width, see
    /// <see cref="AddAutoFitHandler(DataGrid, EventHandler)"/>, so it is captured like a dragged column. A star-sized
    /// column the user double-clicks the same way gets its star width back instead, so it is never persisted.
    /// </remarks>
    public static class DataGridColumnWidths
    {
        /// <summary>
        /// The name of the thumb on the right edge of a column header, which resizes the column of that header.
        /// </summary>
        private const string RightHeaderGripperName = "PART_RightHeaderGripper";

        /// <summary>
        /// The name of the thumb on the left edge of a column header, which resizes the previous visible column.
        /// </summary>
        private const string LeftHeaderGripperName = "PART_LeftHeaderGripper";

        /// <summary>
        /// Attached to a grid by <see cref="AddAutoFitHandler(DataGrid, EventHandler)"/>: the mouse handler it
        /// registered, so <see cref="RemoveAutoFitHandler(DataGrid)"/> can unregister it.
        /// </summary>
        private static readonly DependencyProperty AutoFitHookProperty = DependencyProperty.RegisterAttached(
            "AutoFitHook", typeof(MouseButtonEventHandler), typeof(DataGridColumnWidths));

        /// <summary>
        /// Records the width every column of the grid was declared with, so
        /// <see cref="Reset(DataGrid, IReadOnlyDictionary{DataGridColumn, DataGridLength})"/> can bring it back.
        /// </summary>
        /// <param name="grid">The grid whose columns are recorded.</param>
        /// <returns>The declared width of each column. Call this before <see cref="Apply(DataGrid, string)"/>.</returns>
        public static IReadOnlyDictionary<DataGridColumn, DataGridLength> CaptureDefaults(DataGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            var defaults = new Dictionary<DataGridColumn, DataGridLength>();
            foreach (var column in grid.Columns)
            {
                defaults[column] = column.Width;
            }

            return defaults;
        }

        /// <summary>
        /// Sets each handled column to its persisted width.
        /// </summary>
        /// <param name="grid">The grid whose columns are resized.</param>
        /// <param name="persisted">The persisted <c>Header=Width</c> list, see <see cref="ColumnWidths"/>.</param>
        /// <remarks>
        /// A column without a persisted width keeps its current width. A persisted width is limited to the
        /// <see cref="DataGridColumn.MinWidth"/> and <see cref="DataGridColumn.MaxWidth"/> of its column.
        /// </remarks>
        public static void Apply(DataGrid grid, string? persisted)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            var widths = ColumnWidths.Parse(persisted);
            if (widths.Count == 0)
            {
                return;
            }

            foreach (var column in grid.Columns)
            {
                if (IsHandled(column) && ColumnWidths.TryGetWidth(widths, ColumnIds.For(column), out var width))
                {
                    column.Width = new DataGridLength(Math.Max(column.MinWidth, Math.Min(width, column.MaxWidth)));
                }
            }
        }

        /// <summary>
        /// Builds the <c>Header=Width</c> list to persist from the current width of each handled, visible column
        /// whose width is absolute.
        /// </summary>
        /// <param name="grid">The grid whose columns are measured.</param>
        /// <param name="previous">The currently persisted list; its entries for columns that are hidden now are kept.</param>
        /// <returns>The list to persist, see <see cref="ColumnWidths.Merge(string, IEnumerable{KeyValuePair{string, double}})"/>.</returns>
        public static string Capture(DataGrid grid, string? previous)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            var measured = new List<KeyValuePair<string, double>>();
            foreach (var column in grid.Columns)
            {
                if (IsHandled(column) && column.Width.IsAbsolute && column.Visibility == System.Windows.Visibility.Visible)
                {
                    measured.Add(new KeyValuePair<string, double>(ColumnIds.For(column)!, column.ActualWidth));
                }
            }

            return ColumnWidths.Merge(previous, measured);
        }

        /// <summary>
        /// Gives every column back the width it was declared with.
        /// </summary>
        /// <param name="grid">The grid whose columns are reset.</param>
        /// <param name="defaults">The widths recorded by <see cref="CaptureDefaults(DataGrid)"/>.</param>
        public static void Reset(DataGrid grid, IReadOnlyDictionary<DataGridColumn, DataGridLength> defaults)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (defaults == null) throw new ArgumentNullException(nameof(defaults));

            foreach (var column in grid.Columns)
            {
                if (defaults.TryGetValue(column, out var width))
                {
                    column.Width = width;
                }
            }
        }

        /// <summary>
        /// Calls <paramref name="handler"/> every time the width of a handled column of the grid changes,
        /// whether the user dragged its edge or the layout resized it.
        /// </summary>
        /// <param name="grid">The grid whose columns are watched.</param>
        /// <param name="handler">The handler to call. The sender is the column whose width changed.</param>
        /// <remarks>
        /// The handler is registered with the property descriptor of <see cref="DataGridColumn.ActualWidthProperty"/>,
        /// which lives as long as the process, not with the grid: the descriptor keeps every watched column and the
        /// handler alive until <see cref="RemoveWidthChangedHandler(DataGrid, EventHandler)"/> is called with the same handler.
        /// </remarks>
        public static void AddWidthChangedHandler(DataGrid grid, EventHandler handler)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var descriptor = GetActualWidthDescriptor();
            foreach (var column in grid.Columns)
            {
                if (IsHandled(column))
                {
                    descriptor.AddValueChanged(column, handler);
                }
            }
        }

        /// <summary>
        /// Stops the notifications started by <see cref="AddWidthChangedHandler(DataGrid, EventHandler)"/>.
        /// </summary>
        /// <param name="grid">The grid whose columns were watched.</param>
        /// <param name="handler">The handler to remove.</param>
        public static void RemoveWidthChangedHandler(DataGrid grid, EventHandler handler)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var descriptor = GetActualWidthDescriptor();
            foreach (var column in grid.Columns)
            {
                descriptor.RemoveValueChanged(column, handler);
            }
        }

        /// <summary>
        /// Persists the width of a column the user auto-fits by double-clicking the edge of its header: once the
        /// grid has fitted the column to its content, the fitted width is made absolute and
        /// <paramref name="handler"/> is called, like for a dragged column.
        /// </summary>
        /// <param name="grid">The grid whose column headers are watched.</param>
        /// <param name="handler">The handler to call. The sender is the auto-fitted column.</param>
        /// <remarks>
        /// A double-click on a header gripper sets the width of the column to <see cref="DataGridLength.Auto"/>, which
        /// <see cref="Capture(DataGrid, string)"/> leaves out. Only a column the user double-clicks is changed, so
        /// an auto-sized column the user never touched keeps sizing itself to its content. The star-sized columns and
        /// their widths are recorded when this method is called, so call it after <see cref="Apply(DataGrid, string)"/>:
        /// a recorded column the user double-clicks gets its star width back and <paramref name="handler"/> is not
        /// called. Calling this method again on the same grid replaces the previous handler.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="grid"/> or <paramref name="handler"/> is <see langword="null"/>.</exception>
        public static void AddAutoFitHandler(DataGrid grid, EventHandler handler)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            RemoveAutoFitHandler(grid);

            // A double-click turns a star column into an auto-sized one before the pin runs, so the star
            // columns are recorded now, while their declared width can still be read
            var starColumns = grid.Columns.Where(column => column.Width.IsStar).ToDictionary(column => column, column => column.Width);

            MouseButtonEventHandler hook = (sender, e) => OnPreviewMouseLeftButtonDown(grid, e, handler, starColumns);
            grid.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, hook, true);
            grid.SetValue(AutoFitHookProperty, hook);
        }

        /// <summary>
        /// Stops the notifications started by <see cref="AddAutoFitHandler(DataGrid, EventHandler)"/>.
        /// </summary>
        /// <param name="grid">The grid whose column headers were watched.</param>
        /// <exception cref="ArgumentNullException"><paramref name="grid"/> is <see langword="null"/>.</exception>
        public static void RemoveAutoFitHandler(DataGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            if (grid.GetValue(AutoFitHookProperty) is MouseButtonEventHandler hook)
            {
                grid.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, hook);
                grid.ClearValue(AutoFitHookProperty);
            }
        }

        /// <summary>
        /// Finds the column a double-click on a header gripper auto-fits, the same way <see cref="DataGridColumnHeader"/> does.
        /// </summary>
        /// <param name="grid">The grid that owns <paramref name="headerColumn"/>.</param>
        /// <param name="headerColumn">The column of the header whose gripper was double-clicked.</param>
        /// <param name="leftGripper">
        /// <see langword="true"/> for the gripper on the left edge of the header, <see langword="false"/> for the one on the right edge.
        /// </param>
        /// <returns>
        /// <paramref name="headerColumn"/> for the right gripper; for the left gripper the closest visible column displayed
        /// before it, or <see langword="null"/> when there is none.
        /// </returns>
        internal static DataGridColumn? GetColumnToFit(DataGrid grid, DataGridColumn headerColumn, bool leftGripper)
        {
            if (!leftGripper)
            {
                return headerColumn;
            }

            // Ordered by display index; OrderBy is stable, so a grid whose display order is not set up yet keeps its declared order
            var displayed = grid.Columns.OrderBy(column => column.DisplayIndex).ToList();
            for (var index = displayed.IndexOf(headerColumn) - 1; index >= 0; index--)
            {
                if (displayed[index].Visibility == Visibility.Visible)
                {
                    return displayed[index];
                }
            }

            return null;
        }

        /// <summary>
        /// Gives an auto-fitted column its current width as an absolute width and calls <paramref name="handler"/>,
        /// or gives a recorded star-sized column its star width back.
        /// </summary>
        /// <param name="column">The column the user auto-fitted.</param>
        /// <param name="handler">The handler to call when the width was made absolute. The sender is <paramref name="column"/>.</param>
        /// <param name="starColumns">
        /// The columns that were star-sized when <see cref="AddAutoFitHandler(DataGrid, EventHandler)"/> was called,
        /// with their star width. Such a column gets that width back, so it fills the remaining width again and is
        /// not persisted; <paramref name="handler"/> is not called for it.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the width was made absolute; <see langword="false"/> when the column is in
        /// <paramref name="starColumns"/>, is not persisted or its width is not <see cref="DataGridLengthUnitType.Auto"/>.
        /// </returns>
        internal static bool PinAutoFittedWidth(DataGridColumn column, EventHandler handler, IReadOnlyDictionary<DataGridColumn, DataGridLength> starColumns)
        {
            if (starColumns.TryGetValue(column, out var starWidth))
            {
                column.Width = starWidth;
                return false;
            }

            if (!IsHandled(column) || !column.Width.IsAuto)
            {
                return false;
            }

            column.Width = new DataGridLength(column.ActualWidth);
            handler(column, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Handles a mouse button press anywhere in a watched grid: on a double-click on a column header gripper,
        /// pins the width of the column it auto-fits once the grid has laid that column out again.
        /// </summary>
        /// <param name="grid">The watched grid.</param>
        /// <param name="e">The mouse event; its <see cref="RoutedEventArgs.OriginalSource"/> is the element pressed.</param>
        /// <param name="handler">The handler passed to <see cref="AddAutoFitHandler(DataGrid, EventHandler)"/>.</param>
        /// <param name="starColumns">The star-sized columns recorded by <see cref="AddAutoFitHandler(DataGrid, EventHandler)"/>, with their width.</param>
        /// <remarks>
        /// The header sets the width to <see cref="DataGridLength.Auto"/> after this preview handler runs, and the grid
        /// measures the fitted width in its next layout pass, so the width is read at
        /// <see cref="DispatcherPriority.Background"/>, after that pass.
        /// </remarks>
        private static void OnPreviewMouseLeftButtonDown(DataGrid grid, MouseButtonEventArgs e, EventHandler handler, IReadOnlyDictionary<DataGridColumn, DataGridLength> starColumns)
        {
            if (e.ClickCount != 2)
            {
                return;
            }

            var element = e.OriginalSource as DependencyObject;
            while (element != null && !(element is Thumb) && !(element is DataGrid))
            {
                element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
            }

            if (!(element is Thumb gripper)
                || !(gripper.TemplatedParent is DataGridColumnHeader header)
                || header.Column == null
                || (gripper.Name != RightHeaderGripperName && gripper.Name != LeftHeaderGripperName))
            {
                return;
            }

            var column = GetColumnToFit(grid, header.Column, gripper.Name == LeftHeaderGripperName);
            if (column != null)
            {
                grid.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => PinAutoFittedWidth(column, handler, starColumns)));
            }
        }

        /// <summary>
        /// Gets the descriptor used to observe <see cref="DataGridColumn.ActualWidth"/>.
        /// </summary>
        /// <returns>The property descriptor of <see cref="DataGridColumn.ActualWidthProperty"/>.</returns>
        private static DependencyPropertyDescriptor GetActualWidthDescriptor()
        {
            return DependencyPropertyDescriptor.FromProperty(DataGridColumn.ActualWidthProperty, typeof(DataGridColumn));
        }

        /// <summary>
        /// Determines whether the width of a column is persisted.
        /// </summary>
        /// <param name="column">The column to test.</param>
        /// <returns>
        /// <see langword="true"/> when the user can resize <paramref name="column"/>, it is not star-sized
        /// and its header is a non-empty text. <see cref="Capture(DataGrid, string)"/> also requires an absolute
        /// width, so an auto-sized column the user never resized is not persisted.
        /// </returns>
        private static bool IsHandled(DataGridColumn column)
        {
            return column.CanUserResize
                && !column.Width.IsStar
                && column.Header is string header
                && header.Length > 0;
        }
    }
}

using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Controls;
using System;

namespace Servy.Manager.Utils
{
    /// <summary>
    /// Applies, captures and resets the persisted column widths of a <see cref="DataGrid"/>.
    /// </summary>
    /// <remarks>
    /// Only the columns the user can resize and that carry a header text are handled. A star-sized column
    /// is left alone on purpose: it keeps absorbing the space the other columns leave, exactly as it does
    /// with the default layout, so the grid still fills the window after a restore. An auto-sized column is
    /// captured only once its width is absolute (the user dragged it, or a persisted width was applied), so a
    /// column the user never resized keeps sizing itself to its content.
    /// </remarks>
    public static class DataGridColumnWidths
    {
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
        public static void Apply(DataGrid grid, string persisted)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            var widths = ColumnWidths.Parse(persisted);
            if (widths.Count == 0)
            {
                return;
            }

            foreach (var column in grid.Columns)
            {
                if (IsHandled(column) && ColumnWidths.TryGetWidth(widths, column.Header as string, out var width))
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
        public static string Capture(DataGrid grid, string previous)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            var measured = new List<KeyValuePair<string, double>>();
            foreach (var column in grid.Columns)
            {
                if (IsHandled(column) && column.Width.IsAbsolute && column.Visibility == System.Windows.Visibility.Visible)
                {
                    measured.Add(new KeyValuePair<string, double>((string)column.Header, column.ActualWidth));
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
        /// The grid keeps the handler alive until
        /// <see cref="RemoveWidthChangedHandler(DataGrid, EventHandler)"/> is called with the same handler.
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

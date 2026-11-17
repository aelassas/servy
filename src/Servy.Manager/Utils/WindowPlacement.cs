using System.Globalization;

namespace Servy.Manager.Utils
{
    /// <summary>
    /// Represents the position, size and maximized state of the main window, as persisted in the
    /// <c>MainWindowPlacement</c> setting between two sessions.
    /// </summary>
    /// <remarks>
    /// The persisted form is <c>Left,Top,Width,Height,State</c>, where the four numbers are device-independent
    /// units written with the invariant culture and <c>State</c> is either <c>Normal</c> or <c>Maximized</c>.
    /// The numbers always describe the restored (non-maximized) bounds, so a window closed while maximized
    /// still returns to a sensible size once it is restored.
    /// </remarks>
    public sealed class WindowPlacement
    {
        /// <summary>
        /// The persisted state token of a window that is not maximized.
        /// </summary>
        public const string NormalState = "Normal";

        /// <summary>
        /// The persisted state token of a maximized window.
        /// </summary>
        public const string MaximizedState = "Maximized";

        /// <summary>
        /// The number of comma-separated fields in the persisted form.
        /// </summary>
        private const int FieldCount = 5;

        /// <summary>
        /// The format applied to each persisted number: at most two decimals, no trailing zeros.
        /// </summary>
        private const string NumberFormat = "0.##";

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowPlacement"/> class.
        /// </summary>
        /// <param name="left">The position of the left edge of the restored window.</param>
        /// <param name="top">The position of the top edge of the restored window.</param>
        /// <param name="width">The width of the restored window.</param>
        /// <param name="height">The height of the restored window.</param>
        /// <param name="isMaximized">Whether the window is maximized.</param>
        public WindowPlacement(double left, double top, double width, double height, bool isMaximized)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
            IsMaximized = isMaximized;
        }

        /// <summary>
        /// Gets the position of the left edge of the restored window.
        /// </summary>
        public double Left { get; }

        /// <summary>
        /// Gets the position of the top edge of the restored window.
        /// </summary>
        public double Top { get; }

        /// <summary>
        /// Gets the width of the restored window.
        /// </summary>
        public double Width { get; }

        /// <summary>
        /// Gets the height of the restored window.
        /// </summary>
        public double Height { get; }

        /// <summary>
        /// Gets a value indicating whether the window is maximized.
        /// </summary>
        public bool IsMaximized { get; }

        /// <summary>
        /// Parses the persisted form of a window placement.
        /// </summary>
        /// <param name="value">The persisted <c>Left,Top,Width,Height,State</c> string.</param>
        /// <param name="placement">The parsed placement, or <see langword="null"/> when <paramref name="value"/> is not usable.</param>
        /// <returns>
        /// <see langword="true"/> when <paramref name="value"/> holds five fields, four finite numbers with a
        /// positive width and height, and a known state; otherwise <see langword="false"/>, in which case the
        /// caller keeps the default placement.
        /// </returns>
        public static bool TryParse(string? value, out WindowPlacement? placement)
        {
            placement = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var parts = value.Split(',');
            if (parts.Length != FieldCount)
            {
                return false;
            }

            if (!TryParseNumber(parts[0], out var left)
                || !TryParseNumber(parts[1], out var top)
                || !TryParseNumber(parts[2], out var width)
                || !TryParseNumber(parts[3], out var height)
                || width <= 0
                || height <= 0)
            {
                return false;
            }

            var state = parts[4].Trim();
            bool isMaximized;
            if (string.Equals(state, MaximizedState, StringComparison.OrdinalIgnoreCase))
            {
                isMaximized = true;
            }
            else if (string.Equals(state, NormalState, StringComparison.OrdinalIgnoreCase))
            {
                isMaximized = false;
            }
            else
            {
                return false;
            }

            placement = new WindowPlacement(left, top, width, height, isMaximized);
            return true;
        }

        /// <summary>
        /// Returns the persisted <c>Left,Top,Width,Height,State</c> form of this placement.
        /// </summary>
        /// <returns>The string to store in the <c>MainWindowPlacement</c> setting.</returns>
        public string Serialize()
        {
            return string.Join(",",
                FormatNumber(Left),
                FormatNumber(Top),
                FormatNumber(Width),
                FormatNumber(Height),
                IsMaximized ? MaximizedState : NormalState);
        }

        /// <summary>
        /// Returns a placement that lies entirely inside the given screen area and respects the minimum
        /// window size, so a placement saved on a monitor that is no longer attached, or with a larger
        /// resolution, never restores the window out of reach.
        /// </summary>
        /// <param name="screenLeft">The left edge of the available screen area.</param>
        /// <param name="screenTop">The top edge of the available screen area.</param>
        /// <param name="screenWidth">The width of the available screen area.</param>
        /// <param name="screenHeight">The height of the available screen area.</param>
        /// <param name="minWidth">The minimum width of the window.</param>
        /// <param name="minHeight">The minimum height of the window.</param>
        /// <returns>
        /// The adjusted placement. The size is raised to the minimum first and then limited to the screen
        /// area (the minimum wins when the screen is smaller than it); the position is then moved so the
        /// window fits, aligned with the left and top edges when it is larger than the screen area. The
        /// placement is returned unchanged when the screen area is not a positive, finite size.
        /// </returns>
        public WindowPlacement FitTo(double screenLeft, double screenTop, double screenWidth, double screenHeight, double minWidth, double minHeight)
        {
            if (!IsFinite(screenLeft) || !IsFinite(screenTop)
                || !IsFinite(screenWidth) || !IsFinite(screenHeight)
                || screenWidth <= 0 || screenHeight <= 0)
            {
                return this;
            }

            var width = FitSize(Width, minWidth, screenWidth);
            var height = FitSize(Height, minHeight, screenHeight);
            var left = FitPosition(Left, width, screenLeft, screenWidth);
            var top = FitPosition(Top, height, screenTop, screenHeight);

            return new WindowPlacement(left, top, width, height, IsMaximized);
        }

        /// <summary>
        /// Limits a window dimension to the screen dimension, never going below the minimum.
        /// </summary>
        /// <param name="size">The saved dimension.</param>
        /// <param name="minSize">The minimum dimension of the window.</param>
        /// <param name="screenSize">The matching dimension of the screen area.</param>
        /// <returns>The dimension to restore.</returns>
        private static double FitSize(double size, double minSize, double screenSize)
        {
            var min = IsFinite(minSize) && minSize > 0 ? minSize : 0;
            return Math.Max(min, Math.Min(size, screenSize));
        }

        /// <summary>
        /// Moves a window edge so the window lies inside the screen area along one axis.
        /// </summary>
        /// <param name="position">The saved position of the leading edge.</param>
        /// <param name="size">The dimension of the window along the axis, already fitted.</param>
        /// <param name="screenStart">The position of the leading edge of the screen area.</param>
        /// <param name="screenSize">The dimension of the screen area along the axis.</param>
        /// <returns>The position to restore.</returns>
        private static double FitPosition(double position, double size, double screenStart, double screenSize)
        {
            var max = Math.Max(screenStart, screenStart + screenSize - size);
            return Math.Max(screenStart, Math.Min(position, max));
        }

        /// <summary>
        /// Parses one persisted number with the invariant culture, rejecting NaN and infinities.
        /// </summary>
        /// <param name="text">The field to parse.</param>
        /// <param name="number">The parsed number.</param>
        /// <returns><see langword="true"/> when <paramref name="text"/> is a finite number.</returns>
        private static bool TryParseNumber(string text, out double number)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && IsFinite(number);
        }

        /// <summary>
        /// Formats one number for persistence with the invariant culture.
        /// </summary>
        /// <param name="number">The number to format.</param>
        /// <returns>The persisted text of <paramref name="number"/>.</returns>
        private static string FormatNumber(double number)
        {
            return number.ToString(NumberFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Determines whether a number is neither NaN nor an infinity.
        /// </summary>
        /// <param name="number">The number to test.</param>
        /// <returns><see langword="true"/> when <paramref name="number"/> is finite.</returns>
        private static bool IsFinite(double number)
        {
            return !double.IsNaN(number) && !double.IsInfinity(number);
        }
    }
}

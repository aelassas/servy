namespace Servy.Manager.Converters
{
    /// <summary>
    /// Converts a RAM usage to a string.
    /// </summary>
    public class RamUsageConverter : ProcessMetricConverter<long>
    {
        /// <summary>
        /// Returns the RAM usage as a formatted string.
        /// </summary>
        /// <param name="value">The RAM usage in bytes.</param>
        /// <returns>A human-readable RAM usage string (e.g., "128 MB").</returns>
        protected override string Format(long value)
        {
            return ProcessHelper.FormatRamUsage(value);
        }
    }
}
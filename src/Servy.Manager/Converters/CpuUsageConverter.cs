namespace Servy.Manager.Converters
{
    /// <summary>
    /// Converts a CPU usage to a string in percentage.
    /// </summary>
    public class CpuUsageConverter : ProcessMetricConverter<double>
    {
        /// <summary>
        /// Formats a numeric CPU usage percentage into a formatted string.
        /// </summary>
        /// <param name="value">The unboxed double precision value representing percentage usage.</param>
        /// <returns>A string representing the formatted CPU usage (e.g., "1.2%").</returns>
        protected override string Format(double value)
        {
            return ProcessHelper.FormatCpuUsage(value);
        }
    }
}
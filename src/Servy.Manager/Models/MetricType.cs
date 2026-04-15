namespace Servy.Manager.Models
{
    /// <summary>
    /// Specifies the type of performance metric being monitored or visualized.
    /// Used to differentiate logic between percentage-based and capacity-based metrics.
    /// </summary>
    public enum MetricType
    {
        /// <summary>
        /// Central Processing Unit usage, typically represented as a percentage (0-100%).
        /// </summary>
        Cpu,

        /// <summary>
        /// Random Access Memory usage, typically represented in Megabytes (MB).
        /// </summary>
        Ram
    }
}
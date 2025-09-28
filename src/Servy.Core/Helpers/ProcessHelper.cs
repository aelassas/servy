using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;

namespace Servy.Core.Helpers
{
    /// <summary>
    /// Provides helper methods for retrieving and formatting process-related information
    /// such as CPU usage and RAM usage.
    /// </summary>
    public static class ProcessHelper
    {
        /// <summary>
        /// Gets the CPU usage of a process by its PID.
        /// </summary>
        /// <param name="pid">The process identifier (PID).</param>
        /// <returns>
        /// The CPU usage in percentage (0–100+), rounded to two decimals.
        /// Returns <c>0</c> if the process cannot be accessed.
        /// </returns>
        /// <remarks>
        /// This method samples the <see cref="PerformanceCounter"/> twice with a short delay.
        /// On multi-core systems, the result is normalized by <see cref="Environment.ProcessorCount"/>.
        /// Windows-only.
        /// </remarks>
        [ExcludeFromCodeCoverage]
        public static double GetCPUUsage(int pid)
        {
            try
            {
                using (var process = Process.GetProcessById(pid))
                using (var cpuCounter = new PerformanceCounter("Process", "% Processor Time", process.ProcessName, true))
                {
                    _ = cpuCounter.NextValue();
                    Thread.Sleep(500); // sampling interval
                    double value = cpuCounter.NextValue() / Environment.ProcessorCount;
                    return Math.Round(value, 2, MidpointRounding.AwayFromZero);
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is Win32Exception || ex is InvalidOperationException)
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets the RAM usage of a process by its PID.
        /// </summary>
        /// <param name="pid">The process identifier (PID).</param>
        /// <returns>
        /// The RAM usage in bytes.
        /// Returns <c>0</c> if the process cannot be accessed.
        /// </returns>
        [ExcludeFromCodeCoverage]
        public static long GetRAMUsage(int pid)
        {
            try
            {
                using (var process = Process.GetProcessById(pid))
                {
                    return process.WorkingSet64;
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is Win32Exception || ex is InvalidOperationException)
            {
                return 0;
            }
        }

        /// <summary>
        /// Formats a CPU usage value as a percentage string.
        /// </summary>
        /// <param name="cpuUsage">The CPU usage value.</param>
        /// <returns>
        /// A formatted string with a percent sign.
        /// Examples:
        /// <list type="bullet">
        /// <item><description>0 → "0%"</description></item>
        /// <item><description>0.03 → "0%"</description></item>
        /// <item><description>1 → "1.0%"</description></item>
        /// <item><description>1.04 → "1.0%"</description></item>
        /// <item><description>1.05 → "1.1%"</description></item>
        /// <item><description>1.06 → "1.1%"</description></item>
        /// <item><description>1.1 → "1.1%"</description></item>
        /// <item><description>1.49 → "1.4%"</description></item>
        /// <item><description>1.51 → "1.5%"</description></item>
        /// <item><description>1.57 → "1.5%"</description></item>
        /// <item><description>1.636 → "1.6%"</description></item>
        /// </list>
        /// </returns>
        public static string FormatCPUUsage(double cpuUsage)
        {
            double rounded = Math.Round(cpuUsage, 1, MidpointRounding.AwayFromZero);
            string formatted = rounded == 0
                ? "0"
                : rounded.ToString("0.0", CultureInfo.InvariantCulture);

            return $"{formatted}%";
        }

        /// <summary>
        /// Formats a RAM usage value in human-readable units.
        /// </summary>
        /// <param name="ramUsage">The RAM usage in bytes.</param>
        /// <returns>
        /// A formatted string with the most appropriate unit:
        /// B, KB, MB, GB, or TB.
        /// Examples:
        /// <list type="bullet">
        /// <item><description>512 → "512.0 B"</description></item>
        /// <item><description>2048 → "2.0 KB"</description></item>
        /// <item><description>1048576 → "1.0 MB"</description></item>
        /// <item><description>1073741824 → "1.0 GB"</description></item>
        /// </list>
        /// </returns>
        public static string FormatRAMUsage(long ramUsage)
        {
            const double KB = 1024.0;
            const double MB = KB * 1024.0;
            const double GB = MB * 1024.0;
            const double TB = GB * 1024.0;

            string result;
            if (ramUsage < KB)
            {
                result = $"{ramUsage.ToString("0.0", CultureInfo.InvariantCulture)} B";
            }
            else if (ramUsage < MB)
            {
                result = $"{(ramUsage / KB).ToString("0.0", CultureInfo.InvariantCulture)} KB";
            }
            else if (ramUsage < GB)
            {
                result = $"{(ramUsage / MB).ToString("0.0", CultureInfo.InvariantCulture)} MB";
            }
            else if (ramUsage < TB)
            {
                result = $"{(ramUsage / GB).ToString("0.0", CultureInfo.InvariantCulture)} GB";
            }
            else
            {
                result = $"{(ramUsage / TB).ToString("0.0", CultureInfo.InvariantCulture)} TB";
            }

            return result;
        }
    }

}

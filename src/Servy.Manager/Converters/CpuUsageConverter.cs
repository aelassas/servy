using Microsoft.Extensions.DependencyInjection;
using Servy.Core.Helpers;
using Servy.UI.Constants;
using System.Globalization;
using System.Windows.Data;

namespace Servy.Manager.Converters
{
    /// <summary>
    /// Converts a CPU usage to a string in percentage.
    /// </summary>
    public class CpuUsageConverter : IValueConverter
    {
        private readonly IProcessHelper _processHelper;

        /// <summary>
        /// CPU usage not available.
        /// </summary>
        const string UnknownCpuUsage = UiConstants.NotAvailable;

        /// <summary>
        /// Initializes a new instance of the CpuUsageConverter with dependency injection.
        /// Note: If resolving via XAML, ensure a DI-aware markup extension or service locator is configured.
        /// </summary>
        public CpuUsageConverter()
        {
            if (App.Services == null)
            {
                throw new InvalidOperationException("App.Services is not initialized. Ensure that the application is configured for dependency injection.");
            }
            _processHelper = App.Services.GetRequiredService<IProcessHelper>();
        }

        /// <summary>
        /// Returns the CPU usage as string in percentage.
        /// </summary>
        /// <param name="value">The PID.</param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns>The CPU usage as string.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Pattern matching handles the null check and unboxing in one go.
            // If the bound property is double? and is null, this will safely return false.
            if (value is double cpuUsage)
            {
                return _processHelper.FormatCpuUsage(cpuUsage);
            }

            return UnknownCpuUsage;
        }

        /// <summary>
        /// Not implemented (one-way binding only).
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
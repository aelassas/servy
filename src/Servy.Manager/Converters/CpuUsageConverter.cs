using Microsoft.Extensions.DependencyInjection;
using Servy.Core.Helpers;
using Servy.UI.Constants;
using Servy.UI.Design;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
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
        /// Initializes a new instance of the CpuUsageConverter.
        /// Provides design-time support to prevent XAML designer crashes when DI is not initialized.
        /// </summary>
        public CpuUsageConverter()
        {
            // Check for design mode before accessing App.Services
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                _processHelper = new DesignTimeProcessHelper();
                return;
            }

            _processHelper = App.Services!.GetRequiredService<IProcessHelper>();
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
            // If the bound property is double? and is null, the pattern match fails
            // and we fall through to the UnknownCpuUsage placeholder.
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
            return Binding.DoNothing;
        }
    }
}
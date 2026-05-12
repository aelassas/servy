using Microsoft.Extensions.DependencyInjection;
using Servy.Core.Helpers;
using Servy.UI.Constants;
using Servy.UI.Design;
using System;
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

            _processHelper = App.Services.GetRequiredService<IProcessHelper>();
        }

        /// <summary>
        /// Converts a numeric CPU usage percentage into a formatted string for display in the Manager UI.
        /// </summary>
        /// <param name="value">The CPU usage value, typically a <see cref="double"/> or <see cref="Nullable{Double}"/> percentage (0–100).</param>
        /// <param name="targetType">The type of the binding target property; typically <see cref="string"/>.</param>
        /// <param name="parameter">Optional converter parameter; not used in this implementation.</param>
        /// <param name="culture">The culture to use in the converter; uses the system's current UI culture.</param>
        /// <returns>
        /// A string representing the formatted CPU usage (e.g., "1.2%") produced by <see cref="_processHelper.FormatCpuUsage"/>, 
        /// or the <see cref="UnknownCpuUsage"/> placeholder if the value is null or an incompatible type.
        /// </returns>
        /// <remarks>
        /// This implementation utilizes C# pattern matching to provide a "fail-silent" guard.
        /// If the bound property is a null <see cref="Nullable{Double}"/>, the pattern match fails, 
        /// safely returning the unknown placeholder instead of throwing a <see cref="NullReferenceException"/>.
        /// </remarks>
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
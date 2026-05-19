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
    /// Converts a RAM usage to a string.
    /// </summary>
    public class RamUsageConverter : IValueConverter
    {
        private readonly IProcessHelper _processHelper;

        /// <summary>
        /// RAM usage not available.
        /// </summary>
        const string UnknownRamUsage = UiConstants.NotAvailable;

        /// <summary>
        /// Initializes a new instance of the RamUsageConverter with dependency injection.
        /// Note: If resolving via XAML, ensure a DI-aware markup extension or service locator is configured.
        /// </summary>
        public RamUsageConverter()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                _processHelper = new DesignTimeProcessHelper();
                return;
            }

            _processHelper = App.Services?.GetService<IProcessHelper>() ?? new DesignTimeProcessHelper();
        }

        /// <summary>
        /// Returns the RAM usage as a formatted string.
        /// </summary>
        /// <param name="value">The RAM usage in bytes.</param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns>A human-readable RAM usage string (e.g., "128 MB") or the unknown placeholder.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Pattern matching handles the null check and unboxing in one go.
            // If the bound property is long (RAM in bytes) and is null, the pattern match fails
            // and we fall through to the UnknownRamUsage placeholder.
            if (value is long ramUsage)
            {
                return _processHelper.FormatRamUsage(ramUsage);
            }

            return UnknownRamUsage;
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
using Servy.Core.Enums;
using Servy.Manager.Resources;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Servy.Manager.Converters
{
    /// <summary>
    /// Converts between <see cref="ServiceStartType"/> enum values and their localized string representations
    /// defined in <see cref="Strings.resx"/>.
    /// </summary>
    public class StartupTypeConverter : IValueConverter
    {
        /// <summary>
        /// Converts a <see cref="ServiceStartType"/> value to its localized string.
        /// </summary>
        /// <param name="value">The enum value to convert.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">Optional parameter (unused).</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>The localized string corresponding to the enum value.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ServiceStartType status)
            {
                switch (status)
                {
                    case ServiceStartType.Automatic:
                        return Strings.StartupType_Automatic;
                    case ServiceStartType.AutomaticDelayedStart:
                        return Strings.StartupType_AutomaticDelayedStart;
                    case ServiceStartType.Manual:
                        return Strings.StartupType_Manual;
                    case ServiceStartType.Disabled:
                        return Strings.StartupType_Disabled;
                }
            }

            return value?.ToString() ?? Strings.Label_Fetching;
        }

        /// <summary>
        /// Converts a localized string back to its corresponding <see cref="ServiceStartType"/> value.
        /// </summary>
        /// <param name="value">The localized string to convert.</param>
        /// <param name="targetType">The type to convert to.</param>
        /// <param name="parameter">Optional parameter (unused).</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>
        /// The corresponding <see cref="ServiceStartType"/> value if the string matches a known value; 
        /// otherwise, <see cref="Binding.DoNothing"/>.
        /// </returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                if (str == Strings.StartupType_Automatic)
                    return ServiceStartType.Automatic;
                if (str == Strings.StartupType_AutomaticDelayedStart)
                    return ServiceStartType.AutomaticDelayedStart;
                if (str == Strings.StartupType_Manual)
                    return ServiceStartType.Manual;
                if (str == Strings.StartupType_Disabled)
                    return ServiceStartType.Disabled;
            }

            return Binding.DoNothing;
        }
    }
}

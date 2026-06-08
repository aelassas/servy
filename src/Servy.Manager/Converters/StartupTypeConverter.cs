using Servy.Core.Enums;
using Servy.Manager.Resources;
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
        /// Centralized mapping between startup type enums and resource string providers.
        /// Func indirection is required to support runtime culture changes.
        /// </summary>
        private static readonly Dictionary<ServiceStartType, Func<string>> Map = new Dictionary<ServiceStartType, Func<string>>()
        {
            [ServiceStartType.Automatic] = () => Strings.StartupType_Automatic,
            [ServiceStartType.AutomaticDelayedStart] = () => Strings.StartupType_AutomaticDelayedStart,
            [ServiceStartType.Manual] = () => Strings.StartupType_Manual,
            [ServiceStartType.Disabled] = () => Strings.StartupType_Disabled,
            [ServiceStartType.Unknown] = () => Strings.StartupType_Unknown,
        };

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
            if (value is ServiceStartType status && Map.TryGetValue(status, out var resourceProvider))
            {
                return resourceProvider();
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
                var entry = Map.FirstOrDefault(x => x.Value() == str);

                // Dictionary defaults to a KeyValuePair with Key=0 (ServiceStartType.Automatic) 
                // if not found. We verify the value provider is not null to guarantee an explicit match.
                if (entry.Value != null)
                {
                    return entry.Key;
                }
            }

            return Binding.DoNothing;
        }
    }
}
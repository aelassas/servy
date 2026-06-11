using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Servy.Manager.Converters
{
    /// <summary>
    /// Provides a generic base implementation for converting between enum values and their localized string representations.
    /// </summary>
    /// <typeparam name="TEnum">The struct enum type being managed by the converter.</typeparam>
    public abstract class EnumLocalizedConverter<TEnum> : IValueConverter where TEnum : struct, Enum
    {
        private readonly Dictionary<TEnum, Func<string>> _map;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnumLocalizedConverter{TEnum}"/> class.
        /// </summary>
        /// <param name="map">The centralized mapping between enum fields and resource string providers.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="map"/> is null.</exception>
        protected EnumLocalizedConverter(Dictionary<TEnum, Func<string>> map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
        }

        /// <summary>
        /// Converts an enum value to its localized string representation.
        /// </summary>
        /// <param name="value">The enum value to convert.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">Optional parameter (unused).</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>A localized string corresponding to the enum value, or a customized fallback on errors.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TEnum enumValue && _map.TryGetValue(enumValue, out var resourceProvider))
            {
                return resourceProvider();
            }

            return GetFallbackValue(value);
        }

        /// <summary>
        /// Converts a localized string back to its corresponding enum value.
        /// </summary>
        /// <param name="value">The localized string to convert.</param>
        /// <param name="targetType">The type to convert to.</param>
        /// <param name="parameter">Optional parameter (unused).</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>The corresponding enum value if found; otherwise, <see cref="Binding.DoNothing"/>.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                var entry = _map.FirstOrDefault(x => x.Value() == str);

                // Dictionary defaults to a KeyValuePair with Key=0 if not found.
                // We verify the value provider is not null to guarantee an explicit match.
                if (entry.Value != null)
                {
                    return entry.Key;
                }
            }

            return Binding.DoNothing;
        }

        /// <summary>
        /// When overridden in a derived class, provides a customized fallback value when conversion fails.
        /// </summary>
        /// <param name="value">The raw input value passing through the conversion route.</param>
        /// <returns>The fallback string representation.</returns>
        protected abstract string GetFallbackValue(object value);
    }
}
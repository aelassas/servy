using Servy.Core.Enums;
using Servy.Manager.Resources;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace Servy.Manager.Converters
{
    /// <summary>
    /// Converts between <see cref="ServiceStatus"/> values and their localized string 
    /// representations defined in <see cref="Strings.resx"/>.
    /// </summary>
    public class StatusConverter : IValueConverter
    {
        /// <summary>
        /// Centralized mapping between status enums and resource string providers.
        /// Func indirection is required to support runtime culture changes.
        /// </summary>
        private static readonly Dictionary<ServiceStatus, Func<string>> Map = new Dictionary<ServiceStatus, Func<string>>()
        {
            [ServiceStatus.None] = () => Strings.Label_Fetching,
            [ServiceStatus.NotInstalled] = () => Strings.Status_NotInstalled,
            [ServiceStatus.Stopped] = () => Strings.Status_Stopped,
            [ServiceStatus.StartPending] = () => Strings.Status_StartPending,
            [ServiceStatus.StopPending] = () => Strings.Status_StopPending,
            [ServiceStatus.Running] = () => Strings.Status_Running,
            [ServiceStatus.ContinuePending] = () => Strings.Status_ContinuePending,
            [ServiceStatus.PausePending] = () => Strings.Status_PausePending,
            [ServiceStatus.Paused] = () => Strings.Status_Paused,
        };

        /// <summary>
        /// Converts a <see cref="ServiceStatus"/> value to its localized string.
        /// </summary>
        /// <param name="value">The enum value to convert.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">Optional parameter (unused).</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>
        /// A localized string corresponding to the <see cref="ServiceStatus"/> value, 
        /// or the raw string representation if the binding is broken or unknown.
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ServiceStatus status && Map.TryGetValue(status, out var resourceProvider))
            {
                return resourceProvider();
            }

            // Return the raw value or empty string to surface binding errors 
            // rather than masquerading as 'Not Installed'.
            return value?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Converts a localized string back to its corresponding <see cref="ServiceStatus"/> value.
        /// </summary>
        /// <param name="value">The localized string to convert.</param>
        /// <param name="targetType">The type to convert to.</param>
        /// <param name="parameter">Optional parameter (unused).</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>
        /// The corresponding <see cref="ServiceStatus"/> value if the string matches a known resource; 
        /// otherwise, <see cref="Binding.DoNothing"/>.
        /// </returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                var entry = Map.FirstOrDefault(x => x.Value() == str);

                // Dictionary defaults to a KeyValuePair with Key=0 (ServiceStatus.None) 
                // if not found. We verify the value matches to be safe.
                if (entry.Value != null)
                {
                    return entry.Key;
                }
            }

            return Binding.DoNothing;
        }
    }
}
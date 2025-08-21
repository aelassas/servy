using Servy.Core.Enums;
using Servy.Manager.Resources;
using System;
using System.Globalization;
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
        /// Converts a <see cref="ServiceStatus"/> value to its localized string.
        /// </summary>
        /// <param name="value">The enum value to convert.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">Optional parameter (unused).</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>
        /// A localized string corresponding to the <see cref="ServiceStatus"/> value, 
        /// or the <see cref="object.ToString"/> representation if no match is found.
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ServiceStatus status)
            {
                switch (status)
                {
                    case ServiceStatus.None:
                        return Strings.Label_Fetching;
                    case ServiceStatus.NotInstalled:
                        return Strings.Status_NotInstalled;
                    case ServiceStatus.Stopped:
                        return Strings.Status_Stopped;
                    case ServiceStatus.StartPending:
                        return Strings.Status_StartPending;
                    case ServiceStatus.StopPending:
                        return Strings.Status_StopPending;
                    case ServiceStatus.Running:
                        return Strings.Status_Running;
                    case ServiceStatus.ContinuePending:
                        return Strings.Status_ContinuePending;
                    case ServiceStatus.PausePending:
                        return Strings.Status_PausePending;
                    case ServiceStatus.Paused:
                        return Strings.Status_Paused;
                }
            }

            return value?.ToString() ?? Strings.Status_NotInstalled;
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
                if (str == Strings.Label_Fetching)
                    return ServiceStatus.None;
                if (str == Strings.Status_Stopped)
                    return ServiceStatus.Stopped;
                if (str == Strings.Status_StartPending)
                    return ServiceStatus.StartPending;
                if (str == Strings.Status_StopPending)
                    return ServiceStatus.StopPending;
                if (str == Strings.Status_Running)
                    return ServiceStatus.Running;
                if (str == Strings.Status_ContinuePending)
                    return ServiceStatus.ContinuePending;
                if (str == Strings.Status_PausePending)
                    return ServiceStatus.PausePending;
                if (str == Strings.Status_Paused)
                    return ServiceStatus.Paused;
            }

            return Binding.DoNothing;
        }
    }
}

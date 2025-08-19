using Servy.Manager.Resources;
using System.Globalization;
using System.ServiceProcess;
using System.Windows.Data;

namespace Servy.Manager.Converters
{
    /// <summary>
    /// Converts between <see cref="ServiceControllerStatus"/> values and their localized string 
    /// representations defined in <see cref="Strings.resx"/>.
    /// </summary>
    public class StatusConverter : IValueConverter
    {
        /// <summary>
        /// Converts a <see cref="ServiceControllerStatus"/> value to its localized string.
        /// </summary>
        /// <param name="value">The enum value to convert.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">Optional parameter (unused).</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>
        /// A localized string corresponding to the <see cref="ServiceControllerStatus"/> value, 
        /// or the <see cref="object.ToString"/> representation if no match is found.
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ServiceControllerStatus status)
            {
                switch (status)
                {
                    case ServiceControllerStatus.Stopped:
                        return Strings.Status_Stopped;
                    case ServiceControllerStatus.StartPending:
                        return Strings.Status_StartPending;
                    case ServiceControllerStatus.StopPending:
                        return Strings.Status_StopPending;
                    case ServiceControllerStatus.Running:
                        return Strings.Status_Running;
                    case ServiceControllerStatus.ContinuePending:
                        return Strings.Status_ContinuePending;
                    case ServiceControllerStatus.PausePending:
                        return Strings.Status_PausePending;
                    case ServiceControllerStatus.Paused:
                        return Strings.Status_Paused;
                }
            }

            return value?.ToString() ?? Strings.Status_NotInstalled;
        }

        /// <summary>
        /// Converts a localized string back to its corresponding <see cref="ServiceControllerStatus"/> value.
        /// </summary>
        /// <param name="value">The localized string to convert.</param>
        /// <param name="targetType">The type to convert to.</param>
        /// <param name="parameter">Optional parameter (unused).</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>
        /// The corresponding <see cref="ServiceControllerStatus"/> value if the string matches a known resource; 
        /// otherwise, <see cref="Binding.DoNothing"/>.
        /// </returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                if (str == Strings.Status_Stopped)
                    return ServiceControllerStatus.Stopped;
                if (str == Strings.Status_StartPending)
                    return ServiceControllerStatus.StartPending;
                if (str == Strings.Status_StopPending)
                    return ServiceControllerStatus.StopPending;
                if (str == Strings.Status_Running)
                    return ServiceControllerStatus.Running;
                if (str == Strings.Status_ContinuePending)
                    return ServiceControllerStatus.ContinuePending;
                if (str == Strings.Status_PausePending)
                    return ServiceControllerStatus.PausePending;
                if (str == Strings.Status_Paused)
                    return ServiceControllerStatus.Paused;
            }

            return Binding.DoNothing;
        }
    }
}

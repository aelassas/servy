using Servy.Core.Enums;
using Servy.Manager.Resources;
using System;
using System.Collections.Generic;

namespace Servy.Manager.Converters
{
    /// <summary>
    /// Converts between <see cref="ServiceStatus"/> values and their localized string
    /// representations defined in <see cref="Strings"/>.
    /// </summary>
    public class StatusConverter : EnumLocalizedConverter<ServiceStatus>
    {
        private static readonly Dictionary<ServiceStatus, Func<string>> StatusMap = new Dictionary<ServiceStatus, Func<string>>()
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
        /// Initializes a new instance of the <see cref="StatusConverter"/> class.
        /// </summary>
        public StatusConverter() : base(StatusMap)
        {
        }

        /// <summary>
        /// Returns a fallback string when the bound value is not a mapped <see cref="ServiceStatus"/>.
        /// </summary>
        /// <param name="value">The unmapped source value.</param>
        /// <returns>The value's string representation, or an empty string when null.</returns>
        protected override string GetFallbackValue(object value)
        {
            // Return the raw value or empty string to surface binding errors
            // rather than masquerading as 'Not Installed'.
            return value?.ToString() ?? string.Empty;
        }
    }
}
